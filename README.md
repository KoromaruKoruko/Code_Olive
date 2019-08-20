# Code_Olive
Code_Olive .Net Plugins for the future.


What is Code_Olive?
Code Olive is an advanced Plugin Loader witch aims to keep the benefits of Isolation via AppDomain but still enable advanced modding support such that mods could rewrite most of a game or program but in a way that will allow the game to fully control what can and can't be changed. Another aim is to keep things fast and simple, so instead of the usual interface fuckery that one has to do to get a plugin system we use reflection and to keep it as fast as possible we use some optimizer tricks such as method injection witch works much like dependency injection but without any of that hacky looking junk to go with it.

How does it work?
Code olive consists of 2 files. one that goes inside your game/program and one that goes inside the Plugin/Mod. The Olive Loader.cs file goes in the Program/game witch will load the plugins where the game/program will hand over all the supposed plugin files to the CodeOlive.LoadPlugin(String PluginPath) function witch will read the plugin and begin loading it, once all plugins are pased to the Loader it will wait on the game/Program to call CodeOlive.Start() to witch it will finnalize the plugins and unload any with missing dependencies and then "start" all the plugins. The Plugin Side of things consists of a single class witch tells the loader what the plugin is, most of this file can be removed for compactness as the loader never actualy toutches anything that isn't there, some of it however MUST be there such as the PluginName, CodeOliveVersion, and Version Info (see the plugin file to see what variables to keep). The Plugin File also contains any Attributes that are needed by the Loader to find and reslove dependency and external functions. See: ./doc/DependencyFunctions

This is also rather Light weight needing only an extra Microsoft Dependency 'System.Reflection.Loader' this is to restrict external dependencies into the Plugin Folder as well as supporting .Net Core Isolation / Execution Restrictions
