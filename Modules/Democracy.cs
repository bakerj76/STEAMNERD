using System.Collections.Generic;
using System.Linq;
using System.Timers;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Democracy : Module
    {
        private int _ayes;
        private int _nays;
        private List<SteamID> _voters;

        private Timer _voteTimer;
        private bool _voting;

        private SteamID _kickee;

        public Democracy(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Democracy";
            Description = "USA! USA! USA!";

            AddCommand(
                "vote",
                "",
                Vote
            );

            AddCommand(
                "vote [thing to vote on]",
                "Vote on something.",
                null
            );

            AddCommand(
                "votekick",
                "",
                VoteKick
            );

            AddCommand(
                "votekick [person]",
                "Vote kick a useless troll idiot.",
                null
            );

            AddCommand(
                "",
                "",
                CheckVote
            );

            _voters = new List<SteamID>();
        }
        

        /// <summary>
        /// Starts a vote
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void Vote(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_voting || args.Length < 2)
            {
                return;
            }

            var sentence = args.Skip(1).Aggregate((current, next) => current + " " + next);
            sentence = sentence.Trim();

            SteamNerd.SendMessage("Voting has started! Type aye or nay to vote.", callback.ChatRoomID, true);
            SteamNerd.SendMessage(sentence, callback.ChatRoomID, true);

            // Reset ayes and nays
            _ayes = _nays = 0;

            _voters = new List<SteamID>();
            _voting = true;

            _voteTimer = new Timer(30000);
            _voteTimer.AutoReset = false;
            _voteTimer.Elapsed += (src, e) => EndVote(callback);
            _voteTimer.Start();
        }

        /// <summary>
        /// Checks if it's a vote and does stuff
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void CheckVote(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var name = SteamNerd.ChatterNames[callback.ChatterID];
            var message = callback.Message.ToLower();

            if (!_voting || _voters.Contains(callback.ChatterID))
            {
                return;
            }

            if (message == "aye")
            {
                _ayes++;
                SteamNerd.SendMessage(string.Format("{0} voted aye", name), callback.ChatRoomID, true);
            }
            else if (message == "nay")
            {
                _nays++;
                SteamNerd.SendMessage(string.Format("{0} voted nay", name), callback.ChatRoomID, true);
            }


            _voters.Add(callback.ChatterID);

            if (_voters.Count == SteamNerd.ChatterNames.Count)
            {
                _voteTimer.Stop();
                EndVote(callback);
            }
        }

        public void VoteKick(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (args.Length < 2)
            {
                return;
            }

            var chat = callback.ChatRoomID;
            var kickeeName = args.Skip(1).Aggregate((current, next) => current + " " + next);
            var name = SteamNerd.ChatterNames[callback.ChatterID];

            _kickee = null;

            foreach (var chatterKV in SteamNerd.ChatterNames)
            {
                if (chatterKV.Value.Contains(kickeeName))
                {
                    _kickee = chatterKV.Key;
                    break;
                }
            }
             
            if (_kickee == null)
            {
                SteamNerd.SendMessage(string.Format("{0} not found!", kickeeName), chat, true);
                return;
            }

            SteamNerd.SendMessage(string.Format("{0} wants to kick {1}! Type aye or nay to vote.", name, SteamNerd.ChatterNames[_kickee]), callback.ChatRoomID, true);

            // Reset ayes and nays
            _ayes = 1;
            _nays = 0;

            _voting = true;

            // Obviously, the votekicker wants to kick
            _voters = new List<SteamID>();
            _voters.Add(callback.ChatterID);

            _voteTimer = new Timer(30000);
            _voteTimer.AutoReset = false;
            _voteTimer.Elapsed += (src, e) => EndVoteKick(callback);
            _voteTimer.Start();
        }

        public void EndVote(SteamFriends.ChatMsgCallback callback)
        {
            _voting = false;

            var total = _ayes + _nays;
            var ayePercent = (float)_ayes / total;
            var nayPercent = (float)_nays / total;

            var message = string.Format("The votes are in:\n " +
                "Ayes: {0} ({1:P0})\n" +
                "Nays: {2} ({3:P0})", 
                _ayes, ayePercent, _nays, nayPercent);

            SteamNerd.SendMessage(message, callback.ChatRoomID, true);

            _voters = new List<SteamID>();
        }

        public void EndVoteKick(SteamFriends.ChatMsgCallback callback)
        {
            _voting = false;

            var chat = callback.ChatRoomID;
            var total = _ayes + _nays;
            // -1 for TrollSlayer
            var chatterCount = SteamNerd.ChatterNames.Count - 1;
            var ayePercent = (float)_ayes / total;
            var nayPercent = (float)_nays / total;

            var message = string.Format("The votes are in:\n " +
                "Ayes: {0} ({1:P})\n" +
                "Nays: {2} ({3:P})",
                _ayes, ayePercent, _nays, nayPercent);

            SteamNerd.SendMessage(message, chat, true);

            // 50% of chatters need to vote
            if (total < chatterCount / 2)
            {
                SteamNerd.SendMessage("50% of the chatters need to vote!", chat, true);
                return;
            }

            // and a majority 
            if (_ayes > _nays)
            {
                SteamNerd.SteamFriends.KickChatMember(chat, _kickee);
            }
        }
    }
}
