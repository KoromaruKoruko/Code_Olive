using System;
using System.Collections.Generic;
using System.Text;

namespace ExamplePlugin
{
    // WARNING: This may be removed and Loading May change to Only Permit Virtual Dependency Declarations.
    //     see: https://github.com/dedady157/Code_Olive/blob/master/doc/PluginCreation


    // WARNING: If you use any Dependencies that are NOT loaded via CodeOlive, you MUST put the dependencies in '<OlivePluginFile>/<PluinName>/<Dependencies>'
    //      this is because DotnetCore requires some hacks to fully load an assembly and it allows for better control incase of one Plugin using one version and another a different version.

    // This MUST be in your Plugin Otherwise it will not be detected
    public static class CodeOliveInfo
    {
        // [NOTE]
        //  Don't change this unless you know what your doing.
        //  This tells the Loader what version IT must be to be able to load this Plugin.
        public const uint CodeOliveVersion = 1;

        // This is the name of the plugin it's used to be found by other Plugins.
        public const string Name = "TestPlugin";

        // NOTE: Build can be removed if it is going to be null anyways
        public const uint Version_Major = 1;
        public const uint Version_Minor = 0;
        //public static string Version_Build = null;

        // NOTE: If you don't like the above you can use this.
        //  See: https://github.com/dedady157/Code_Olive/blob/master/doc/VersionControl
        //public const string Version = "<Major>.<Minor>[+<Build>]";

        // NOTE: THE BELOW FUNCTIONS CAN BE COMMENTED OUT IF NOT USED

        // This is where you Load Whatever data you need
        // This is also where you Load Plugin Functions, As in Functions that can be used
        //  by other Plugins.
        public static void Load()
        {
            // this is called first
        }
        // this is where you Initialize Classes and Objects as well as Figure out what Plugin
        //  Functions You need from other Plugins.
        public static void Init()
        {
            // this is called after Load
        }
        // this is where you Start what ever services you need
        public static void Start()
        {
            // this is called after Init, if all dependencies can be fulfilled
            // if all is successful we shall get "Hello World!" in the Plugin Log, given that the Print Function Exists (Not always the case).
        }


        // Missing Function will be added at a later date, plans are to replace it with a potentially more TypeSafe and Expandable Veriant
    }

    //NOTE: You can Remove the following classes if you don't use them.
    //  see: https://github.com/dedady157/Code_Olive/blob/master/doc/DependencyFunctions
    /// <summary>
    /// Define that this function is a to be loaded from another Plugin,
    /// Only valid for 'public static' methods.
    /// </summary>
    /// <note>
    /// if missing will not thorw an error and the function body will be left
    /// </note>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class OliveSoftDependencyAttribute : Attribute
    {
        public OliveSoftDependencyAttribute(String Dependency, String FunctionName)
        {
            // We don't actually load this data.
            // What we do is Read the Constructor Parameters in Reflection.
        }
    }
    /// <summary>
    /// Define that this function is a to be loaded from another Plugin,
    /// Only valid for 'public static' methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class OliveHardDependencyAttribute : Attribute
    {
        public OliveHardDependencyAttribute(String Dependency, String FunctionName)
        {
            // We don't actually load this data.
            // What we do is Read the Constructor Parameters in Reflection.
        }
    }
}
