using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class TrollSlayer : Module
    {
        private Random _rand;
        private Dictionary<SteamID, Stopwatch> _cooldowns;

        public TrollSlayer(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Troll Slayer";
            Description = "Slays trolls.";

            RegisterCommand(
                "slaytroll",
                "Slays the troll in this chatroom.",
                SlayTroll
            );

            _rand = new Random();
            _cooldowns = new Dictionary<SteamID, Stopwatch>();

            foreach (var chatter in SteamNerd.ChatterNames.Keys)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Reset();
                _cooldowns[chatter] = new Stopwatch();
            }
        }

        public void SlayTroll(SteamFriends.ChatMsgCallback callback, string[] args)
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

            var chatters = SteamNerd.ChatterNames.Keys.ToList();

            // Don't kick yourself
            chatters.Remove(SteamNerd.SteamUser.SteamID);
            
            var numKeys = chatters.Count;
            var troll = chatters[_rand.Next(numKeys)];

            // Put this punk on cooldown
            timer.Reset();
            timer.Start();

            SteamNerd.SendMessage(string.Format("SLAYING TROLL: {0}", SteamNerd.ChatterNames[troll]), callback.ChatRoomID, true);

            var delay = new Timer(1000);
            delay.AutoReset = false;
            delay.Elapsed += (src, e) => 
            {
                SteamNerd.SteamFriends.KickChatMember(callback.ChatRoomID, troll);
                (src as Timer).Dispose();
            };

            delay.Start();
        }

        public override void OnFriendChatEnter(SteamFriends.PersonaStateCallback callback)
        {
            if (!_cooldowns.Keys.Contains(callback.FriendID))
            {
                _cooldowns[callback.FriendID] = new Stopwatch();
            }
        }
    }
}
