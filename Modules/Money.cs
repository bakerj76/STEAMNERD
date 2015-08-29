using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Timers;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Money : Module
    {
        private const float SAVE_TIME = 30000f;
        private const int STARTING_MONEY = 200;
        private const string PATH = @"stats.txt";

        private Dictionary<SteamID, int> _money;
        private Dictionary<SteamID, int> _loans;

        private bool _changed;
        private Timer _saveTimer;

        public Money(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Money";
            Description = "Handles player money.";

            AddCommand(
                "money",
                "View how much money you have.",
                PrintMoney
            );

            AddCommand(
                "loans",
                "View how much you're in debt.",
                PrintLoans
            );

            AddCommand(
                "loan",
                string.Format("Get a loan. Usage: {0}loan [money]", SteamNerd.CommandChar),
                GetLoan
            );


            AddCommand(
                "payback",
                string.Format("Payback your debt. Usage: {0}payback [money]", SteamNerd.CommandChar),
                Payback
            );
            
            _money = new Dictionary<SteamID, int>();
            _loans = new Dictionary<SteamID, int>();

            _changed = false;
            _saveTimer = new Timer(SAVE_TIME);
            _saveTimer.Elapsed += (src, e) => Save();
            _saveTimer.Start();

            Load();
        }

        /// <summary>
        /// Save everybody's money
        /// </summary>
        private void Save()
        {
            // Was there a change in money?
            if (_changed)
            {
                using (var fileStream = File.Open(PATH, FileMode.Create))
                {
                    var writer = new BinaryWriter(fileStream);
                    writer.Write(_money.Count);

                    for (var i = 0; i < _money.Count; i++)
                    {
                        var key = _money.Keys.ElementAt(i);
                        writer.Write(key.Render());
                        writer.Write(_money[key]);
                        writer.Write(_loans[key]);
                    }

                    writer.Flush();
                }

                _changed = false;
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
                uint count = 0;

                try
                {
                    count = reader.ReadUInt32();
                }
                // File is empty
                catch(EndOfStreamException)
                {
                    return;
                }

                for (var i = 0; i < count; i++)
                {
                    var key = new SteamID(reader.ReadString());
                    _money[key] = reader.ReadInt32();
                    _loans[key] = reader.ReadInt32();
                }
            }
        }

        public void PrintMoney(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chatter = callback.ChatterID;
            var name = SteamNerd.ChatterNames[chatter];

            if (!_money.ContainsKey(chatter))
            {
                AddSteamID(chatter);
            }

            SteamNerd.SendMessage(string.Format("{0} has ${1}", name, _money[callback.ChatterID]), callback.ChatRoomID, true);
        }

        public void PrintLoans(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chatter = callback.ChatterID;
            var name = SteamNerd.ChatterNames[chatter];

            if (!_money.ContainsKey(chatter))
            {
                AddSteamID(chatter);
            }

            SteamNerd.SendMessage(string.Format("{0} has ${1} in loans", name, _loans[chatter]), callback.ChatRoomID, true);
        }

        public void GetLoan(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (args.Length < 2)
            {
                SteamNerd.SendMessage(string.Format("Usage: {0}loan [money]", SteamNerd.CommandChar), callback.ChatRoomID, true);
                return;
            }

            var chatter = callback.ChatterID;
            
            if (!_money.ContainsKey(chatter))
            {
                AddSteamID(chatter);
            }

            var name = SteamNerd.ChatterNames[chatter];
            var money = _money[chatter];
            var loans = _loans[chatter];
            int amount;

            if (!int.TryParse(args[1], out amount))
            {
                SteamNerd.SendMessage(string.Format("{0}, that's not a number", name), callback.ChatRoomID, true);
                return;
            }

            if (amount < 0)
            {
                SteamNerd.SendMessage(string.Format("{0}, you can't borrow negative money", name), callback.ChatRoomID, true);
                return;
            }

            AddMoney(chatter, callback.ChatRoomID, amount);
            AddLoan(chatter, callback.ChatRoomID, amount);
        }

        public void Payback(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chatter = callback.ChatterID;

            if (args.Length < 2)
            {
                SteamNerd.SendMessage(string.Format("Usage: {0}payback [money]", SteamNerd.CommandChar), callback.ChatRoomID, true);
                return;
            }

            if (!_money.ContainsKey(chatter))
            {
                AddSteamID(chatter);
            }

            var name = SteamNerd.ChatterNames[chatter];
            var money = _money[chatter];
            var loans = _loans[chatter];
            long amount;

            if (loans == 0)
            {
                SteamNerd.SendMessage(string.Format("{0}, you don't have any loans to payback!", name), callback.ChatRoomID, true);
                return;
            }

            if (!long.TryParse(args[1], out amount))
            {
                SteamNerd.SendMessage(string.Format("{0}, that's not a number", name), callback.ChatRoomID, true);
                return;
            }

            if (amount < 0)
            {
                SteamNerd.SendMessage(string.Format("{0}, you can't payback negative money", name), callback.ChatRoomID, true);
                return;
            }

            if (amount > loans)
            {
                amount = loans;
            }

            if (amount > money)
            {
                SteamNerd.SendMessage(string.Format("{0}, you don't have ${1}!", name, amount), callback.ChatRoomID, true);
                return;
            }

            AddMoney(chatter, callback.ChatRoomID, -(int)amount);
            AddLoan(chatter, callback.ChatRoomID, -(int)amount);
        }

        private void AddSteamID(SteamID steamID)
        {
            _money[steamID] = STARTING_MONEY;
            _loans[steamID] = 0;
        }

        public void AddMoney(SteamID steamID, SteamID chat, int amount)
        {
            var name = SteamNerd.ChatterNames[steamID];
            var money = _money[steamID];

            try
            {
                int test = checked(money + amount);
            }
            catch (OverflowException)
            {
                SteamNerd.SendMessage(string.Format("Whoa, {0}, you've got way too much money", name), chat, true);
                return;
            }

            _money[steamID] += amount;
            _changed = true;
        }

        public void AddLoan(SteamID steamID, SteamID chat, int amount)
        {
            var name = SteamNerd.ChatterNames[steamID];
            var loans = _loans[steamID];

            try
            {
                int test = checked(loans + amount);
            }
            catch (OverflowException)
            {
                SteamNerd.SendMessage(string.Format("Whoa, {0}, you've borrowed way too much", name), chat, true);
                return;
            }

            _loans[steamID] += amount;
            _changed = true;
        }

        public int GetPlayerMoney(SteamID steamID)
        {
            if (!_money.ContainsKey(steamID))
            {
                AddSteamID(steamID);
            }

            return _money[steamID];
        }

        public int GetPlayerLoans(SteamID steamID)
        {
            if (!_money.ContainsKey(steamID))
            {
                AddSteamID(steamID);
            }

            return _loans[steamID];
        }
    }
}
