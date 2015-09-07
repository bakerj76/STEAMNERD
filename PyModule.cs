using System;
using System.Collections.Generic;
using System.IO;
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
        
        private Action<SteamFriends.ChatMsgCallback, string[]> ChatMessage;
        private Action<SteamFriends.FriendMsgCallback, string[]> FriendMessage;
        private Action<SteamFriends.ChatEnterCallback> SelfChatEnterCallback;
        private Action<SteamFriends.PersonaStateCallback> ChatEnterCallback;
        private Action<SteamFriends.ChatMemberInfoCallback> ChatLeaveCallback;

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
        /// Interprets a Python file and creates a module.
        /// </summary>
        /// <param name="file">The path of the Python file.</param>
        public ScriptScope Interpret(SteamNerd steamNerd, ScriptEngine pyEngine)
        {
            Commands.Clear();
            var scope = pyEngine.CreateScope();

            scope.SetVariable("SteamNerd", steamNerd);
            scope.SetVariable("Module", this);

            try
            {
                // Add a "var" class to get all of the script variables.
                // And add a way to reference SteamKit2.
                pyEngine.Execute(
                    "from System import Environment\n" +
                    "import clr\n" +
                    "import sys\n" +
                    "sys.path.append(Environment.GetEnvironmentVariable('IRONPYTHONPATH'))\n" +
                    "sys.path.append(r'" + Directory.GetCurrentDirectory() + "')\n" +
                    "clr.AddReference('SteamKit2.dll')\n" +
                    "class var:\n" +
                    "   pass", 
                    scope
                );
                pyEngine.ExecuteFile(Path, scope);
            }
            catch (Exception e)
            {
                ModuleManager.PrintStackFrame(e);
                return null;
            }

            SetModuleCallbacks(scope);

            return scope;
        }

        /// <summary>
        /// Gets the functions in the python file and assigns the appropriate
        /// callback to them.
        /// </summary>
        /// <param name="module">The module to assign the callbacks</param>
        /// <param name="scope">The program scope</param>
        private void SetModuleCallbacks(ScriptScope scope)
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
                    ChatMessage = onChatMessage;
                }

                if (scope.TryGetVariable("OnFriendMessage", out onFriendMessage))
                {
                    FriendMessage = onFriendMessage;
                }

                if (scope.TryGetVariable("OnSelfChatEnter", out onSelfEnterChat))
                {
                    SelfChatEnterCallback = onSelfEnterChat;
                }

                if (scope.TryGetVariable("OnChatEnter", out onEnterChat))
                {
                    ChatEnterCallback = onEnterChat;
                }

                if (scope.TryGetVariable("OnChatLeave", out onLeaveChat))
                {
                    ChatLeaveCallback = onLeaveChat;
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
            if (ChatMessage != null)
            {
                try
                {
                    ChatMessage(callback, args);
                }
                catch (Exception e)
                {
                    ModuleManager.PrintStackFrame(e);
                }
            }
        }

        public void OnFriendMessage(SteamFriends.FriendMsgCallback callback, string[] args)
        {
            if (FriendMessage != null)
            {
                try
                {
                    FriendMessage(callback, args);
                }
                    catch (Exception e)
                {
                    ModuleManager.PrintStackFrame(e);
                }
            }
        }

        public void OnSelfChatEnter(SteamFriends.ChatEnterCallback callback)
        {
            if (SelfChatEnterCallback != null)
            {
                try
                { 
                    SelfChatEnterCallback(callback);
                }
                catch (Exception e)
                {
                    ModuleManager.PrintStackFrame(e);
                }
            }
        }

        public void OnChatEnter(SteamFriends.PersonaStateCallback callback)
        {
            if (ChatEnterCallback != null)
            {
                try
                {
                    ChatEnterCallback(callback);
                }
                catch (Exception e)
                {
                    ModuleManager.PrintStackFrame(e);
                }
            }
        }

        public void OnChatLeave(SteamFriends.ChatMemberInfoCallback callback)
        {
            if (ChatLeaveCallback != null)
            {
                try
                { 
                    ChatLeaveCallback(callback);
                }
                catch (Exception e)
                {
                    ModuleManager.PrintStackFrame(e);
                }
            }
        }
    }
}
