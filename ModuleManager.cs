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
    public class ModuleManager
    {
        public static string ModulesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"SteamNerd\Modules");

        private SteamNerd _steamNerd;
        private FileSystemWatcher _watcher;
        private ScriptEngine _pyEngine;

        private List<string> _localModulePaths;
        private Dictionary<string, Module> _globalModules;
        private Dictionary<SteamID, Dictionary<string, Module>> _chatroomModules;
        private Dictionary<string, Module> _adminModules;

        public ModuleManager(SteamNerd steamNerd)
        {
            _steamNerd = steamNerd;
            _pyEngine = Python.CreateEngine();
            _localModulePaths = new List<string>();
            _globalModules = new Dictionary<string, Module>();
            _chatroomModules = new Dictionary<SteamID, Dictionary<string, Module>>();
            _adminModules = new Dictionary<string, Module>();

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
            var module = new Module(_steamNerd, file);

            var scope = module.Interpret(_steamNerd, _pyEngine);

            if (scope != null && CheckModule(module))
            {  
                AddModule(module);
            }
        }

        /// <summary>
        /// Checks the module to see if it has everything that SteamNerd needs
        /// for a module.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        private bool CheckModule(Module module)
        {
            var fileName = Path.GetFileName(module.Path);

            // If it doesn't have a name, it's probably not a module.
            if (module.Name == null)
            {
                return false;
            }

            return true;
        }

        public void AddModule(Module module)
        {
            if (module.Global)
            {
                _globalModules[module.Name] = module;

                // Run start on global modules whenever they're added.
                module.OnStart();
            }
            else
            {
                _localModulePaths.Add(module.Path);

                foreach (var chatroom in _chatroomModules.Keys)
                {
                    var chatModule = AddModuleToChatroom(module.Path, chatroom);
                    chatModule.OnStart();
                }
            }
        }

        public void AddChatroom(SteamID chatroom)
        {
            _chatroomModules[chatroom] = new Dictionary<string, Module>();

            // Add every local module to this chat.
            foreach (var path in _localModulePaths)
            {
                AddModuleToChatroom(path, chatroom);
            }

            // Run start on each local module.
            foreach (var module in _chatroomModules[chatroom].Values)
            {
                module.OnStart();
            }
        }

        private Module AddModuleToChatroom(string path, SteamID chatroom)
        {
            var module = new Module(_steamNerd, path);
            module.Chatroom = chatroom;
            module.Interpret(_steamNerd, _pyEngine);
            _chatroomModules[chatroom][module.Name] = module;

            return module;
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
            // Find in globals.
            if (_globalModules.ContainsKey(moduleName))
            {
                return _globalModules[moduleName].Variables;
            }

            // Find in locals.
            if (chatroom != null)
            {
                if (_chatroomModules.ContainsKey(chatroom))
                {
                    if (_chatroomModules[chatroom].ContainsKey(moduleName))
                    {
                        return _chatroomModules[chatroom][moduleName].Variables;
                    }
                }
            }

            return null;
        }

        public dynamic[] GetModules(SteamID chatroom = null)
        {
            var modules = _globalModules.Select(moduleKV => moduleKV.Value.Variables).ToList();

            if (chatroom != null && _chatroomModules.ContainsKey(chatroom))
            {
                modules.AddRange(_chatroomModules[chatroom].Select(moduleKV => moduleKV.Value.Variables));
            }

            return modules.ToArray();
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
        /// <param name="chatRoom"></param>
        /// <param name="user"></param>
        /// <param name="callback"></param>
        public void EnteredChat(
            ChatRoom chatRoom, 
            User user, 
            SteamFriends.PersonaStateCallback callback
        )
        {
            foreach (var module in chatRoom.GetAllModules())
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
            // Make OnChanged "atomic," so we don't get two OnChanged events.
            _watcher.EnableRaisingEvents = false;

            Console.WriteLine("{0} changed.", e.Name);

            var modules = _globalModules.Values.ToList();

            // Sleep to avoid reading while text editor is writing (?).
            Thread.Sleep(100);

            RemoveModule(e.FullPath);
            CreateModule(e.FullPath);

            _watcher.EnableRaisingEvents = true;
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
