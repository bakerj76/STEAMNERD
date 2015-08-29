using System;
using System.Timers;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class LetterGames : Module
    {
        private const string LETTERS = "abcdefghijklmnopqrstuvwxyz";

        private Random _rand;
        private bool _inProgress;
        private char _bannedLetter;
        private Timer _changeTimer;


        public LetterGames(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Letter Games";
            Description = "Fun chat game.";

            AddCommand(
                "letter",
                "",
                LetterGame
            );

            AddCommand(
                "letter on",
                "Turns the letter game on.",
                null
            );

            AddCommand(
                "letter off",
                "Turns the letter game off.",
                null
            );

            AddCommand(
                "letter rules",
                "Checks the current rules.",
                null
            );

            AddCommand(
                "",
                "",
                CheckForLetter
            );

            _rand = new Random();
        }

        public void LetterGame(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            switch (args[1])
            {
                case "on":
                    Start(callback);
                    break;
                case "off":
                    Stop(callback);
                    break;
                case "rules":
                    Rules(callback);
                    break;
                default:
                    SteamNerd.SendMessage(string.Format("Usage: {0}letter on or {0}letter off", SteamNerd.CommandChar), callback.ChatRoomID, true);
                    break;
            }
        }

        public void Start(SteamFriends.ChatMsgCallback callback)
        {
            if (_inProgress) return;

            _inProgress = true;
            SteamNerd.SendMessage("The Letter Game is starting!", callback.ChatRoomID, true);
            ChooseLetter(callback);

            _changeTimer = new Timer(30000);
            _changeTimer.Elapsed += (src, e) => ChooseLetter(callback);

            var delay = new Timer(100);
            delay.Elapsed += (src, e) => _changeTimer.Start();
            delay.AutoReset = false;
            delay.Start();
        }

        public void Stop(SteamFriends.ChatMsgCallback callback)
        {
            if (!_inProgress) return;

            _inProgress = false;
            SteamNerd.SendMessage("The Letter Game is over!", callback.ChatRoomID, true);
            _changeTimer.Stop();

        }

        public void Rules(SteamFriends.ChatMsgCallback callback)
        {
            SteamNerd.SendMessage(string.Format("If you type '{0}', you die!", _bannedLetter), callback.ChatRoomID, true);
        }

        public void CheckForLetter(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_inProgress && callback.Message.Contains(_bannedLetter.ToString()))
            {
                SteamNerd.SteamFriends.KickChatMember(callback.ChatRoomID, callback.ChatterID);
            }
        }

        private void ChooseLetter(SteamFriends.ChatMsgCallback callback)
        {
            _bannedLetter = LETTERS[_rand.Next(LETTERS.Length)];
            Rules(callback);
        }
    }
}
