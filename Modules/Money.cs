using System;
using System.Collections.Generic;
using System.IO;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Money : Module
    {
        private const int STARTING_MONEY = 200;
        private static string PATH = @"stats.txt";

        public Dictionary<SteamID, int> _money;
        public Dictionary<SteamID, int> _loans;

        public Money(SteamNerd steamNerd) : base(steamNerd)
        {
            _money = new Dictionary<SteamID, int>();
            _loans = new Dictionary<SteamID, int>();
        }

        /// <summary>
        /// Save everybody's money
        /// </summary>
        private void Save()
        {
            using (var fileStream = File.Open(PATH, FileMode.Create))
            {
                var writer = new BinaryWriter(fileStream);
                writer.Write(_money.Count);
                writer.Write(_loans.Count);

                foreach (var entry in _money)
                {
                    writer.Write(entry.Key.Render());
                    writer.Write(entry.Value);
                }
                
                foreach (var entry in _loans)
                {
                    writer.Write(entry.Key.Render());
                    writer.Write(entry.Value);
                }

                writer.Flush();
            }
        }

        /// <summary>
        /// Load everybody's money
        /// </summary>
        private void Load()
        {
            if (!File.Exists(PATH))
            {
                File.Create(PATH);
                return;
            }

            using (var fileStream = File.Open(PATH, FileMode.Open))
            {
                var reader = new BinaryReader(fileStream);
                uint moneyCount = 0;
                uint loanCount = 0;

                try
                {
                    moneyCount = reader.ReadUInt32();
                    loanCount = reader.ReadUInt32();
                }
                // File is empty
                catch(EndOfStreamException)
                {
                    return;
                }

                for (var i = 0; i < moneyCount; i++)
                {
                    var key = new SteamID(reader.ReadString());
                    var value = reader.ReadInt32();
                    _money[key] = value;
                }

                for (var i = 0; i < loanCount; i++)
                {
                    var key = new SteamID(reader.ReadString());
                    var value = reader.ReadInt32();
                    _loans[key] = value;
                }
            }
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            var message = callback.Message.ToLower();
            return message == "!money" || message == "!loans" || message.StartsWith("!loan") || message.StartsWith("!payback");
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var message = callback.Message.ToLower();
            var chatter = callback.ChatterID;
            var name = SteamNerd.ChatRoomChatters[chatter];

            // Add the player if they aren't in the tables
            if (!_money.ContainsKey(callback.ChatterID))
            {
                _money[callback.ChatterID] = STARTING_MONEY;
            }

            if (!_loans.ContainsKey(callback.ChatterID))
            {
                _loans[callback.ChatterID] = 0;
            }

            if (message == "!money")
            {
                SteamNerd.SendMessage(string.Format("{0} has ${1}", chatter, _money[callback.ChatterID]), callback.ChatRoomID, true);
            }
            else if (message == "!loans")
            {
                SteamNerd.SendMessage(string.Format("{0} has ${1} in loans", chatter, _loans[chatter]), callback.ChatRoomID, true);
            }
            else if (message.StartsWith("!loan"))
            {
                int amount;

                if (!int.TryParse(message.Substring(6), out amount))
                {
                    SteamNerd.SendMessage(string.Format("{0}, that's not a number.", chatter), callback.ChatRoomID, true);
                    return;
                }

                if (amount < 0)
                {
                    SteamNerd.SendMessage(string.Format("{0}, you can't borrow negative money", chatter), callback.ChatRoomID, true);
                    return;
                }

                _money[callback.ChatterID] += amount;
                _loans[callback.ChatterID] += amount;
            }
            else if (message.StartsWith("!payback"))
            {
                int amount;
                if (!int.TryParse(message.Substring(9), out amount))
                {
                    SteamNerd.SendMessage(string.Format("{0}, that's not a number.", chatter), callback.ChatRoomID, true);
                    return;
                }

                if (amount < 0)
                {
                    SteamNerd.SendMessage(string.Format("{0}, you can't payback negative money", chatter), callback.ChatRoomID, true);
                    return;
                }

                var borrowed = _loans[callback.ChatterID];
                var money = _money[callback.ChatterID];

                if (borrowed == 0)
                {
                    SteamNerd.SendMessage(string.Format("{0}, you don't have any loans!", chatter), callback.ChatRoomID, true);
                    return;
                }

                if (amount > borrowed)
                {
                    amount = borrowed;
                }

                if (money < amount)
                {
                    SteamNerd.SendMessage(string.Format("{0}, you don't have ${1}!", chatter, amount), callback.ChatRoomID, true);
                    return;
                }

                _loans[callback.ChatterID] -= amount;
                _money[callback.ChatterID] -= amount;

                SteamNerd.SendMessage(string.Format("{0} paid back ${1}.", chatter, amount), callback.ChatRoomID, true);
            }
        }

    }
}
