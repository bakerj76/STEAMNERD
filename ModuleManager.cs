using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private List<PyModule> _modules;
        private Dictionary<string, PyModule> _modulesByName;

        public ModuleManager(SteamNerd steamNerd)
        {
            _steamNerd = steamNerd;
            _pyEngine = Python.CreateEngine();
            _modules = new List<PyModule>();
            _modulesByName = new Dictionary<string, PyModule>();

            CompileDirectory();

            _watcher = new FileSystemWatcher(ModulesDirectory);
            _watcher.Filter = "*.py";
            _watcher.NotifyFilter = NotifyFilters.LastAccess |
                NotifyFilters.LastWrite| 
                NotifyFilters.FileName | 
                NotifyFilters.DirectoryName;

            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnDelete;

            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Prints a Python-esque stack frame.
        /// </summary>
        /// <param name="e">The Python exception</param>
        public static void PrintStackFrame(Exception e)
        {
            //var eo = pyEngine.GetService<ExceptionOperations>();
            //Console.WriteLine(eo.FormatException(e));

            var pyStackFrame = PythonOps.GetDynamicStackFrames(e);

            Console.WriteLine("Traceback (most recent call last):");

            foreach (var frame in pyStackFrame)
            {
                Console.WriteLine("  File \"{0}\", line {1}, in {2}", frame.GetFileName(), frame.GetFileLineNumber(), frame.GetMethodName());
            }

            Console.WriteLine("{0}: {1}", e.GetType().Name, e.Message);
        }

        /// <summary>
        /// 'Compiles' all of the modules in the directory.
        /// </summary>
        private void CompileDirectory()
        {
            foreach (var file in  Directory.EnumerateFiles(ModulesDirectory))
            {
                var module = new PyModule(file);

                var scope = module.Compile(_steamNerd, _pyEngine);

                if (scope != null && CheckModule(module))
                {
                    module.Variables = scope.GetVariable("var");
                    AddModule(module);
                }
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

        /// <summary>
        /// Get a module's properties by it's name.
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        public dynamic GetModule(string moduleName)
        {
            return _modulesByName.ContainsKey(moduleName) ? 
                _modulesByName[moduleName].Variables : null;
        }

        /// <summary>
        /// When a chat message is received, signal the modules. Also checks
        /// against every module's commands.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void ChatMessageSent(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            foreach (var module in _modules)
            {
                var command = module.FindCommand(args);

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

            foreach (var module in _modules)
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
            foreach (var module in _modules)
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
            foreach (var module in _modules)
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
            foreach (var module in _modules)
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
            foreach (var module in _modules)
            {
                module.OnChatLeave(callback);
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} changed.", e.Name);

            //foreach (var module in _modules)
            //{
            //    if (module.Path == e.FullPath)
            //    {
            //        module.Compile(_steamNerd, _pyEngine);
            //    }
            //}
        }

        private void OnDelete(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} deleted.", e.Name);
        }

        public void AddModule(PyModule module)
        {
            _modules.Add(module);
            _modulesByName[module.Name] = module;
        }
    }
}
