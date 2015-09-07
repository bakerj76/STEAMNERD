using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SteamKit2;
using Microsoft.Scripting.Hosting;
using IronPython.Hosting;
using IronPython.Runtime.Operations;

namespace SteamNerd
{
    class ModuleManager
    {
        public static string ModulesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"SteamNerd\Modules");

        private SteamNerd _steamNerd;
        private FileSystemWatcher _watcher;
        private ScriptEngine _pyEngine;

        private List<string> _localModulePaths;
        private Dictionary<string, PyModule> _globalModules;
        private Dictionary<SteamID, Dictionary<string, PyModule>> _chatroomModules;

        public ModuleManager(SteamNerd steamNerd)
        {
            _steamNerd = steamNerd;
            _pyEngine = Python.CreateEngine();
            _localModulePaths = new List<string>();
            _globalModules = new Dictionary<string, PyModule>();
            _chatroomModules = new Dictionary<SteamID, Dictionary<string, PyModule>>();

            InterpretDirectory();

            _watcher = new FileSystemWatcher(ModulesDirectory);
            _watcher.Filter = "*.py";
            _watcher.NotifyFilter = NotifyFilters.LastAccess |
                NotifyFilters.LastWrite| 
                NotifyFilters.FileName | 
                NotifyFilters.DirectoryName;

            _watcher.Created += OnCreated;
            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnDelete;

            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Prints a Python-esque stack frame.
        /// </summary>
        /// <param name="e">The Python exception</param>
        public static void PrintStackFrame(Exception e)
        {
            var pyStackFrame = PythonOps.GetDynamicStackFrames(e);

            Console.WriteLine("Traceback (most recent call last):");

            foreach (var frame in pyStackFrame)
            {
                Console.WriteLine("  File \"{0}\", line {1}, in {2}", frame.GetFileName(), frame.GetFileLineNumber(), frame.GetMethodName());
            }

            Console.WriteLine("{0}: {1}", e.GetType().Name, e.Message);
        }

        /// <summary>
        /// Interpret all of the modules in the directory.
        /// </summary>
        private void InterpretDirectory()
        {
            foreach (var file in  Directory.EnumerateFiles(ModulesDirectory, "*.py"))
            {
                CreateModule(file);
            }
        }

        private void CreateModule(string file)
        {
            var module = new PyModule(file);

            var scope = module.Interpret(_steamNerd, _pyEngine);

            if (scope != null && CheckModule(module))
            {
                module.Variables = scope.GetVariable("var");
                AddModule(module);
            }
        }

        /// <summary>
        /// Checks the module to see if it has everything that SteamNerd needs
        /// for a module.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        private bool CheckModule(PyModule module)
        {
            var fileName = Path.GetFileName(module.Path);

            if (module.Name == null)
            {
                Console.WriteLine("{0} has no module name!", fileName);
                return false;
            }

            return true;
        }

        public void AddModule(PyModule module)
        {
            if (module.Global)
            {
                _globalModules[module.Name] = module;
            }
            else
            {
                _localModulePaths.Add(module.Path);

                foreach (var chatroom in _chatroomModules.Keys)
                {
                    AddModuleToChatroom(module.Path, chatroom);
                }
            }
        }

        public void AddChatroom(SteamID chatroom)
        {
            _chatroomModules[chatroom] = new Dictionary<string, PyModule>();

            foreach (var path in _localModulePaths)
            {
                AddModuleToChatroom(path, chatroom);
            }
        }

        private void AddModuleToChatroom(string path, SteamID chatroom)
        {
            var module = new PyModule(path);
            module.Chatroom = chatroom;
            module.Interpret(_steamNerd, _pyEngine);
            _chatroomModules[chatroom][module.Name] = module;
        }

        /// <summary>
        /// Get a module's properties by it's name and chatroom.
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="chatroom">
        /// The chatroom where the instance of the module exists. If it's null, 
        /// it searches the global modules.
        /// </param>
        /// <returns></returns>
        public dynamic GetModule(string moduleName, SteamID chatroom = null)
        {
            if (chatroom == null)
            {
                if (_globalModules.ContainsKey(moduleName))
                {
                    return _globalModules[moduleName];
                }
            }
            else
            {
                if (_chatroomModules.ContainsKey(chatroom))
                {
                    if (_chatroomModules[chatroom].ContainsKey(moduleName))
                    {
                        return _chatroomModules[chatroom][moduleName];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// When a chat message is received, signal the modules. Also checks
        /// against every module's commands.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void ChatMessageSent(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            // Get both the global modules and the modules specific to that chatroom.
            var modules = _globalModules.Values.ToList();
            modules.AddRange(_chatroomModules[callback.ChatRoomID].Values);

            foreach (var module in modules)
            {
                // Find the command
                var command = module.FindCommand(args);

                // If FindCommand returned something, execute it.
                if (command.HasValue)
                {
                    try
                    {
                        command.Value.Callback(callback, args);
                    }
                    catch (Exception e)
                    {
                        PrintStackFrame(e);
                    }
                }
            }

            foreach (var module in modules)
            {
                module.OnChatMessage(callback, args);
            }
        }

        /// <summary>
        /// When a private message is received, signal the modules.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void FriendMessageSent(SteamFriends.FriendMsgCallback callback, string[] args)
        {
            var modules = _globalModules.Values.ToList();

            // Get all modules
            foreach (var chatKV in _chatroomModules)
            {
                modules.AddRange(chatKV.Value.Values);
            }

            foreach (var module in modules)
            {
                module.OnFriendMessage(callback, args);
            }
        }

        /// <summary>
        /// When a chat is entered by the bot, signal the modules.
        /// </summary>
        /// <param name="callback"></param>
        public void SelfEnteredChat(SteamFriends.ChatEnterCallback callback)
        {
            // Get both the global modules and the modules specific to that chatroom.
            var modules = _globalModules.Values.Union(_chatroomModules[callback.ChatID].Values);

            foreach (var module in modules)
            {
                module.OnSelfChatEnter(callback);
            }
        }

        /// <summary>
        /// When someone other than the bot enters the chat, signal the 
        /// modules.
        /// </summary>
        /// <param name="callback"></param>
        public void EnteredChat(SteamFriends.PersonaStateCallback callback)
        {
            // Get both the global modules and the modules specific to that chatroom.
            var modules = _globalModules.Values.Union(_chatroomModules[callback.SourceSteamID].Values);

            foreach (var module in modules)
            {
                module.OnChatEnter(callback);
            }
        }

        /// <summary>
        /// When someone leaves the chat, signal the modules.
        /// </summary>
        /// <param name="callback"></param>
        public void LeftChat(SteamFriends.ChatMemberInfoCallback callback)
        {
            // Get both the global modules and the modules specific to that chatroom.
            var modules = _globalModules.Values.Union(_chatroomModules[callback.ChatRoomID].Values);

            foreach (var module in modules)
            {
                module.OnChatLeave(callback);
            }
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} created.", e.Name);
            CreateModule(e.FullPath);
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} changed.", e.Name);

            var modules = _globalModules.Values.ToList();

            // Sleep to avoid reading while text editor is writing.
            Thread.Sleep(100);

            RemoveModule(e.FullPath);
            CreateModule(e.FullPath);
        }

        private void OnDelete(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} deleted.", e.Name);
            RemoveModule(e.FullPath);
        }

        private void RemoveModule(string path)
        {
            // Remove from global modules.
            var globalRemoval = new List<string>();

            foreach (var module in _globalModules.Values)
            {
                if (module.Path == path)
                {
                    globalRemoval.Add(module.Name);
                }
            }

            foreach (var name in globalRemoval)
            {
                _globalModules.Remove(name);
            }


            // Remove from local modules.
            var localRemoval = new List<string>();

            foreach (var chatKV in _chatroomModules)
            {
                // Get the dictionary of modules in this chat
                foreach (var moduleKV in chatKV.Value)
                {
                    if (moduleKV.Value.Path == path)
                    {
                        localRemoval.Add(moduleKV.Value.Name);
                    }
                }
            }

            foreach (var chatKV in _chatroomModules)
            {
                foreach (var name in localRemoval)
                {
                    chatKV.Value.Remove(name);
                }
            }
        }
    }
}
