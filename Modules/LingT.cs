using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class LingT : Module
    {
        private readonly Random _rand;

        public LingT(SteamNerd steamNerd) : base(steamNerd)
        {
            _rand = new Random();
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            var message = callback.Message.ToLower();
            return message.Contains("ling") && message.Contains("t");
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var stupidMessages = new[] {"selamat pagi", "ling t", "selamat malam"};
            SteamNerd.SendMessage(stupidMessages[_rand.Next(stupidMessages.Length)], callback.ChatRoomID, true);
        }
    }
}
