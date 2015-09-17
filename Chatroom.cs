using System.Collections.Generic;
using SteamKit2;

namespace SteamNerd
{

    public class ChatRoom
    {
        public SteamID SteamID;
        public string Name { get; set; }
        public List<SteamID> Chatters { get; private set; }

        public ChatRoom(string name)
        {
            Name = name;
            Chatters = new List<SteamID>();
        }
    }
}
