using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Scripting.Hosting;
using IronPython.Hosting;

namespace SteamNerd.Modules
{
    class ModuleManager
    {
        public static string ModulesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"SteamNerd\Modules");

        private FileSystemWatcher _watcher;
        private ScriptEngine _pyEngine;

        //private List<Module> _modules;

        public ModuleManager()
        {
            _pyEngine = Python.CreateEngine();

            CompileDirectory();

            _watcher = new FileSystemWatcher(ModulesDirectory);
            _watcher.NotifyFilter = NotifyFilters.LastAccess |
                NotifyFilters.LastWrite| 
                NotifyFilters.FileName | 
                NotifyFilters.DirectoryName;
            _watcher.Filter = "*.py";

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
                var scope = _pyEngine.ExecuteFile(file);

                var moduleType = scope.GetVariable(fileName);
                var module = _pyEngine.Operations.CreateInstance(moduleType);
            }
        }

        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} changed or created.", e.Name);
        }

        private static void OnDelete(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} deleted.", e.Name);
        }
    }
}
