using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using SteamKit2;
using SteamKit2.GC.Dota.Internal;

namespace STEAMNERD
{
    public class SteamNerd
    {
        public bool IsRunning;

        private readonly SteamClient _steamClient;
        private readonly SteamUser _steamUser;
        private readonly SteamFriends _steamFriends;
        private readonly CallbackManager _manager;

        private readonly string _user;
        private readonly string _password;
        private string _authCode;
        private string _twoFactorAuth;

        public SteamNerd(string user, string pass)
        {
            _user = user;
            _password = pass;

            _steamClient = new SteamClient();
            _manager = new CallbackManager(_steamClient);

            _steamUser = _steamClient.GetHandler<SteamUser>();
            _steamFriends = _steamClient.GetHandler<SteamFriends>();

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
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logging off of {0}!", _user);
        }

        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            _steamFriends.SetPersonaState(EPersonaState.Online);
        }

        private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            ParseMessage(callback.Message, callback.Sender, false);
        }

        private void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            if (callback.ChatMsgType != EChatEntryType.ChatMsg) return;

            ParseMessage(callback.Message, callback.ChatRoomID, true);
        }

        private void OnChatInvite(SteamFriends.ChatInviteCallback callback)
        {
            Console.WriteLine("Joining chat {0}...", callback.ChatRoomName);
            _steamFriends.JoinChat(callback.ChatRoomID);
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
        }

        private void ParseMessage(string message, SteamID steamid, bool isChat)
        {

            if (!Regex.IsMatch(message, @"^!\d+d\d")) return;

            var split = Regex.Split(message, "[!d]");
            int numDice, sides;

            if (!int.TryParse(split[1], out numDice) || numDice == 0 || numDice > 1000) return;
            if (!int.TryParse(split[2], out sides) || sides == 0 || sides >= int.MaxValue - 1) return;

            var rolls = new int[numDice];
            var rand = new Random();

            for (var i = 0; i < numDice; i++)
            {
                rolls[i] = rand.Next(1, sides + 1);
            }

            var rollStr = "";
            if (numDice > 1)
            {
                rollStr = rolls.Select(roll => roll.ToString())
                    .Aggregate((current, roll) => string.Format("{0} + {1}", current, roll));
                try
                {
                    rollStr += string.Format(" = {0}", rolls.Sum());
                }
                catch (Exception e)
                {
                    SendMessage(string.Format("Wow, cool, overflow... Very nice {0}", steamid.Render()), steamid, isChat);
                    return;
                }
            }
            else
            {
                rollStr = rolls[0].ToString();
            }

            SendMessage(rollStr, steamid, isChat);
        }

        private void SendMessage(string message, SteamID steamid, bool isChat)
        {
            if (isChat)
            {
                _steamFriends.SendChatRoomMessage(steamid, EChatEntryType.ChatMsg, message);
            }
            else
            {
                _steamFriends.SendChatMessage(steamid, EChatEntryType.ChatMsg, message);
            }
        }
    }
}