using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using SteamKit2;

namespace SteamNerd
{
    class Login
    {
        private SteamNerd _steamNerd;
        private string _username;
        private string _password;
        private string _authCode;
        private string _twoFactorCode;
        private string _loginKey;

        public Login(SteamNerd steamNerd, CallbackManager manager)
        {
            _steamNerd = steamNerd;

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnect);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnect);
            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoginKeyCallback>(OnLoginKey);
        }

        public void Connect(string username, string password)
        {
            _username = username;
            _password = password;

            Connect();
        }

        public void Connect()
        {
            Console.WriteLine("Connecting to Steam...");
            _steamNerd.SteamClient.Connect();
        }

        private void OnConnect(SteamClient.ConnectedCallback callback)
        {
            // Some error popped up while trying to log in
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("{0}", callback.Result);

                _steamNerd.Disconnect();
                return;
            }

            byte[] sentryHash = null;

            if (File.Exists("sentry.bin"))
            {
                var sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            LogOn(sentryHash);
        }

        private void LogOn(byte[] sentryHash)
        {
            _steamNerd.SteamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = _username,
                Password = _password,

                LoginKey = _loginKey,

                AuthCode = _authCode,
                TwoFactorCode = _twoFactorCode,
                SentryFileHash = sentryHash,

                ShouldRememberPassword = true
            });

            _username = "";
            _password = "";
        }

        private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
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
                fileSize = (int)fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = new SHA1CryptoServiceProvider())
                {
                    sentryHash = sha.ComputeHash(fs);
                }
            }

            // inform the steam servers that we're accepting this sentry file
            _steamNerd.SteamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
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
                    Console.Write("Please enter your two-factor authentication code: ");
                    _twoFactorCode = Console.ReadLine();
                }
                else
                {
                    Console.Write("Please enter the authentication code sent to the email at {0}: ", callback.EmailDomain);
                    _authCode = Console.ReadLine();
                }

                return;
            }

            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);
                _steamNerd.Disconnect();
                return;
            }

            Console.WriteLine("Logged in!");
            _password = "";
        }

        private void OnLoginKey(SteamUser.LoginKeyCallback callback)
        {
            _loginKey = callback.LoginKey;
        }

        private void OnDisconnect(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected! Reconnecting...");

            Thread.Sleep(TimeSpan.FromSeconds(5));
            _steamNerd.SteamClient.Connect();
        }

        /// <summary>
        /// Handles input for a "fancy" login. It doesn't show the password
        ///  when the user types it.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public static void FancyLogIn(out string username, out string password) {
            // Get the username
            Console.Write("Username: ");
            username = Console.ReadLine();

            // Get the password
            Console.Write("Password: ");
            var key = Console.ReadKey(true);
            password = "";

            while (key.Key != ConsoleKey.Enter) {
                password += key.KeyChar;
                key = Console.ReadKey(true);
            }

            Console.WriteLine();
        }
    }
}
