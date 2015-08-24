using System;
using System.Collections.Generic;
using System.Linq;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class TrollSlayer : Module
    {
        private bool _playing;
        private Random _rand;
        private List<SteamID> _chatters;

        public TrollSlayer(SteamNerd steamNerd) : base(steamNerd)
        {
            _playing = false;
            _rand = new Random();
            _chatters = new List<SteamID>();
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return callback.Message.ToLower() == "!slaytroll";
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var numKeys = SteamNerd.Chatters.Keys.Count;
            var troll = SteamNerd.Chatters.Keys.ToArray()[_rand.Next(numKeys)];

            SteamNerd.SendMessage(string.Format("SLAYING TROLL: {0}", SteamNerd.Chatters[troll]), callback.ChatRoomID, true);
            
            SteamNerd.SteamFriends.KickChatMember(callback.ChatRoomID, troll);
        }
    }
}
