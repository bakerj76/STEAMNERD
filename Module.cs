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

        public virtual void OnChatEnter(SteamFriends.ChatEnterCallback callback) { }
        public abstract bool Match(SteamFriends.ChatMsgCallback callback);
        public virtual void OnFriendMsg(SteamFriends.FriendMsgCallback callback) { }
        public virtual void OnChatMsg(SteamFriends.ChatMsgCallback callback) { }
    }
}
