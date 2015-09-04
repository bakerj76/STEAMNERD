using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Timers;
using SteamKit2;

namespace SteamNerd.Modules
{
    class Duel : Module
    {
        private List<SteamID> _players;
        private bool _inProgress;
        private string _word;

        public Duel(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Duel";
            Description = "Duel other chatters!";

            AddCommand(
                "duel",
                "Enter the dueling arena to duel.",
                EnterDuel
            );

            AddCommand(
                "",
                "",
                CheckDuel
            );

            _players = new List<SteamID>();
        }

        public void EnterDuel(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var dueler = callback.ChatterID;
            var chat = callback.ChatRoomID;
            var name = SteamNerd.ChatterNames[dueler];

            if (_inProgress || _players.Contains(dueler)) return;

            _players.Add(dueler);

            if (_players.Count == 1)
            {
                SteamNerd.SendMessage(string.Format("{0} wants to duel someone! Bring it on!", name),
                    chat);
            }
            else if (_players.Count == 2)
            {
                var challengerName = SteamNerd.ChatterNames[_players[0]];

                _inProgress = true;
                SteamNerd.SendMessage(string.Format("{0} is dueling {1}! D-d-d-d-d-duel.", challengerName, name), chat);


                var countdown = new Countdown(SteamNerd, chat, (src, e) => StartDuel(callback), 4f, 3);

                var webRequest = WebRequest.Create("http://randomword.setgetgo.com/get.php");
                var webResponse = webRequest.GetResponse();
                var buffer = new StringBuilder();

                using (var stream = new StreamReader(webResponse.GetResponseStream()))
                {
                    _word = stream.ReadToEnd().ToLower().Trim();
                }
            }
        }

        public void StartDuel(SteamFriends.ChatMsgCallback callback)
        {
            SteamNerd.SendMessage(string.Format("Type: '{0}'", _word), callback.ChatRoomID);
        }

        public void CheckDuel(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (!_inProgress || args[0].ToLower() != _word || _word == "") return;

            var chat = callback.ChatRoomID;
            var name = SteamNerd.ChatterNames[callback.ChatterID];
            SteamNerd.SendMessage(string.Format("{0} wins!", name), chat);

            SteamID kickee = _players.Where(player => player != callback.ChatterID).First();

            var delay = new Timer(1000f);
            delay.AutoReset = false;
            delay.Elapsed += (src, e) => SteamNerd.SteamFriends.KickChatMember(chat, kickee);
            delay.Start();

            _inProgress = false;
            _players = new List<SteamID>();
            _word = "";
        }
    }
}
