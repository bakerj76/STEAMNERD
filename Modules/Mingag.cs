using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Mingag : Module
    {
        public Mingag(SteamNerd steamNerd) : base(steamNerd)
        {

        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return callback.Message.ToLower().Contains("mingag");
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            Console.WriteLine("Sending mingag a message");

            var mingag = new SteamID("STEAM_0:0:5153026");

            SteamNerd.SteamFriends.SendChatMessage(mingag, EChatEntryType.ChatMsg,
                string.Format("{0}: {1}", SteamNerd.ChatterNames[callback.ChatterID], callback.Message));
        }
    }
}