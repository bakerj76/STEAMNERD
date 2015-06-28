using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SteamKit2;

namespace STEAMNERD
{
    class Program
    {
        static void Main(string[] args)
        {
            var user = args[0];
            var pass = args[1];
            
            var steamClient = new SteamClient();
            var manager = new CallbackManager(steamClient);
            var steamUser = steamClient.GetHandler<SteamUser>();
            
            new Callback<SteamClient.ConnectedCallback>(OnConnect, manager);
            new Callback<SteamClient.DisconnectedCallback>(OnDisconnect, manager);
            
            new Callback<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth, manager);
            
        }
        
        public static void OnConnect(SteamClient.ConnectedCallback callback)
        {
            
        }
        
        public static void OnDisconnect(SteamClient.DisconnectedCallback callback)
        {
            
        }
        
        public static void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            
        }
    }
}
