using System.Collections.Generic;
using SteamKit2;

namespace STEAMNERD
{
    public abstract class Module
    {
        protected SteamNerd SteamNerd;

        /// <summary>
        /// The name of the module.
        /// </summary>
        public string Name;

        /// <summary>
        /// What does the module do?
        /// </summary>
        public string Description;

        /// <summary>
        /// The list of commands associated with this module.
        /// </summary>
        public List<Command> Commands;

        /// <summary>
        /// A callback delegate for commands
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public delegate void CommandCallback(SteamFriends.ChatMsgCallback callback, string[] args);

        /// <summary>
        /// An array of matching strings, a description describing what the 
        /// command does, a callback to the action, and the minimum number
        /// of arguments to match this command.
        /// </summary>
        public struct Command
        {
            public string[] Match;
            public string Description;
            public CommandCallback Callback;
            public int MinimumArguments;
        }


        /// <summary>
        /// Module constructor
        /// </summary>
        /// <param name="steamNerd">The running instance of SteamNerd</param>
        protected Module(SteamNerd steamNerd)
        {
            SteamNerd = steamNerd;
            Commands = new List<Command>();
        }

        /// <summary>
        /// Adds a command to the module.
        /// </summary>
        /// <param name="match">
        /// The string that matches the first argument.
        /// </param>
        /// <param name="help">
        /// A message explaining what the command does (used for Help).
        /// </param>
        /// <param name="callback">
        /// The function to call when a chatter types the command.
        /// </param>
        public void AddCommand(string match, string help, CommandCallback callback, int minimumArguments = 0)
        {
            Commands.Add(new Command
            {
                Match = new[] { match },
                Description = help,
                Callback = callback,
                MinimumArguments = minimumArguments
            });
        }

        /// <summary>
        /// Adds a command to the module.
        /// </summary>
        /// <param name="match">
        /// A list of strings that match the arguments.
        /// </param>
        /// <param name="help">
        /// A message explaining what the command does (used for Help).
        /// </param>
        /// <param name="callback">
        /// The function to call when a chatter types the command.
        /// </param>
        public void AddCommand(string[] match, string help, CommandCallback callback, int minimumArguments = 0)
        {
            Commands.Add(new Command
            {
                Match = match,
                Description = help,
                Callback = callback,
                MinimumArguments = minimumArguments
            });
        }

        /// <summary>
        /// Calls the callback on a command if it's matched.
        /// </summary>
        /// <param name="callback">
        /// The callback that fired this check.
        /// </param>
        /// <param name="args">
        /// What the user typed, in a space-split array.
        /// </param>
        public void RunCommand(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            foreach (var command in Commands)
            {
                // If there's no callback, don't check the command, or if the
                // input doesn't meet the minimum number of arguments, then 
                // skip it.
                if (command.Callback == null || args.Length < command.MinimumArguments) continue;

                // The command with the beginning command character
                var realCommandString = SteamNerd.CommandChar + command.Match[0];
                var match = true;

                // If the command doesn't have a match, then it matches everything.
                if (command.Match == null || command.Match[0] == "" || 
                    realCommandString == args[0])
                {
                    // Check all of the match strings
                    for (var i = 1; i < command.Match.Length; i++)
                    {
                        if (args[i] != command.Match[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        command.Callback(callback, args);
                    }
                }
            }
        }

        /// <summary>
        /// When the bot enters the chat.
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnSelfChatEnter(SteamFriends.ChatEnterCallback callback) { }

        /// <summary>
        /// When someone enters the chat.
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnFriendChatEnter(SteamFriends.PersonaStateCallback callback) { }
        
        /// <summary>
        /// When someone leaves the chat.
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnFriendChatLeave(SteamFriends.ChatMemberInfoCallback callback) { }

        /// <summary>
        /// Used for determining if OnChatMsg should run based on the content 
        /// in the message.
        /// </summary>
        /// <param name="callback"></param>
        /// <returns>If the module should run</returns>
        public virtual bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return false;
        }

        /// <summary>
        /// When the bot gets a personal message.
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnFriendMsg(SteamFriends.FriendMsgCallback callback) { }
        
        /// <summary>
        /// When the chatroom gets a message and it is matched.
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnChatMsg(SteamFriends.ChatMsgCallback callback) { }
    }
}
