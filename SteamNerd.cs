using System;
using System.Collections.Generic;
using System.Linq;
using SteamKit2;

namespace SteamNerd
{
    public class SteamNerd
    {
        public const char CommandChar = '.';

        public bool IsRunning;

        private string _username;
        
        public readonly SteamUser SteamUser;
        public readonly SteamClient SteamClient;
        public readonly SteamFriends SteamFriends;

        public readonly CallbackManager CallbackManager;
        public readonly ModuleManager ModuleManager;
        public readonly UserManager UserManager;
        public readonly ChatRoomManager ChatRoomManager;

        private readonly Login _login;

        public SteamNerd()
        {
            SteamClient = new SteamClient();

            CallbackManager = new CallbackManager(SteamClient);
            ModuleManager = new ModuleManager(this);
            UserManager = new UserManager(this, "admins.txt");
            ChatRoomManager = new ChatRoomManager(this, "chats.txt");

            SteamUser = SteamClient.GetHandler<SteamUser>();
            SteamFriends = SteamClient.GetHandler<SteamFriends>();
            
            SubscribeCallbacks();

            _login = new Login(this, CallbackManager);
        }

        public void Connect() 
        {
            Connect(null, null);
        }

        public void Connect(string username, string password)
        {
            if (username == null) 
            {
                Login.FancyLogIn(out username, out password);
            }

            IsRunning = true;
            _username = username;
            _login.Connect(username, password);

            while (IsRunning)
            {
                CallbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1f));
            }
        }

        public void Disconnect(bool tryReconnect = false)
        {
            IsRunning = false;
            SteamUser.LogOff();
            SteamClient.Disconnect();

            if (tryReconnect)
            {
                _login.Connect();
            }
        }

        /// <summary>
        /// Subscribes the functions to the SteamKit callbacks.
        /// </summary>
        private void SubscribeCallbacks()
        {
            #region Steam User Callbacks
            CallbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            #endregion

            #region Steam Friend Callbacks
            CallbackManager.Subscribe<SteamFriends.PersonaStateCallback>(OnPersonaState);
            CallbackManager.Subscribe<SteamFriends.FriendMsgCallback>(OnFriendMsg);
            CallbackManager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);
            CallbackManager.Subscribe<SteamFriends.ChatMsgCallback>(OnChatMsg);
            CallbackManager.Subscribe<SteamFriends.ChatInviteCallback>(OnChatInvite);
            CallbackManager.Subscribe<SteamFriends.ChatEnterCallback>(OnChatEnter);
            CallbackManager.Subscribe<SteamFriends.ChatMemberInfoCallback>(OnChatMemberInfo);
            #endregion
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logging off of {0}!", _username);
        }

        /// <summary>
        /// Sets the persona state to online and, since logging in changes the
        /// bot's name to [unknown] sometimes, sets the persona name.
        /// </summary>
        /// <param name="callback"></param>
        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            Console.WriteLine("Setting account info.");

            // TODO: put this stuff in an .ini file
            SteamFriends.SetPersonaState(EPersonaState.Online);
            SteamFriends.SetPersonaName("xXxTrollSlayerxXx");
        }

        /// <summary>
        /// Gets someone's name when joining a chatroom and fires off 
        /// OnFriendChatEnter module callbacks.
        /// </summary>
        /// <param name="callback"></param>
        private void OnPersonaState(SteamFriends.PersonaStateCallback callback)
        {
            var friendID = callback.FriendID;
            var name = callback.Name;
            var user = UserManager.GetUser(friendID);

            if (!UserManager.UserExists(friendID))
            {
                user = UserManager.AddUser(friendID, name, callback.State);
            }

            if (callback.SourceSteamID.IsChatAccount)
            {
                var chatID = callback.SourceSteamID;
                var chatRoom = ChatRoomManager.AddChatRoom(chatID, null);

                // Add the user to this chat room.
                chatRoom.AddUser(user);

                // Trigger EnteredChat for each module.
                ModuleManager.EnteredChat(callback);
            }

            // Update their persona state.
            user.PersonaState = callback.State;
        }

        private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            if (callback.EntryType != EChatEntryType.ChatMsg)
            
            ModuleManager.FriendMessageSent(callback, MessageToArgs(callback.Message));
            
        }

        private void OnChatMemberInfo(SteamFriends.ChatMemberInfoCallback callback)
        {
            var stateChangeInfo = callback.StateChangeInfo;
            var chatRoomID = callback.ChatRoomID;
            var chatterID = stateChangeInfo.ChatterActedOn;

            if (!ChatRoomManager.ChatRoomExists(chatRoomID) ||
                !UserManager.UserExists(chatterID))
            {
                return;
            }
            
            var user = UserManager.GetUser(chatterID);
            var chatRoom = ChatRoomManager.GetChatRoom(chatRoomID);

            switch (stateChangeInfo.StateChange)
            {
                case EChatMemberStateChange.Banned:
                case EChatMemberStateChange.Disconnected:
                case EChatMemberStateChange.Kicked:
                case EChatMemberStateChange.Left:
                    Console.WriteLine("Removing {0} from {1}", user.Name, chatRoom.Name);

                    // Trigger LeftChat for each module. 
                    ModuleManager.LeftChat(callback);

                    // Remove this user from the chat room.
                    chatRoom.RemoveUser(user);
                    break;
            }
        }

        private void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            if (callback.ChatMsgType != EChatEntryType.ChatMsg) return;

            var args = MessageToArgs(callback.Message);
            ModuleManager.ChatMessageSent(callback, args);
        }

        private static string[] MessageToArgs(string message)
        {
            var listArgs = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (var i = 0; i < listArgs.Count; i++)
            {
                if (listArgs[i].Contains('\n'))
                {
                    var split = listArgs[i].Replace("\n", "\n\0").Split('\0');
                    listArgs.RemoveAt(i);
                    listArgs.InsertRange(i, split);

                    i += split.Length - 1;
                }
            }

            return listArgs.ToArray();
        }

        private void OnChatInvite(SteamFriends.ChatInviteCallback callback)
        {
            Console.WriteLine("Joining chat {0} ({1})...", callback.ChatRoomName, callback.ChatRoomID.Render());
            SteamFriends.JoinChat(callback.ChatRoomID);
        }

        private void OnChatEnter(SteamFriends.ChatEnterCallback callback)
        {
            if (callback.EnterResponse != EChatRoomEnterResponse.Success)
            {
                // It's super cool that Steam added this $5.00 thing! Real cool...
                Console.WriteLine("{0}", callback.EnterResponse);
                return;
            }

            var chatID = callback.ChatID;
            var chatName = callback.ChatRoomName;

            // If the chat room exists, update the name.
            if (ChatRoomManager.ChatRoomExists(chatID))
            {
                Console.WriteLine("Updating chat room name.");
                var chatRoom = ChatRoomManager.GetChatRoom(callback.ChatID);
                chatRoom.Name = chatName;
            }
            // Else just add the chat room.
            else
            {
                Console.WriteLine("Adding chat room.");
                ChatRoomManager.AddChatRoom(chatID, chatName);
            }

            Console.WriteLine("Joined chat.");
            ModuleManager.SelfEnteredChat(callback);
        }

        private void OnFriendsList(SteamFriends.FriendsListCallback callback)
        {
            Console.WriteLine("Getting Friends List...");

            foreach (var friend in callback.FriendList)
            {
                if (friend.Relationship == EFriendRelationship.RequestRecipient)
                {
                    Console.WriteLine("Adding Friend {0}.", friend.SteamID);
                    SteamFriends.AddFriend(friend.SteamID);
                }
            }
        }

        /// <summary>
        /// Sends a chat message to a SteamID.
        /// </summary>
        /// <param name="message">The chat message</param>
        /// <param name="steamID">The person or the chat room to send to</param>
        /// 
        public void SendMessage(string message, SteamID steamID)
        {
            if (steamID.IsChatAccount)
            {
                SteamFriends.SendChatRoomMessage(steamID, EChatEntryType.ChatMsg, message);
            }
            else
            {
                SteamFriends.SendChatMessage(steamID, EChatEntryType.ChatMsg, message);
            }
        }

        public dynamic[] GetModules(SteamID chatroomID = null)
        {
            return ModuleManager.GetModules(chatroomID);
        }

        public dynamic GetModule(string moduleName, SteamID chatroomID = null)
        {
            return ModuleManager.GetModule(moduleName, chatroomID);
        }
    }
}