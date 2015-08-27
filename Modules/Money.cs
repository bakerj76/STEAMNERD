using System;
using System.Collections.Generic;
using System.IO;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Money : Module
    {
        private static string path = @"stats.txt";

        public Dictionary<SteamID, int> _money;
        public Dictionary<SteamID, int> _loans;

        public Money(SteamNerd steamNerd) : base(steamNerd)
        {

        }

        /// <summary>
        /// Save everybody's money
        /// </summary>
        private void Save()
        {
            using (var fileStream = File.Open(path, FileMode.Create))
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
            if (!File.Exists(path))
            {
                File.Create(path);
                return;
            }

            using (var fileStream = File.Open(path, FileMode.Open))
            {
                var reader = new BinaryReader(fileStream);
                uint moneyCount = 0;
                uint loanCount = 0;

                try
                {
                    moneyCount = reader.ReadUInt32();
                }
                catch { }

                try
                {
                    loanCount = reader.ReadUInt32();
                }
                catch { }

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
            return message == "!money";
        }

        
    }
}
