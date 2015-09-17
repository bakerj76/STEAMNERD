using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Security.Cryptography;

using SteamKit2;

namespace SteamNerd
{
    public class SteamNerd
    {
        public const char CommandChar = '.';

        public bool IsRunning;

        public Dictionary<SteamID, string> ChatterNames { get; private set; }
        public Dictionary<SteamID, ChatRoom> ChatRooms { get; private set; }

        public readonly SteamUser SteamUser;
        public readonly SteamClient SteamClient;
        public readonly SteamFriends SteamFriends;
        public readonly CallbackManager CallbackManager;
        public readonly ModuleManager ModuleManager;

        private readonly string _user;
        private string _password;
        private string _authCode;
        private string _twoFactorAuth;

        public SteamNerd(string user, string pass)
        {
            _user = user;
            _password = pass;

            SteamClient = new SteamClient();
            CallbackManager = new CallbackManager(SteamClient);
            ModuleManager = new ModuleManager(this);

            SteamUser = SteamClient.GetHandler<SteamUser>();
            SteamFriends = SteamClient.GetHandler<SteamFriends>();

            ChatterNames = new Dictionary<SteamID, string>();
            ChatRooms = new Dictionary<SteamID, ChatRoom>();

            SubscribeCallbacks();
        }

        /// <summary>
        /// Subscribes the functions to the SteamKit callbacks.
        /// </summary>
        private void SubscribeCallbacks()
        {
            #region Steam Client Callbacks

            CallbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnect);
            CallbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnect);

            #endregion

            #region Steam User Callbacks

            CallbackManager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
            CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
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

        #region Logging On/Off
        public void Connect()
        {
            IsRunning = true;
            SteamClient.Connect();

            Console.WriteLine("Connecting to Steam as {0}...", _user);

            while (IsRunning)
            {
               CallbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1f));
            }
        }

        private void OnConnect(SteamClient.ConnectedCallback callback)
        {
            // Some error popped up while trying to log in
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("{0}", callback.Result);

                IsRunning = false;
                return;
            }

            byte[] sentryHash = null;

            if (File.Exists("sentry.bin"))
            {
                var sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            SteamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = _user,
                Password = _password,

                AuthCode = _authCode,
                TwoFactorCode = _twoFactorAuth,
                SentryFileHash = sentryHash,
            });
        }

        private void OnDisconnect(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected! Reconnecting...");

            Thread.Sleep(TimeSpan.FromSeconds(5));
            SteamClient.Connect();
        }

        public void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating sentryfile...");

            // write out our sentry file
            // ideally we'd want to write to the filename specified in the callback
            // but then this sample would require more code to find the correct sentry file to read during logon
            // for the sake of simplicity, we'll just use "sentry.bin"

            int fileSize;
            byte[] sentryHash;
            using (var fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = (int) fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = new SHA1CryptoServiceProvider())
                {
                    sentryHash = sha.ComputeHash(fs);
                }
            }

            // inform the steam servers that we're accepting this sentry file
            SteamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
                {
                    JobID = callback.JobID,

                    FileName = callback.FileName,

                    BytesWritten = callback.BytesToWrite,
                    FileSize = fileSize,
                    Offset = callback.Offset,

                    Result = EResult.OK,
                    LastError = 0,

                    OneTimePassword = callback.OneTimePassword,

                    SentryFileHash = sentryHash,
                }
            );

            Console.WriteLine("Done!");
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            var isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            var is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuard || is2FA)
            {
                Console.WriteLine("This account is SteamGuard protected!");

                if (is2FA)
                {
                    Console.Write("Please enter your 2 factor auth code from your authenticator app: ");
                    _twoFactorAuth = Console.ReadLine();
                }
                else
                {
                    Console.Write("Please enter the auth code sent to the email at {0}: ", callback.EmailDomain);
                    _authCode = Console.ReadLine();
                }

                return;
            }

            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);

                IsRunning = false;
                return;
            }

            Console.WriteLine("Logged in as {0}!", _user);
            _password = "";
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logging off of {0}!", _user);
        }
        #endregion

        /// <summary>
        /// Sets the persona state to online and, since logging in changes the
        /// bot's name to [unknown] sometimes, sets the persona name.
        /// </summary>
        /// <param name="callback"></param>
        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
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

            if (callback.State == EPersonaState.Offline ||
                !callback.SourceSteamID.IsChatAccount ||
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
                ChatRooms[chatroom] = new ChatRoom(name);
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