using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class TrollSlayer : Module
    {
        private Random _rand;
        private List<SteamID> _kickable;
        private Dictionary<SteamID, Stopwatch> _cooldowns;

        public TrollSlayer(SteamNerd steamNerd) : base(steamNerd)
        {
            _rand = new Random();
            _kickable = new List<SteamID>();
            _cooldowns = new Dictionary<SteamID, Stopwatch>();

            foreach (var chatter in SteamNerd.ChatterNames.Keys)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Reset();
                _cooldowns[chatter] = new Stopwatch();

                _kickable.Add(chatter);
            }
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return callback.Message.ToLower() == "!slaytroll";
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var twoMinutes = TimeSpan.FromMinutes(2);

            // If they aren't in _cooldowns, don't let them kick
            if (!_cooldowns.ContainsKey(callback.ChatterID))
            {
                return;
            }
            var timer = _cooldowns[callback.ChatterID];

            // If they're on cooldown, tell them
            if (timer.IsRunning && timer.Elapsed < twoMinutes)
            {
                var timeLeft = twoMinutes - timer.Elapsed;
                var minutes = timeLeft.Minutes;
                var seconds = timeLeft.Seconds;
                var chatter = SteamNerd.ChatterNames[callback.ChatterID];
                var messageString = "{0}. Listen up, punk. You're on cooldown, buddy. Wait ";
                var minutesString = minutes == 0 ? "" : ("{1} minute " + (minutes != 1 ? "s" : ""));
                var secondsString = (minutesString == "" ? "" : "and ") + "{2} second" + (seconds != 1 ? "s." : ".");
                

                var message = string.Format(messageString + minutesString + secondsString, chatter, minutes, seconds);
                SteamNerd.SendMessage(message, callback.ChatRoomID, true);

                return;
            }

            var numKeys = _kickable.Count;
            var troll = _kickable[_rand.Next(numKeys)];

            // Don't kick yourself
            while (troll == SteamNerd.SteamUser.SteamID)
            {
                troll = _kickable[_rand.Next(numKeys)];
            }

            // Put this punk on cooldown
            timer.Reset();
            timer.Start();

            SteamNerd.SendMessage(string.Format("SLAYING TROLL: {0}", SteamNerd.ChatterNames[troll]), callback.ChatRoomID, true);
            
            SteamNerd.SteamFriends.KickChatMember(callback.ChatRoomID, troll);
        }

        public override void OnFriendChatEnter(SteamFriends.PersonaStateCallback callback)
        {
            if (!_cooldowns.Keys.Contains(callback.FriendID))
            {
                _cooldowns[callback.FriendID] = new Stopwatch();
            }

            if (!_kickable.Contains(callback.FriendID))
            {
                _kickable.Add(callback.FriendID);
            }
        }

        public override void OnFriendChatLeave(SteamFriends.ChatMemberInfoCallback callback)
        {
            var chatterID = callback.StateChangeInfo.ChatterActedOn;

            Console.WriteLine("Removing {0} from troll list.", SteamNerd.ChatterNames[chatterID]);
            _kickable.Remove(chatterID);

            foreach (var steamID in _kickable)
            {
                Console.WriteLine(SteamNerd.ChatterNames[steamID]);
            }
        }
    }
}
