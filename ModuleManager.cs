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

        //private List<Module> _modules;

        public ModuleManager(SteamNerd steamNerd)
        {
            _steamNerd = steamNerd;
            _pyEngine = Python.CreateEngine();

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

            Console.ReadKey();
        }

        /// <summary>
        /// 'Compiles' all of the modules in the directory.
        /// </summary>
        private void CompileDirectory()
        {
            foreach (var file in  Directory.EnumerateFiles(ModulesDirectory))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var scope = _pyEngine.CreateScope();
                var module = new PyModule();

                scope.SetVariable("SteamNerd", _steamNerd);
                scope.SetVariable("Module", module);

                try
                {
                    _pyEngine.ExecuteFile(file, scope);
                }
                catch (Exception e)
                {
                    PrintStackFrame(e);
                }

                Action<SteamFriends.ChatMsgCallback, string[]> onChatMessage;
                Action<SteamFriends.FriendMsgCallback, string[]> onFriendMessage;
                Action<SteamFriends.ChatEnterCallback> onSelfJoinChat;
                Action<SteamFriends.PersonaStateCallback> onJoinChat;
                Action<SteamFriends.ChatMemberInfoCallback> onLeaveChat;

                if (scope.TryGetVariable("onChatMessage", out onChatMessage))
                {
                    module.OnChatMessage = (callback, args) => 
                        onChatMessage(callback, args);
                }

                if (scope.TryGetVariable("onFriendMessage", out onFriendMessage))
                {
                    module.OnFriendMessage = (callback, args) =>
                        onFriendMessage(callback, args);
                }

                if (scope.TryGetVariable("onSelfJoinChat", out onSelfJoinChat))
                {
                    module.OnSelfJoinChat = (callback) =>
                        onSelfJoinChat(callback);
                }

                if (scope.TryGetVariable("onJoinChat", out onJoinChat))
                {
                    module.OnJoinChat = (callback) =>
                        onJoinChat(callback);
                }

                if (scope.TryGetVariable("onLeaveChat", out onLeaveChat))
                {
                    module.OnLeaveChat = (callback) =>
                        onLeaveChat(callback);
                }
            }
        }

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

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} changed or created.", e.Name);
        }

        private void OnDelete(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} deleted.", e.Name);
        }
    }
}
