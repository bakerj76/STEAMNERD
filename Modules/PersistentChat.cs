using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;

namespace SteamNerd.Modules
{

    class PersistentChat : Module
    {
        public PersistentChat(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Persistent Chat";
            Description = "Message xXxTrollSlayerxXx to get invited to the chat.";
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return true;
        }

        public override void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            if (SteamNerd.CurrentChatRoom != null)
            {
                SteamNerd.SteamFriends.InviteUserToChat(callback.Sender, SteamNerd.CurrentChatRoom);
            }
        }
    }
}
