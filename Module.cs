using SteamKit2;

namespace STEAMNERD
{
    public abstract class Module
    {
        // TODO: Change this into Singleton
        protected SteamNerd SteamNerd;

        protected Module(SteamNerd steamNerd)
        {
            SteamNerd = steamNerd;
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
