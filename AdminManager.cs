using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="adminListPath">The file that contains the admin list.</param>
        public AdminManager(string adminListPath)
        {
            _adminListPath = adminListPath;
        }

        /// <summary>
        /// Adds an admin and gives them permissions.
        /// </summary>
        /// <param name="user">The user to make admin.</param>
        public void AddAdmin(User user)
        {
            user.IsAdmin = true;
            Admins.Add(user.SteamID);
        }

        /// <summary>
        /// Saves the list of admins.
        /// </summary>
        public void Save()
        {
            using (var file = new StreamWriter(_adminListPath))
            {
                foreach (var admin in Admins)
                {
                    file.Write(admin.Render() + "\r\n");
                }
            }
        }

        /// <summary>
        /// Loads the list of admins.
        /// </summary>
        /// <param name="adminListPath"></param>
        public void Load(string adminListPath)
        {
            using (var file = new StreamReader(_adminListPath))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();

                    var steamID = new SteamID(line);
                    Admins.Add(steamID);
                }
            }
        }
    }
}
