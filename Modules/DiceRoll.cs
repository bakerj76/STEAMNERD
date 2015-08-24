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
        private static Regex _regex = new Regex(@"\d*d\d+", RegexOptions.Compiled);

        public DiceRoll(SteamNerd steamNerd) : base(steamNerd)
        {
        }

        public static bool IsDiceRoll(string message)
        {
            return _regex.IsMatch(message);
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return IsDiceRoll(callback.Message);
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var message = _regex.Match(callback.Message).Value;
            int numDice, sides;

            if (!ParseString(message, out numDice, out sides))
            {
                return;
            }

            var rolls = GetRolls(numDice, sides);
            var rollStr = GetStringFromRolls(rolls);

            SteamNerd.SendMessage(rollStr, callback.ChatRoomID, true);
        }

        /// <summary>
        /// Parses the message and returns the number of rolls of the die and the number of faces of that die
        /// </summary>
        /// <param name="message">The message to be parsed</param>
        /// <param name="numDice">The number of rolls</param>
        /// <param name="faces">The number of faces</param>
        /// <returns>Did it work?</returns>
        public static bool ParseString(string message, out int numDice, out int faces)
        {
            var split = Regex.Split(message, "d");
            numDice = 0;
            faces = 0;

            if (split[0] == "")
            {
                numDice = 1;
            }
            else if (!int.TryParse(split[0], out numDice) || numDice == 0 || numDice > 1000)
            {
                return false;
            }

            if (!int.TryParse(split[1], out faces) || faces == 0 || faces >= int.MaxValue - 1)
            {
                return false;
            }

            return true;
        }

        public static int[] GetRolls(int numDice, int sides)
        {
            var rand = new Random();
            var rolls = new int[numDice];

            for (var i = 0; i < numDice; i++)
            {
                rolls[i] = rand.Next(1, sides + 1);
            }

            return rolls;
        }

        public static string GetStringFromRolls(int[] rolls)
        {
            var rollStr = "";

            if (rolls.Length > 1)
            {
                rollStr = rolls.Select(roll => roll.ToString())
                    .Aggregate((current, roll) => string.Format("{0} + {1}", current, roll));
                try
                {
                    rollStr += string.Format(" = {0}", rolls.Sum());
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }
            else
            {
                rollStr = rolls[0].ToString();
            }

            return rollStr;
        }
    }
}
