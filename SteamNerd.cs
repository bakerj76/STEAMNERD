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

        public readonly SteamFriends SteamFriends;

        public Dictionary<SteamID, string> ChatterNames { get; private set; }
        public SteamID CurrentChatRoom;

        public readonly SteamUser SteamUser;
        private readonly SteamClient _steamClient;
        private readonly CallbackManager _manager;

        private readonly string _user;
        private string _password;
        private string _authCode;
        private string _twoFactorAuth;

        public List<Module> Modules;
        private Dictionary<string, Module> _modulesByName;

        public SteamNerd(string user, string pass)
        {
            _user = user;
            _password = pass;

            _steamClient = new SteamClient();
            _manager = new CallbackManager(_steamClient);

            SteamUser = _steamClient.GetHandler<SteamUser>();
            SteamFriends = _steamClient.GetHandler<SteamFriends>();

            Modules = new List<Module>();
            _modulesByName = new Dictionary<string, Module>();
            CurrentChatRoom = null;

            ChatterNames = new Dictionary<SteamID, string>();

            #region Steam Client Callbacks

            _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnect);
            _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnect);

            #endregion

            #region Steam User Callbacks

            _manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
            _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            _manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            _manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

            #endregion

            #region Steam Friend Callbacks

            _manager.Subscribe<SteamFriends.PersonaStateCallback>(OnPersonaState);
            _manager.Subscribe<SteamFriends.FriendMsgCallback>(OnFriendMsg);
            _manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);
            _manager.Subscribe<SteamFriends.ChatMsgCallback>(OnChatMsg);
            _manager.Subscribe<SteamFriends.ChatInviteCallback>(OnChatInvite);
            _manager.Subscribe<SteamFriends.ChatEnterCallback>(OnChatEnter);
            _manager.Subscribe<SteamFriends.ChatMemberInfoCallback>(OnChatMemberInfo);

            #endregion
        }

        public void Connect()
        {
            IsRunning = true;
            _steamClient.Connect();

            Console.WriteLine("Connecting to Steam as {0}...", _user);

            while (IsRunning)
            {
               _manager.RunWaitCallbacks(TimeSpan.FromSeconds(1f));
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

            try
            {
                SteamUser.LogOn(new SteamUser.LogOnDetails
                {
                    Username = _user,
                    Password = _password,

                    AuthCode = _authCode,
                    TwoFactorCode = _twoFactorAuth,
                    SentryFileHash = sentryHash,
                }
);
            } 
            catch (ArgumentException e)
            {
                Console.WriteLine("{0}", e.Message);

                IsRunning = false;
                return;
            }

        }

        private void OnDisconnect(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected! Reconnecting...");

            Thread.Sleep(TimeSpan.FromSeconds(5));
            _steamClient.Connect();
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

        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            SteamFriends.SetPersonaState(EPersonaState.Online);
            SteamFriends.SetPersonaName("xXxTrollSlayerxXx");
        }

        private void OnPersonaState(SteamFriends.PersonaStateCallback callback)
        {
            if (CurrentChatRoom == null || callback.SourceSteamID != CurrentChatRoom) return;
            
            var friendID = callback.FriendID;

            if (callback.State != EPersonaState.Offline && !ChatterNames.ContainsKey(friendID))
            {
                Console.WriteLine("Adding {0}", callback.Name);
                ChatterNames.Add(friendID, callback.Name);

                foreach (var module in Modules)
                {
                    module.OnFriendChatEnter(callback);
                }
            }
        }

        private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            foreach (var module in Modules)
            {
                module.OnFriendMsg(callback);
            }
        }

        private void OnChatMemberInfo(SteamFriends.ChatMemberInfoCallback callback)
        {
            if (callback.ChatRoomID != CurrentChatRoom) return;

            var stateChangeInfo = callback.StateChangeInfo;
            var chatterID = stateChangeInfo.ChatterActedOn;

            switch (stateChangeInfo.StateChange)
            {
                case EChatMemberStateChange.Banned:
                case EChatMemberStateChange.Disconnected:
                case EChatMemberStateChange.Kicked:
                case EChatMemberStateChange.Left:
                {
                    Console.WriteLine("Removing {0}", ChatterNames[chatterID]);

                    foreach (var module in Modules)
                    {
                        module.OnFriendChatLeave(callback);
                    }

                    ChatterNames.Remove(chatterID);
                    break;
                }
            }
        }

        private void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            if (callback.ChatMsgType != EChatEntryType.ChatMsg) return;

            var listArgs = callback.Message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

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
            
            var args = listArgs.ToArray();

            foreach (var module in Modules.Where(module => module.Match(callback)))
            {
                module.OnChatMsg(callback);
            }

            foreach (var module in Modules)
            {
                module.RunCommand(callback, args);
            }
        }

        private void OnChatInvite(SteamFriends.ChatInviteCallback callback)
        {
            Console.WriteLine("Joining chat {0} ({1})...", callback.ChatRoomName, callback.ChatRoomID.Render());

            if (CurrentChatRoom == null)
            {
                CurrentChatRoom = callback.ChatRoomID;
            }

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

            Console.WriteLine("Joined chat.");

            foreach (var module in Modules)
            {
                module.OnSelfChatEnter(callback);
            }
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
        /// <param name="isChat">Is this a chat room or a person?</param>
        public void SendMessage(string message, SteamID steamID, bool isChat = true)
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

        public void AddModule(Module module)
        {
            Modules.Add(module);

            if (module.Name != null)
            {
                var name = module.Name.ToLower().Trim();

                if (name != "" && !_modulesByName.ContainsKey(name))
                {
                    _modulesByName[name] = module;
                }
            }
        }

        public Module GetModule(string name)
        {
            name = name.ToLower().Trim();

            if (_modulesByName.Keys.Contains(name))
            {
                return _modulesByName[name];
            }
            else
            {
                return null;
            }
        }
    }
}