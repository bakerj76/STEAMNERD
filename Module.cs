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
        public delegate void CommandCallback(SteamFriends.ChatMsgCallback callback, string[] args);

        public struct Command
        {
            public string Match;
            public string Description;
            public CommandCallback Callback;
        }

        protected Module(SteamNerd steamNerd)
        {
            SteamNerd = steamNerd;
            Commands = new List<Command>();
        }

        public void AddCommand(string match, string help, CommandCallback callback)
        {
            Commands.Add(new Command
            {
                Match = match,
                Description = help,
                Callback = callback
            });
        }

        public void RunCommand(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            foreach (var command in Commands)
            {
                var realCommandString = "." + command.Match;
                if (command.Match == null || command.Match == "" || realCommandString == args[0])
                {
                    if (command.Callback != null)
                    {
                        command.Callback(callback, args);
                    }
                }
            }
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
        public virtual bool Match(SteamFriends.ChatMsgCallback callback) { return false; }

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
