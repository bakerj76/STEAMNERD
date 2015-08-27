using System.Collections.Generic;
using SteamKit2;

namespace STEAMNERD
{
    public abstract class Module
    {
        protected SteamNerd SteamNerd;

        public string Name;
        public string Description;
        public List<Command> Commands;
        public delegate void CommandCallback(SteamFriends.ChatMsgCallback callback);

        public struct Command
        {
            public string Name;
            public string Help;
            public CommandCallback Callback;
        }

        protected Module(SteamNerd steamNerd)
        {
            SteamNerd = steamNerd;
        }

        public void Help(SteamID chatRoom)
        {
            var message1 = string.Format("{0}\n{1}", Name, Description);
            var message2 = ".\n";

            foreach (var command in Commands)
            {
                message2 += string.Format("{0,-15}- {1}", command.Name, command.Help);
            }

            SteamNerd.SendMessage(message1, chatRoom, true);
            SteamNerd.SendMessage(message2, chatRoom, true);
        }

        /// <summary>
        /// When the bot enters the chat
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnSelfChatEnter(SteamFriends.ChatEnterCallback callback) { }
        /// <summary>
        /// When someone enters the chat
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnFriendChatEnter(SteamFriends.PersonaStateCallback callback) { }
        /// <summary>
        /// When someone leaves the chat
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnFriendChatLeave(SteamFriends.ChatMemberInfoCallback callback) { }

        /// <summary>
        /// Used for determining if OnChatMsg should run based on the content in the message
        /// </summary>
        /// <param name="callback"></param>
        /// <returns>If the module should run</returns>
        public abstract bool Match(SteamFriends.ChatMsgCallback callback);

        /// <summary>
        /// When the bot gets a personal message
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnFriendMsg(SteamFriends.FriendMsgCallback callback) { }
        /// <summary>
        /// When the chatroom gets a message and it is matched
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnChatMsg(SteamFriends.ChatMsgCallback callback) { }
    }
}
