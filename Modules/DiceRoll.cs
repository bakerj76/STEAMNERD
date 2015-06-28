using System;
using System.Linq;
using System.Text.RegularExpressions;
using SteamKit2;

namespace STEAMNERD.Modules
{
    /// <summary>
    /// Prints dice rolls to chat
    /// Usage: ![x]d[y] where x is the number of rolls and y is the sides
    /// </summary>
    class DiceRoll : Module
    {
        private readonly Random _rand;

        public DiceRoll(SteamNerd steamNerd) : base(steamNerd)
        {
            _rand = new Random();
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return Regex.IsMatch(callback.Message, @"^!\d+d\d");
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var message = callback.Message;
            var steamid = callback.ChatterID;

            var split = Regex.Split(message, "[!d]");
            int numDice, sides;

            if (split[1] == "")
            {
                numDice = 1;
            }
            else if (!int.TryParse(split[1], out numDice) || numDice == 0 || numDice > 1000) return;
            if (!int.TryParse(split[2], out sides) || sides == 0 || sides >= int.MaxValue - 1) return;

            var rolls = new int[numDice];

            for (var i = 0; i < numDice; i++)
            {
                rolls[i] = _rand.Next(1, sides + 1);
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
                catch (Exception)
                {
                    SteamNerd.SendMessage(string.Format("Wow, cool, overflow... Very nice {0}", 
                        steamid.Render()), callback.ChatRoomID, true);
                    return;
                }
            }
            else
            {
                rollStr = rolls[0].ToString();
            }

            SteamNerd.SendMessage(rollStr, callback.ChatRoomID, true);
        }
    }
}
