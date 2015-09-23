using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;

namespace SteamNerd
{
    class AdminManager
    {
        private string _adminListPath;
        public List<SteamID> Admins;

        public void AddAdmin(User user)
        {
            user.IsAdmin = true;
            Admins.Add(user.SteamID);
        }

        public void Save()
        {

        }

        public void Load(string adminListPath)
        {
            _adminListPath = adminListPath;
        }
    }
}
