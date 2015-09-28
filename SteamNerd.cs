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
        public Dictionary<SteamID, string> ChatterNames { get; private set; }
        public Dictionary<SteamID, ChatRoom> ChatRooms { get; private set; }
        public Dictionary<SteamID, User> Users { get; private set; }

        public readonly SteamUser SteamUser;
        public readonly SteamClient SteamClient;
        public readonly SteamFriends SteamFriends;
        public readonly CallbackManager CallbackManager;
        public readonly ModuleManager ModuleManager;
        private readonly AdminManager _adminManager;
        private readonly Login _login;

        public SteamNerd()
        {
            SteamClient = new SteamClient();
            CallbackManager = new CallbackManager(SteamClient);
            ModuleManager = new ModuleManager(this);
            _adminManager = new AdminManager("admins.txt");

            SteamUser = SteamClient.GetHandler<SteamUser>();
            SteamFriends = SteamClient.GetHandler<SteamFriends>();

            Users = new Dictionary<SteamID, User>();
            ChatterNames = new Dictionary<SteamID, string>();
            ChatRooms = new Dictionary<SteamID, ChatRoom>();

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

            if (!Users.ContainsKey(friendID))
            {
                Users[friendID] = new User(this, friendID, name, callback.State);
            }
            else
            {
                Users[friendID].PersonaState = callback.State;
            }
            

            if (!callback.SourceSteamID.IsChatAccount ||
                ChatterNames.ContainsKey(friendID))
            {
                return;
            }

            AddChatroom(callback.SourceSteamID, null);
            AddChatter(friendID, callback.SourceSteamID, name);
            ModuleManager.EnteredChat(callback);
        }

        private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            if (callback.EntryType != EChatEntryType.ChatMsg)
            
            ModuleManager.FriendMessageSent(callback, MessageToArgs(callback.Message));
            
        }

        private void OnChatMemberInfo(SteamFriends.ChatMemberInfoCallback callback)
        {
            var chatRoomID = callback.ChatRoomID;

            if (!ChatRooms.ContainsKey(chatRoomID)) return;

            var stateChangeInfo = callback.StateChangeInfo;
            var chatterID = stateChangeInfo.ChatterActedOn;

            switch (stateChangeInfo.StateChange)
            {
                case EChatMemberStateChange.Banned:
                case EChatMemberStateChange.Disconnected:
                case EChatMemberStateChange.Kicked:
                case EChatMemberStateChange.Left:
                    Console.WriteLine("Removing {0} from {1}", ChatterNames[chatterID], ChatRooms[chatRoomID].Name);
                    ModuleManager.LeftChat(callback);
                    ChatterNames.Remove(chatterID);
                    ChatRooms[chatRoomID].Chatters.Remove(chatterID);
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

            AddChatroom(callback.ChatID, callback.ChatRoomName);
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

        public string GetName(SteamID steamID)
        {
            return ChatterNames.ContainsKey(steamID) ? ChatterNames[steamID] : null;
        }

        public void AddChatroom(SteamID chatroom, string name)
        {
            if (ChatRooms.ContainsKey(chatroom))
            {
                if (ChatRooms[chatroom].Name == null)
                {
                    ChatRooms[chatroom].Name = name;
                }
            }
            else
            {
                ChatRooms[chatroom] = new ChatRoom(this, chatroom, name);
                ModuleManager.AddChatroom(chatroom);
            }
        }

        public void AddChatter(SteamID chatterID, SteamID chatID, string name)
        {
            Console.WriteLine("Adding {0}", name);
            ChatterNames.Add(chatterID, name);
            ChatRooms[chatID].Chatters.Add(chatterID);
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