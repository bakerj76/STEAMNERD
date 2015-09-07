using System.Linq;
using SteamKit2;

namespace SteamNerd.Modules
{
    class Party : Module
    {
        public Party(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Party";
            Description = "Let's party";

            AddCommand(
                "party",
                "Invites everyone for a party.",
                LetsParty
            );
        }

        public void LetsParty(SteamFriends.ChatMsgCallback callback, string[] args)
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

            SteamNerd.SendMessage(string.Format("INVITING {0} IDIOTS TO THE CHAT.", inviteCount), callback.ChatRoomID);
        }
    }
}
