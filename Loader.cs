using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Loader; // Look in NuGet to get this.

public static class CodeOlive
{
    private delegate void _OnLoaded(Plugin P);
    private class LoadSwitch
    {
        public event _OnLoaded OnLoaded;
        public LoadSwitch(Plugin Plg)
        {
            MRE = new ManualResetEvent(true);
            Return = Plg;
        }
        public LoadSwitch() => MRE = new ManualResetEvent(false);

        private readonly ManualResetEvent MRE;
        public bool IsSet => Return != null;

        public Plugin Return;
        public void Set(Plugin Plg)
        {
            Return = Plg;
            MRE.Set();
            OnLoaded?.Invoke(Plg);
        }

        public Plugin Wait()
        {
            while (!this.IsSet)
            {
                MRE.WaitOne(1000);
                if (this.IsSet) break;
                MRE.WaitOne();
            }
            return Return;
        }
    }

    public const uint Version = 1;

    // Activation List to Load SoftDependencies (optimizer so i dont have as much overhead RAM during runtime, this will decreass loading speed during multiple plugins being loaded)
    private static List<Action> OnStart = new List<Action>();
    // Wait Triggers if Hard Dependencies are missing.
    private static readonly Dictionary<string, LoadSwitch> LoadTriggers = new Dictionary<string, LoadSwitch>();
    private static readonly Dictionary<string, Type> CoreLibrarys = new Dictionary<string, Type>();
    private static readonly Dictionary<String, Plugin> Plugins = new Dictionary<string, Plugin>();

    public static void AddCoreLibrary(String Name, Type Class)
    {
        if (Plugins.Count > 0)
            throw new InvalidOperationException();
        CoreLibrarys.Add(Name, Class);
    }

    /// <summary>
    /// Load and Hook A Plugin But Async
    /// </summary>
    /// <param name="PluginFile">Path to PluginFile</param>
    /// <returns>Plugin Load Result</returns>
    public static async Task<PluginLoadResult> AsyncLoadPlugin(String PluginFile) => await Task.Run<PluginLoadResult>(() => { return LoadPlugin(PluginFile); });

    /// <summary>
    /// Load and Hook A Plugin
    /// </summary>
    /// <param name="PluginFile">Path to PluginFile</param>
    /// <returns>Plugin Load Result</returns>
    public static PluginLoadResult LoadPlugin(String PluginFile)
    {
        Assembly ASM = Assembly.LoadFile(Path.GetFullPath(PluginFile));
        // Find Mount Types
        List<Type> MountPoints = new List<Type>();
        Parallel.ForEach(ASM.GetExportedTypes(), (Type T) =>
        {
            if (T.IsAbstract && T.IsClass)
                if (T.Name == "CodeOliveInfo")
                    MountPoints.Add(T);
        });
        // Find Best MountPoint
        Type MountPoint = null;
        uint MountPointVersion = 0;
        if (MountPoints.Count > 1)
        {
            foreach (Type T in MountPoints)
            {
                FieldInfo Verin = T.GetField("CodeOliveVersion");
                if (Verin == null || Verin.FieldType != typeof(Int32)) continue;
                if (MountPoint == null)
                {
                    MountPointVersion = (uint)Verin.GetRawConstantValue();
                    if (MountPointVersion <= Version)
                        MountPoint = T;
                }
                else
                {
                    uint Ver = (uint)Verin.GetRawConstantValue();
                    if (Ver <= Version && Ver > MountPointVersion)
                    {
                        MountPoint = T;
                        MountPointVersion = Ver;
                    }
                    else if (Ver == MountPointVersion)
                        return new PluginLoadResult
                        {
                            Error = LoadError.NoOrInvalidCodeOliveInfoClass,

                            // Will be there if got parsed
                            OliveVersion = UInt32.MaxValue,
                            Version = PluginVersion.Invalid,
                        };
                }
            }
            if (MountPoint == null)
                return new PluginLoadResult
                {
                    Error = LoadError.NoOrInvalidCodeOliveInfoClass,

                    // Will be there if got parsed
                    OliveVersion = UInt32.MaxValue,
                    Version = PluginVersion.Invalid,
                };
        }
        else
        {
            MountPoint = MountPoints[0];
            FieldInfo Verin = MountPoint.GetField("CodeOliveVersion");
            if (Verin == null || Verin.FieldType != typeof(UInt32))
                return new PluginLoadResult
                {
                    Error = LoadError.NoOrInvalidCodeOliveInfoClass,
                    OliveVersion = 0,
                    Version = PluginVersion.Invalid,
                };
            MountPointVersion = (uint)Verin.GetRawConstantValue();
        }
        MountPoints = null; // Ram Clean Up.

        // Read MountPoint Data
        uint Major = PluginVersion.Invalid.Major;
        uint Minor = PluginVersion.Invalid.Minor;
        string Build = PluginVersion.Invalid.Build;
        switch (MountPointVersion)
        {
            case 1: // Current
            {
                // Get Version Data
                FieldInfo VersionStr = MountPoint.GetField("Version");
                if (VersionStr != null && VersionStr.FieldType == typeof(String))
                {
                    PluginVersion? PV = PluginVersion.Parse(VersionStr.GetRawConstantValue() as String);
                    if (PV.HasValue)
                    {
                        Major = PV.Value.Major;
                        Minor = PV.Value.Minor;
                        if (PV.Value.Build != null)
                        {
                            Build = PV.Value.Build;
                            if (Build == "")
                                return new PluginLoadResult
                                {
                                    Error = LoadError.NoOrInvalidCodeOliveInfoClass,
                                    Version = new PluginVersion(Major, Minor, Build),
                                };

                            char F = Build[0];
                            if (F == '>' || F == '<')
                                return new PluginLoadResult
                                {
                                    Error = LoadError.NoOrInvalidCodeOliveInfoClass,
                                    Version = new PluginVersion(Major, Minor, Build),
                                };
                        }
                    }
                    else
                        return new PluginLoadResult
                        {
                            Error = LoadError.NoOrInvalidCodeOliveInfoClass,
                            Version = new PluginVersion(Major, Minor, Build),
                        };
                }
                else
                {
                    FieldInfo MajorUInt = MountPoint.GetField("Version_Major");
                    FieldInfo MinorUInt = MountPoint.GetField("Version_Minor");
                    if (MajorUInt == null || MinorUInt == null || MajorUInt.FieldType != typeof(UInt32) || MinorUInt.FieldType != typeof(UInt32))
                        return new PluginLoadResult
                        {
                            Error = LoadError.NoOrInvalidCodeOliveInfoClass,
                            Version = new PluginVersion(Major, Minor, Build),
                        };

                    Major = (uint)MajorUInt.GetRawConstantValue();
                    Minor = (uint)MinorUInt.GetRawConstantValue();

                    FieldInfo BuildStr = MountPoint.GetField("Version_Build");
                    if (BuildStr != null && BuildStr.FieldType == typeof(String))
                    {
                        Build = BuildStr.GetRawConstantValue() as String;
                        if (Build != null)
                        {
                            if (Build == "")
                                return new PluginLoadResult
                                {
                                    Error = LoadError.NoOrInvalidCodeOliveInfoClass,
                                    Version = new PluginVersion(Major, Minor, Build),
                                };

                            char F = Build[0];
                            if (F == '>' || F == '<')
                                return new PluginLoadResult
                                {
                                    Error = LoadError.NoOrInvalidCodeOliveInfoClass,
                                    Version = new PluginVersion(Major, Minor, Build),
                                };
                        }
                    }
                }

                // Get Name
                FieldInfo NameStr = MountPoint.GetField("Name");
                if (NameStr != null && NameStr.FieldType == typeof(String))
                {
                    String Name = NameStr.GetRawConstantValue() as String;
                    if (Name != null || Name.Length == 0)
                    {
                        char F_N = Name[0];
                        if (F_N == '>' || F_N == '<')
                            return new PluginLoadResult
                            {
                                Error = LoadError.IlegalName,
                                OliveVersion = MountPointVersion,
                                Version = new PluginVersion(Major, Minor, Build),
                                Name = Name,
                            };
                        else
                        {
                            PluginLoadResult Rest = new PluginLoadResult
                            {
                                Error = null,
                                OliveVersion = MountPointVersion,
                                Version = new PluginVersion(Major, Minor, Build),
                                Name = Name,
                            };

                            // Find All Hard Dependencies

                            HookPluginLoad(ref Rest, MountPoint, ASM, AssemblyLoadContext.GetLoadContext(ASM));

                            return Rest;
                        }
                    }
                    else
                        return new PluginLoadResult
                        {
                            Error = LoadError.IlegalName,
                            OliveVersion = MountPointVersion,
                            Version = new PluginVersion(Major, Minor, Build),
                            Name = Name,
                        };
                }
                return new PluginLoadResult
                {
                    Error = LoadError.IlegalName,
                    OliveVersion = MountPointVersion,
                    Version = new PluginVersion(Major, Minor, Build),
                };
            }

            default:
                return new PluginLoadResult
                {
                    Error = LoadError.OliveOutdated,
                    OliveVersion = MountPointVersion,
                    Version = new PluginVersion(Major, Minor, Build),
                };
        }
    }

    private static void HookPluginLoad(ref PluginLoadResult __inf, Type Mountpt, Assembly ASM, AssemblyLoadContext ALC)
    {
        Plugin EndResult = new Plugin()
        {
            State = LoadState.Awaiting,
            Name = __inf.Name,
            Version = __inf.Version,
            Assembly = ASM,
            MountPoint = Mountpt,
            MRS = new MethodReplacementState[0],
            Dependents = new Plugin[0],
        };
        // This is used to laod External Assemblies refranced by the Plugin.
        ALC.Resolving += (AssemblyLoadContext _, AssemblyName AN) =>
        {
            foreach (String S in Directory.EnumerateFiles($"{Path.GetDirectoryName(ASM.Location)}{Path.DirectorySeparatorChar}{EndResult.Name}", "*.dll", SearchOption.TopDirectoryOnly))
            {
                String DPath = Path.GetFullPath(S);
                AssemblyName As_n = AssemblyName.GetAssemblyName(DPath);
                if (As_n.FullName == AN.FullName && As_n.CultureInfo.Name == AN.CultureInfo.Name && As_n.Version >= AN.Version)
                    return ALC.LoadFromAssemblyPath(DPath);
            }
            return null;
        };
        ALC.Unloading += (AssemblyLoadContext _) => EndResult.Unload();
        try
        {
            Mountpt.GetMethod("Load")?.Invoke(null, null);
        }
        catch
        {
            EndResult.State = LoadState.Crashed;
            EndResult.Unload();
            return;
        }

        Dictionary<String, List<Reflection_RemoteFunction>> HardDeps = new Dictionary<String, List<Reflection_RemoteFunction>>();
        Dictionary<String, List<Reflection_RemoteFunction>> SoftDeps = new Dictionary<String, List<Reflection_RemoteFunction>>();

        bool Inject(MethodInfo T, MethodInfo I, Plugin From)
        {
            MethodReplacementState MRS;
            try
            {
                MRS = Method_Inject(T, I);
            }
            catch
            {
                return false;
            }
            if (From != null)
            {
                lock (From.MRS)
                {
                    int _inx = From.MRS.Length;
                    Array.Resize(ref From.MRS, _inx + 1);
                    From.MRS[_inx] = MRS;
                }
                lock (From.Dependents)
                {
                    foreach (Plugin P in From.Dependents)
                        if (P == EndResult)
                            return true;
                    int _inx = From.Dependents.Length;
                    Array.Resize(ref From.Dependents, _inx + 1);
                    From.Dependents[_inx] = EndResult;
                }
            }
            return true;
        }

        Parallel.ForEach(ASM.GetExportedTypes(), (Type Class) =>
        {
            if (Class.IsClass && Class.IsAbstract && Class.IsPublic)
                Parallel.ForEach(Class.GetMethods(BindingFlags.Static | BindingFlags.Public), (MethodInfo minf) =>
                {
                    foreach (CustomAttributeData Attr in minf.GetCustomAttributesData())
                    {
                        bool IsDefined = false; ;
                        switch (Attr.AttributeType.Name)
                        {
                            case "OliveSoftDependencyAttribute":
                            {

                                if (!(Attr.ConstructorArguments[0].Value is String Dep) ||
                                    !(Attr.ConstructorArguments[1].Value is String DepFunc))
                                    continue;

                                int in_x = DepFunc.LastIndexOf('.');
                                if (in_x > 0)
                                {
                                    String FuncName = DepFunc.Substring(in_x + 1);
                                    DepFunc = DepFunc.Remove(in_x);

                                    if (SoftDeps.ContainsKey(Dep))
                                        SoftDeps[Dep].Add(new Reflection_RemoteFunction { Class = DepFunc, FunctionName = FuncName, OgFunction = minf });
                                    else
                                        SoftDeps.Add(Dep, new List<Reflection_RemoteFunction>() { new Reflection_RemoteFunction { Class = DepFunc, FunctionName = FuncName, OgFunction = minf } });
                                }
                                IsDefined = true;
                            }
                            break;
                            case "OliveHardDependencyAttribute":
                            {

                                if (!(Attr.ConstructorArguments[0].Value is String Dep) ||
                                    !(Attr.ConstructorArguments[1].Value is String DepFunc))
                                    continue;

                                int in_x = DepFunc.LastIndexOf('.');
                                if (in_x > 0)
                                {
                                    String FuncName = DepFunc.Substring(in_x + 1);
                                    DepFunc = DepFunc.Remove(in_x);
                                    if (HardDeps.ContainsKey(Dep))
                                    {
                                        if (LoadTriggers.ContainsKey(Dep))
                                        {
                                            if (LoadTriggers[Dep].IsSet)
                                            {
                                                Plugin Plg = LoadTriggers[Dep].Wait();
                                                try
                                                {
                                                    MethodInfo I = Plg.Assembly.GetType(DepFunc)?.GetMethod(FuncName);
                                                    if (I != null)
                                                    {
                                                        if (!Inject(minf, I, Plg))
                                                        {
                                                            EndResult.Unload();
                                                            return;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        EndResult.Unload();
                                                        return;
                                                    }
                                                }
                                                catch
                                                {
                                                    EndResult.Unload();
                                                    return;
                                                }
                                            }
                                            else
                                                HardDeps[Dep].Add(new Reflection_RemoteFunction { Class = DepFunc, FunctionName = FuncName, OgFunction = minf });
                                        }
                                        else
                                            HardDeps[Dep].Add(new Reflection_RemoteFunction { Class = DepFunc, FunctionName = FuncName, OgFunction = minf });
                                    }
                                    else
                                        HardDeps.Add(Dep, new List<Reflection_RemoteFunction>() { new Reflection_RemoteFunction { Class = DepFunc, FunctionName = FuncName, OgFunction = minf } });
                                }
                                else if(CoreLibrarys.ContainsKey(Dep))
                                {
                                    MethodInfo I = CoreLibrarys[Dep].GetMethod(DepFunc);
                                    if (I != null)
                                    {
                                        if (!Inject(minf, I, null))
                                        {
                                            EndResult.Unload();
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        EndResult.Unload();
                                        return;
                                    }
                                }
                                IsDefined = true;
                            }
                            break;
                        }
                        if (IsDefined)
                            break;
                    }
                });
        });

        if (HardDeps.Count == 0)
        {
            try
            {
                Mountpt.GetMethod("Init")?.Invoke(null, null);
            }
            catch
            {
                HardDeps = null;
                SoftDeps = null;
                EndResult.State = LoadState.Crashed;
                EndResult.Unload();
                return;
            }
            EndResult.State = LoadState.Initulized;
        }
        else
        {
            Dictionary<String, _OnLoaded> ActiveLoads = new Dictionary<String, _OnLoaded>();
            foreach (KeyValuePair<String, List<Reflection_RemoteFunction>> _d in HardDeps)
            {
                lock (LoadTriggers)
                {
                    LoadSwitch ls;
                    void L(Plugin P)
                    {
                        ls.OnLoaded -= L;
                        ActiveLoads.Remove(_d.Key);
                        foreach (Reflection_RemoteFunction RF in _d.Value)
                        {
                            try
                            {
                                MethodInfo I = P.Assembly.GetType(RF.Class)?.GetMethod(RF.FunctionName);
                                if (I != null)
                                {
                                    if (!Inject(RF.OgFunction, I, P))
                                    {
                                        EndResult.Unload();
                                        return;
                                    }
                                }
                                else
                                {
                                    EndResult.Unload();
                                    return;
                                }
                            }
                            catch
                            {
                                EndResult.Unload();
                                return;
                            }
                        }
                        if (ActiveLoads.Count == 0 && SoftDeps.Count == 0)
                            try
                            {
                                Mountpt.GetMethod("Init")?.Invoke(null, null);
                            }
                            catch
                            {
                                HardDeps = null;
                                SoftDeps = null;
                                EndResult.State = LoadState.Crashed;
                                EndResult.Unload();
                                return;
                            }
                    }
                    if (LoadTriggers.ContainsKey(_d.Key))
                    {
                        ls = LoadTriggers[_d.Key];
                        if (ls.IsSet)
                        {
                            Plugin P = ls.Return;
                            foreach (Reflection_RemoteFunction RF in _d.Value)
                            {
                                try
                                {
                                    MethodInfo I = P.Assembly.GetType(RF.Class)?.GetMethod(RF.FunctionName);
                                    if (I != null && Inject(RF.OgFunction, I, P))
                                        continue;
                                    else
                                    {
                                        EndResult.Unload();
                                        foreach (KeyValuePair<string, _OnLoaded> Lo in ActiveLoads)
                                            LoadTriggers[Lo.Key].OnLoaded -= Lo.Value;
                                        return;
                                    }
                                }
                                catch
                                {
                                    EndResult.Unload();
                                    foreach (KeyValuePair<string, _OnLoaded> Lo in ActiveLoads)
                                        LoadTriggers[Lo.Key].OnLoaded -= Lo.Value;
                                    return;
                                }
                            }
                        }
                        else
                        {
                            ls.OnLoaded += L;
                            ActiveLoads.Add(_d.Key, L);
                        }
                    }
                    else
                    {
                        ls = new LoadSwitch();
                        LoadTriggers[_d.Key] = ls;
                        ls.OnLoaded += L;
                        ActiveLoads.Add(_d.Key, L);
                    }
                }
            }

            EndResult.OnUnload += (Plugin P) =>
            {
                lock (LoadTriggers)
                    foreach (KeyValuePair<string, _OnLoaded> Lo in ActiveLoads)
                        LoadTriggers[Lo.Key].OnLoaded -= Lo.Value;
                EndResult.State = LoadState.Failed;
            };
            EndResult.State = LoadState.Loaded;
        }

        Plugins.Add(EndResult.Name, EndResult);
        lock (LoadTriggers)
            if (LoadTriggers.ContainsKey(__inf.Name))
                LoadTriggers[__inf.Name].Set(EndResult);
            else
                LoadTriggers[__inf.Name] = new LoadSwitch(EndResult);

        if (SoftDeps.Count > 0)
        {
            void S()
            {
                Parallel.ForEach(SoftDeps, (KeyValuePair<String, List<Reflection_RemoteFunction>> _d, ParallelLoopState State) =>
                {
                    if (Plugins.ContainsKey(_d.Key))
                    {
                        Plugin P = Plugins[_d.Key];
                        Parallel.ForEach(_d.Value, (Reflection_RemoteFunction F) =>
                        {
                            MethodInfo I = P.Assembly.GetType(F.Class)?.GetMethod(F.FunctionName);
                            if (I != null)
                                Inject(F.OgFunction, I, P);
                        });
                    }
                });

                try
                {
                    Mountpt.GetMethod("Init")?.Invoke(null, null);
                }
                catch
                {
                    HardDeps = null;
                    SoftDeps = null;
                    EndResult.State = LoadState.Crashed;
                    EndResult.Unload();
                    return;
                }
            }
            if (OnStart != null)
                lock (OnStart)
                    if (OnStart != null)
                    {
                        OnStart.Add(S);
                        return;
                    }

            // if OnStart == null by the time we try and attatch S, Just call it
            S();
            try
            {

                Mountpt.GetMethod("Start")?.Invoke(null, null);
            }
            catch
            {
                HardDeps = null;
                SoftDeps = null;
                EndResult.State = LoadState.Crashed;
                EndResult.Unload();
            }
            EndResult.State = LoadState.Started;
        }
        else if (OnStart == null)
        {
            EndResult.State = LoadState.Started;
            Mountpt.GetMethod("Start")?.Invoke(null, null);
        }
    }

    /// <summary>
    /// Reslove Soft Dependencies
    /// </summary>
    public static void Start()
    {
        if (OnStart != null)
            lock (OnStart)
            {
                // Hook Any Soft Deps available
                Parallel.ForEach(OnStart, (Action A) => A());
                OnStart = null;
                Parallel.ForEach(Plugins, (KeyValuePair<String, Plugin> _d) =>
                {
                    if (_d.Value.State != LoadState.Initulized)
                        _d.Value.Unload();
                    else
                    {
                        try
                        {
                            _d.Value.MountPoint.GetMethod("Start")?.Invoke(null, null);
                            _d.Value.State = LoadState.Started;
                        }
                        catch
                        {
                            _d.Value.State = LoadState.Crashed;
                            _d.Value.Unload();
                        }
                    }
                });
            }
    }

    private class Plugin : OlivePlugin
    {
        public event _OnLoaded OnUnload;
        public event OlivePluginUnloadEvent OnUnLoad;
        private bool Unloaded = false;
        public LoadState State;
        public Assembly Assembly;
        public Type MountPoint;
        public string Name;
        public PluginVersion Version;
        public MethodReplacementState[] MRS;
        public Plugin[] Dependents;

        String OlivePlugin.Name => this.Name;
        Assembly OlivePlugin.Assembly => this.Assembly;
        LoadState OlivePlugin.State => this.State;
        PluginVersion OlivePlugin.Version => this.Version;

        public void Unload() => this.Dispose();
        public void Dispose()
        {
            if (this.Unloaded)
                return;
            lock (this)
            {
                if (this.Unloaded)
                    return;
                this.Unloaded = true;

                OnUnload?.Invoke(this); // Internal Event
                try
                {
                    OnUnLoad?.Invoke(this); // External Event (Uses Interface oposed to Actual Plugin Class)
                }
                catch { }

                if (Plugins.ContainsKey(Name))
                    Plugins.Remove(Name);

                if (this.State != LoadState.Failed && this.State != LoadState.Crashed)
                    this.State = LoadState.Stoped;

                Parallel.ForEach(this.Dependents, (Plugin P) => P.Dispose());
                Parallel.ForEach(this.MRS, (MethodReplacementState S) => S.Dispose());
            }
        }
    }
    public struct PluginLoadResult
    {
        public LoadError? Error;
        public string Name;
        public PluginVersion Version;
        public uint OliveVersion;
    }
    public enum LoadError
    {
        NoOrInvalidCodeOliveInfoClass,
        IlegalName,
        OliveOutdated,
    }
    public struct PluginVersion
    {
        public static readonly PluginVersion Invalid = new PluginVersion
        {
            Major = 0,
            Minor = 0,
            Build = null,
        };
        public PluginVersion(uint Major, uint Minor, string Build = null)
        {
            this.Major = Major;
            this.Minor = Minor;
            this.Build = Build;
        }

        public uint Major { get; private set; }
        public uint Minor { get; private set; }
        public string Build { get; private set; }

        public static PluginVersion? Parse(String Str)
        {
            // I FUCKING HATE REGEX, so we don't use it here :D

            PluginVersion Return = new PluginVersion
            {
                Major = 0,
                Minor = 0,
                Build = null,
            };
            StringBuilder Buffer = new StringBuilder();
            for (int x = 0; x < Str.Length; x++) // We use a for because its faster then String.IndexOf(...) in this case.
            {
                char C = Str[x];
                switch (C)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        Buffer.Append(C);
                        break;

                    case '.':
                        if (UInt32.TryParse(Buffer.ToString(), out uint Major))
                        {
                            Return.Major = Major;
                            Buffer.Clear();
                            for (++x; x < Str.Length; x++) // We use a for because its faster then String.IndexOf(...) in this case.
                            {
                                C = Str[x];
                                switch (C)
                                {
                                    case '0':
                                    case '1':
                                    case '2':
                                    case '3':
                                    case '4':
                                    case '5':
                                    case '6':
                                    case '7':
                                    case '8':
                                    case '9':
                                        Buffer.Append(C);
                                        break;

                                    case '+':
                                        if (UInt32.TryParse(Buffer.ToString(), out uint Minor))
                                        {
                                            Return.Minor = Minor;
                                            Return.Build = Str.Substring(++x);
                                            return Return;
                                        }
                                        else
                                            return null;

                                    default:
                                        break;
                                }
                            }
                            return Return;
                        }
                        else
                            return null;

                    default:
                        break;
                }
            }
            return null;
        }
    }

    public struct Reflection_Plugin
    {
        public String Name;
        public Version PluginVersion;
    }
    public struct Reflection_Dependency
    {
        public string DependencyName;
        public Reflection_RemoteFunction[] Functions;
    }
    public struct Reflection_RemoteFunction
    {
        public string Class;
        public string FunctionName;
        public MethodInfo OgFunction;
    }
    public enum LoadState
    {
        Awaiting,
        Failed,
        Loaded,
        Initulized,
        Started,
        Crashed,
        Restarting,
        Stoped,
    }

    private static unsafe MethodReplacementState Method_Inject(MethodInfo methodToReplace, MethodInfo methodToInject)
    {
        RuntimeHelpers.PrepareMethod(methodToReplace.MethodHandle);
        RuntimeHelpers.PrepareMethod(methodToInject.MethodHandle);
        MethodReplacementState state;

        IntPtr tar = methodToReplace.MethodHandle.Value;
        if (!methodToReplace.IsVirtual)
            tar += 8;
        else
        {
            Int32 index = (int)(((*(long*)tar) >> 32) & 0xFF);
            IntPtr classStart = *(IntPtr*)(methodToReplace.DeclaringType.TypeHandle.Value + (IntPtr.Size == 4 ? 40 : 64));
            tar = classStart + (IntPtr.Size * index);
        }
        IntPtr inj = methodToInject.MethodHandle.Value + 8;
#if DEBUG
        tar = *(IntPtr*)tar + 1;
        inj = *(IntPtr*)inj + 1;
        state.Location = tar;
        state.OriginalValue = new IntPtr(*(int*)tar);

        *(int*)tar = *(int*)inj + (int)(long)inj - (int)(long)tar;
        return state;
#else
        state.Location = tar;
        state.OriginalValue = * (IntPtr * ) tar;
        *(IntPtr * ) tar = * (IntPtr * ) inj;
        return state;
#endif
    }
    private struct MethodReplacementState : IDisposable
    {
        public IntPtr Location;
        public IntPtr OriginalValue;

        public void Dispose()
        {
            this.Restore();
        }

        public unsafe void Restore()
        {
#if DEBUG
            *(int*)Location = (int)OriginalValue;
#else
            *(IntPtr * ) Location = OriginalValue;
#endif
        }
    }
}
public interface OlivePlugin : IDisposable
{
    string Name { get; }
    CodeOlive.PluginVersion Version { get; }
    Assembly Assembly { get; }
    CodeOlive.LoadState State { get; }

    event OlivePluginUnloadEvent OnUnLoad;
    void Unload();
}
public delegate void OlivePluginUnloadEvent(OlivePlugin Plugin);