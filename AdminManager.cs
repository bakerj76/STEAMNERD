using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamNerd
{
    class AdminManager
    {
        public List<Module> AdminModules;
        // public List<User> Admins;

        public void CheckCommand(string[] args)
        {

        }

        public void AddModule(Module module)
        {
            AdminModules.Add(module);
        }
    }
}
