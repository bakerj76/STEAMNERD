using System.Collections.Generic;
using SteamKit2;

namespace SteamNerd
{

    public class ChatRoom
    {
        /// <summary>
        /// The SteamID of the ChatRoom.
        /// </summary>
        public SteamID SteamID { get; private set; }

        /// <summary>
        /// The name of the ChatRoom.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The SteamIDs of users currently in this ChatRoom.
        /// </summary>
        public List<SteamID> Chatters { get; private set; }

        /// <summary>
        /// The users currently in this ChatRoom.
        /// </summary>
        public List<User> Users { get; private set; }

        private SteamNerd _steamNerd;

        public ChatRoom(SteamNerd steamNerd, SteamID steamID, string name)
        {
            _steamNerd = steamNerd;
            SteamID = steamID;
            Name = name;

            Chatters = new List<SteamID>();
            Users = new List<User>();
        }

        /// <summary>
        /// Joins the chat room.
        /// </summary>
        public void Join()
        {
            _steamNerd.SteamFriends.JoinChat(SteamID);
        }

        /// <summary>
        /// Invites a user to the chat room.
        /// </summary>
        /// <param name="invitedUser">The invited user.</param>
        public void InviteUser(User invitedUser)
        {
            _steamNerd.SteamFriends.InviteUserToChat(invitedUser.SteamID, SteamID);
        }

        /// <summary>
        /// Invites a user to the chat room.
        /// </summary>
        /// <param name="invitedUser">The SteamID of the invited user.</param>
        public void InviteUser(SteamID invitedUser)
        {
            _steamNerd.SteamFriends.InviteUserToChat(invitedUser, SteamID);
        }
    }
}
