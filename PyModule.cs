using System;
using System.Collections.Generic;
using System.Linq;
using SteamKit2;
using Microsoft.Scripting.Hosting;

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
        
        private ChatMessage OnChatMessageCallback;
        private FriendMessage OnFriendMessageCallback;
        private SelfJoinChat OnSelfChatEnterCallback;
        private JoinChat OnChatEnterCallback;
        private LeaveChat OnChatLeaveCallback;

        protected List<Command> Commands;

        public virtual string Name { get; set; }
        public virtual string Description { get; set; }
        public virtual bool Global { get; set; }
        public string Path { get; }
        public dynamic Variables { get; set; }

        private SteamID _chatroom;
        public SteamID Chatroom
        {
            get
            {
                if (Global)
                {
                    throw new MemberAccessException("Module is global and doesn't have a chatroom");
                }

                return _chatroom;
            }

            set
            {
                _chatroom = value;
            }

        }

        public PyModule(string path)
        {
            Commands = new List<Command>();
            Path = path;
            Global = false;
        }

        /// <summary>
        /// 'Compiles' a Python file and creates a module.
        /// </summary>
        /// <param name="file">The path of the Python file.</param>
        public ScriptScope Compile(SteamNerd steamNerd, ScriptEngine pyEngine)
        {
            var scope = pyEngine.CreateScope();

            scope.SetVariable("SteamNerd", steamNerd);
            scope.SetVariable("Module", this);

            try
            {
                // Add a "var" class to get all of the script variables
                pyEngine.Execute("class var:\n\tpass", scope);
                pyEngine.ExecuteFile(Path, scope);
            }
            catch (Exception e)
            {
                ModuleManager.PrintStackFrame(e);
                return null;
            }

            GetModuleCallbacks(scope);

            return scope;
        }

        /// <summary>
        /// Gets the functions in the python file and assigns the appropriate
        /// callback to them.
        /// </summary>
        /// <param name="module">The module to assign the callbacks</param>
        /// <param name="scope">The program scope</param>
        private void GetModuleCallbacks(ScriptScope scope)
        {
            Action<SteamFriends.ChatMsgCallback, string[]> onChatMessage;
            Action<SteamFriends.FriendMsgCallback, string[]> onFriendMessage;
            Action<SteamFriends.ChatEnterCallback> onSelfEnterChat;
            Action<SteamFriends.PersonaStateCallback> onEnterChat;
            Action<SteamFriends.ChatMemberInfoCallback> onLeaveChat;

            try
            {
                if (scope.TryGetVariable("OnChatMessage", out onChatMessage))
                {
                    OnChatMessageCallback = (callback, args) =>
                        onChatMessage(callback, args);
                }

                if (scope.TryGetVariable("OnFriendMessage", out onFriendMessage))
                {
                    OnFriendMessageCallback = (callback, args) =>
                        onFriendMessage(callback, args);
                }

                if (scope.TryGetVariable("OnSelfChatEnter", out onSelfEnterChat))
                {
                    OnSelfChatEnterCallback = (callback) =>
                        onSelfEnterChat(callback);
                }

                if (scope.TryGetVariable("OnChatEnter", out onEnterChat))
                {
                    OnChatEnterCallback = (callback) =>
                        onEnterChat(callback);
                }

                if (scope.TryGetVariable("OnChatLeave", out onLeaveChat))
                {
                    OnChatLeaveCallback = (callback) =>
                        onLeaveChat(callback);
                }
            }
            catch (Exception e)
            {
                ModuleManager.PrintStackFrame(e);
            }
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

                    for (var i = 1; i < command.Match.Length; i++)
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
