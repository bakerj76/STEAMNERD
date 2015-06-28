using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Security.Cryptography;

using SteamKit2;

namespace STEAMNERD
{
    public class SteamNerd
    {
        public bool IsRunning;

        public readonly SteamFriends SteamFriends;

        public Dictionary<SteamID, string> Chatters;  

        private readonly SteamClient _steamClient;
        private readonly SteamUser _steamUser;
        private readonly CallbackManager _manager;

        private readonly string _user;
        private string _password;
        private string _authCode;
        private string _twoFactorAuth;

        private List<Module> _modules;
        private SteamID _currentChatRoom;

        public SteamNerd(string user, string pass)
        {
            _user = user;
            _password = pass;

            _steamClient = new SteamClient();
            _manager = new CallbackManager(_steamClient);

            _steamUser = _steamClient.GetHandler<SteamUser>();
            SteamFriends = _steamClient.GetHandler<SteamFriends>();

            _modules = new List<Module>();
            _currentChatRoom = null;

            Chatters = new Dictionary<SteamID, string>();

            #region Steam Client Callbacks

            new Callback<SteamClient.ConnectedCallback>(OnConnect, _manager);
            new Callback<SteamClient.DisconnectedCallback>(OnDisconnect, _manager);

            #endregion

            #region Steam User Callbacks

            new Callback<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth, _manager);
            new Callback<SteamUser.LoggedOnCallback>(OnLoggedOn, _manager);
            new Callback<SteamUser.LoggedOffCallback>(OnLoggedOff, _manager);
            new Callback<SteamUser.AccountInfoCallback>(OnAccountInfo, _manager);

            #endregion
            
            #region Steam Friend Callbacks

            new Callback<SteamFriends.PersonaStateCallback>(OnPersonaState, _manager);
            new Callback<SteamFriends.FriendMsgCallback>(OnFriendMsg, _manager);
            new Callback<SteamFriends.ChatMsgCallback>(OnChatMsg, _manager);
            new Callback<SteamFriends.ChatInviteCallback>(OnChatInvite, _manager);
            new Callback<SteamFriends.ChatEnterCallback>(OnChatEnter, _manager);

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

            Console.ReadKey(true);
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

            _steamUser.LogOn(new SteamUser.LogOnDetails
                             {
                                 Username = _user,
                                 Password = _password,

                                 AuthCode = _authCode,
                                 TwoFactorCode = _twoFactorAuth,
                                 SentryFileHash = sentryHash,
                             }
            );
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
            _steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
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
        }

        private void OnPersonaState(SteamFriends.PersonaStateCallback callback)
        {
            if (_currentChatRoom == null || callback.SourceSteamID != _currentChatRoom) return;

            var friendId = callback.FriendID;

            if (callback.State != EPersonaState.Offline && !Chatters.ContainsKey(friendId))
            {
                Console.WriteLine("Adding {0}", callback.Name);
                Chatters.Add(friendId, callback.Name);
            }
            else if (callback.State == EPersonaState.Offline && Chatters.ContainsKey(friendId))
            {
                Chatters.Remove(friendId);
            }
        }

        private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            foreach (var module in _modules)
            {
                module.OnFriendMsg(callback);
            }
        }

        private void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            if (callback.ChatMsgType != EChatEntryType.ChatMsg) return;

            foreach (var module in _modules.Where(module => module.Match(callback)))
            {
                module.OnChatMsg(callback);
            }
        }

        private void OnChatInvite(SteamFriends.ChatInviteCallback callback)
        {
            Console.WriteLine("Joining chat {0}...", callback.ChatRoomName);

            _currentChatRoom = callback.ChatRoomID;
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

            foreach (var module in _modules)
            {
                module.OnChatEnter(callback);
            }
        }

        /// <summary>
        /// Sends a chat message to a SteamID.
        /// </summary>
        /// <param name="message">The chat message</param>
        /// <param name="steamid">The person or the chat room to send to</param>
        /// <param name="isChat">Is this a chat room or a person?</param>
        public void SendMessage(string message, SteamID steamid, bool isChat)
        {
            if (isChat)
            {
                SteamFriends.SendChatRoomMessage(steamid, EChatEntryType.ChatMsg, message);
            }
            else
            {
                SteamFriends.SendChatMessage(steamid, EChatEntryType.ChatMsg, message);
            }
        }

        public void AddModule(Module module)
        {
            _modules.Add(module);
        }
    }
}