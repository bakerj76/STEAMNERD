using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Party : Module
    {
        public Party(SteamNerd steamNerd) : base(steamNerd)
        {

        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return callback.Message.ToLower() == "!party";
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var steamFriends = SteamNerd.SteamFriends;
            var inviteCount = 0;

            for (var i = 0; i < steamFriends.GetFriendCount(); i++)
            {
                var friend = steamFriends.GetFriendByIndex(i);

                if (!SteamNerd.ChatterNames.Keys.Contains(friend))
                {
                    steamFriends.InviteUserToChat(friend, callback.ChatRoomID);
                    inviteCount++;
                }
            }

            SteamNerd.SendMessage(string.Format("INVITING {0} IDIOTS TO THE CHAT.", inviteCount), callback.ChatRoomID, true);
        }
    }
}
