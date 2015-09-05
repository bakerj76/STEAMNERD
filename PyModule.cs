using System;
using System.Collections.Generic;
using System.Linq;
using SteamKit2;

namespace SteamNerd
{
    public class PyModule
    {
        public struct Command
        {
            public string[] Match;
            public string Description;
            public CommandCallback Callback;
        }

        public delegate void CommandCallback(SteamFriends.ChatMsgCallback callback, string[] args);
        public delegate void ChatMessage(SteamFriends.ChatMsgCallback callback, string[] args);
        public delegate void FriendMessage(SteamFriends.FriendMsgCallback callback, string[] args);
        public delegate void SelfJoinChat(SteamFriends.ChatEnterCallback callback);
        public delegate void JoinChat(SteamFriends.PersonaStateCallback callback);
        public delegate void LeaveChat(SteamFriends.ChatMemberInfoCallback callback);
        
        public ChatMessage OnChatMessageCallback;
        public FriendMessage OnFriendMessageCallback;
        public SelfJoinChat OnSelfChatEnterCallback;
        public JoinChat OnChatEnterCallback;
        public LeaveChat OnChatLeaveCallback;

        public virtual string Name { get; set; }
        public virtual string Description { get; set; }
        public string Path { get; }
        public dynamic Variables { get; set; }

        protected List<Command> Commands;

        public PyModule(string path)
        {
            Commands = new List<Command>();
            Path = path;
        }

        public void AddCommand(string match, string help, CommandCallback callback)
        {
            Commands.Add(new Command
            {
                Match = new[] { match },
                Description = help,
                Callback = callback
            });
        }

        public void AddCommand(string[] match, string help, CommandCallback callback)
        {
            Commands.Add(new Command
            {
                Match = match,
                Description = help,
                Callback = callback
            });
        }

        public Command? FindCommand(string[] args)
        {
            Command? candidate = null;
            var best = 0;

            foreach (var command in Commands)
            {
                // If the command doesn't have a callback, match, or
                // if it matches more than the current arguments, it's 
                // probably not a match.
                if (command.Callback == null || 
                    command.Match == null ||
                    command.Match.Length > args.Length) continue;

                var keepMatching = true;
                var matched = 0;
                var completeCommand = SteamNerd.CommandChar + command.Match[0];

                if (args[0] == completeCommand)
                {
                    matched++;

                    for (var i = 1; i < args.Length; i++)
                    {
                        if (args[i] == command.Match[i])
                        {
                            matched++;
                        }
                        else
                        {
                            keepMatching = false;
                            break;
                        }
                    }

                    if (keepMatching && matched > best)
                    {
                        candidate = command;
                        best = matched;
                    }
                }
            }

            return candidate;
        }

        public void OnChatMessage(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (OnChatMessageCallback != null)
            {
                OnChatMessageCallback(callback, args);
            }
        }

        public void OnFriendMessage(SteamFriends.FriendMsgCallback callback, string[] args)
        {
            if (OnFriendMessageCallback != null)
            {
                OnFriendMessageCallback(callback, args);
            }
        }

        public void OnSelfChatEnter(SteamFriends.ChatEnterCallback callback)
        {
            if (OnSelfChatEnterCallback != null)
            {
                OnSelfChatEnterCallback(callback);
            }
        }

        public void OnChatEnter(SteamFriends.PersonaStateCallback callback)
        {
            if (OnChatEnterCallback != null)
            {
                OnChatEnterCallback(callback);
            }
        }

        public void OnChatLeave(SteamFriends.ChatMemberInfoCallback callback)
        {
            if (OnChatLeaveCallback != null)
            {
                OnChatLeaveCallback(callback);
            }
        }
    }
}
