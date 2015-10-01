using System.Collections.Generic;
using System.Linq;
using SteamKit2;

namespace SteamNerd
{

    public class ChatRoom
    {
        /// <summary>
        /// The SteamID of the chat room.
        /// </summary>
        public SteamID SteamID { get; private set; }

        /// <summary>
        /// The name of the chat room.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The users currently in this chat room.
        /// </summary>
        public List<User> Users { get; private set; }

        /// <summary>
        /// A list of local modules in this chat room.
        /// </summary>
        public List<dynamic> Modules { get; private set; }

        /// <summary>
        /// The current SteamNerd instance.
        /// </summary>
        private SteamNerd _steamNerd;

        /// <summary>
        /// Creates a chat room.
        /// </summary>
        /// <param name="steamNerd">The SteamNerd instance.</param>
        /// <param name="steamID">The SteamID of the chat room.</param>
        /// <param name="name">The name of the chat room.</param>
        public ChatRoom(SteamNerd steamNerd, SteamID steamID, string name)
        {
            _steamNerd = steamNerd;
            SteamID = steamID;
            Name = name;
            Users = new List<User>();
            Modules = new List<dynamic>();
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

        /// <summary>
        /// Adds a user to the chat user list.
        /// </summary>
        /// <param name="user">The user to add.</param>
        public void AddUser(User user)
        {
            Users.Add(user);
        }

        /// <summary>
        /// Removes a user from the chat.
        /// </summary>
        /// <param name="user">The user to remove.</param>
        public void RemoveUser(User user)
        {
            Users.Remove(user);
        }

        /// <summary>
        /// Adds a local module to the chat room.
        /// </summary>
        /// <param name="module">The module to add.</param>
        public void AddModule(Module module)
        {
            Modules.Add(module);
        }

        /// <summary>
        /// Gets both local and global modules.
        /// </summary>
        /// <returns>A list of local and global modules.</returns>
        public IEnumerable<dynamic> GetAllModules()
        {
            return Modules.Union(_steamNerd.ModuleManager.GetModules());
        }
    }
}
