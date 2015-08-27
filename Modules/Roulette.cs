using System;
using System.Collections.Generic;
using System.Timers;
using System.IO;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Roulette : Module
    {
        private static string path = @"stats.txt";
        private const int STARTING_MONEY = 200;

        private Dictionary<SteamID, int> _money;
        private Dictionary<SteamID, int> _loans;
        private List<Bet> _bets;
        private List<SteamID> _players;
        private int _currentSpinner;
        private Random _rand;

        private int _magicNumber;
        private int _currentBet;
        private int _pool;

        private bool _isInProgress;
        private bool _betTimerOver;
        private bool _spinning;

        private Timer _betTimer;
        private Timer[] _countdown;
        private Timer _delay;

        private struct Bet
        {
            public SteamID Better;
            public SteamID Side;
            public int Money;
        }

        public Roulette(SteamNerd steamNerd) : base(steamNerd)
        {
            _rand = new Random();
            _money = new Dictionary<SteamID, int>();
            _loans = new Dictionary<SteamID, int>();
            Reset();
            Load();
            _countdown = new Timer[3];
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

                foreach (var entry in _money)
                {
                    writer.Write(entry.Key.Render());
                    writer.Write(entry.Value);
                }

                writer.Write(_loans.Count);

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

                for (var i = 0; i < moneyCount; i++)
                {
                    var key = new SteamID(reader.ReadString());
                    var value = reader.ReadInt32();
                    _money[key] = value;
                }

                try
                {
                    loanCount = reader.ReadUInt32();
                }
                catch { }

                for (var i = 0; i < loanCount; i++)
                {
                    var key = new SteamID(reader.ReadString());
                    var value = reader.ReadInt32();
                    _loans[key] = value;
                }
            }
        }

        private void Reset()
        {
            _currentBet = 0;
            _bets = new List<Bet>();
            _players = new List<SteamID>();

            _pool = 0;

            _isInProgress = false;
            _betTimerOver = false;
            _spinning = false;
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            var message = callback.Message.ToLower();
            return message.StartsWith("!bet") || message.StartsWith("!enter") || message == "!money" || message == "!spin" ||
                message.StartsWith("!loan ") || message == "!loans" || message.StartsWith("!payback");
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var chatter = SteamNerd.ChatRoomChatters[callback.ChatterID];
            var message = callback.Message.ToLower();
            var isEntry = message.StartsWith("!enter");

            // Add the player to the game if they haven't played before
            if (!_money.ContainsKey(callback.ChatterID))
            {
                _money[callback.ChatterID] = STARTING_MONEY;
            }

            if (!_loans.ContainsKey(callback.ChatterID))
            {
                _loans[callback.ChatterID] = 0;
            }

            if (message == "!loans")
            {
                var loan = _loans[callback.ChatterID];
                SteamNerd.SendMessage(string.Format("{0} has ${1} in loans", chatter, loan), callback.ChatRoomID, true);
                return;
            }

            else if (message.StartsWith("!loan "))
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
                return;
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
                return;
            }

            // Check money
            else if (message == "!money")
            {
                SteamNerd.SendMessage(string.Format("{0} has ${1}", chatter, _money[callback.ChatterID]), callback.ChatRoomID, true);
                return;
            }

            // Check if this is the correct person to be spinning at the correct time
            else if (message == "!spin" && _isInProgress && _betTimerOver && !_spinning && callback.ChatterID == _players[_currentSpinner])
            {
                Spin(callback.ChatterID, callback.ChatRoomID);
                return;
            }
            
            // Check if someone bets early
            else if (!isEntry && !_isInProgress)
            {
                SteamNerd.SendMessage(string.Format("There's no match to bet on, {0}. Good job, idiot.", chatter), callback.ChatRoomID, true);
                return;
            }

            // Check if someone bets during betting time
            else if (!isEntry && !_betTimerOver)
            {
                var split = message.Split(' ');
                int bet;

                if (!int.TryParse(split[2], out bet) || split.GetLength(0) < 2)
                {
                    SteamNerd.SendMessage(string.Format("{0}, do !bet [player] [amount]", chatter), callback.ChatRoomID, true);
                    return;
                }

                AddBet(callback.ChatterID, callback.ChatRoomID, split[1], bet);
            }

            // Check if someone enters
            else if (isEntry && _players.Count < 2 && !_isInProgress)
            {
                if (_players.Contains(callback.ChatterID))
                {
                    SteamNerd.SendMessage(string.Format("{0}, you're already playing. Dumbass.", chatter), callback.ChatRoomID, true);
                    return;
                }

                if (_players.Count == 0)
                {
                    int amount;
                    if (!int.TryParse(message.Substring(6), out amount))
                    {
                        SteamNerd.SendMessage(string.Format("{0}, that isn't a number.", chatter), callback.ChatRoomID, true);
                        return;
                    }

                    if (amount <= 0)
                    {
                        SteamNerd.SendMessage(string.Format("{0}, you gotta bet more than $0.", chatter), callback.ChatRoomID, true);
                        return;
                    }

                    if (_money[callback.ChatterID] < amount)
                    {
                        SteamNerd.SendMessage(string.Format("{0}, you don't have that kind of money!", chatter), callback.ChatRoomID, true);
                        return;
                    }

                    _money[callback.ChatterID] -= amount;
                    _currentBet = amount;
                    SteamNerd.SendMessage(string.Format("{0} bet ${1}. Type !enter to join and match it.", chatter, amount), callback.ChatRoomID, true);
                    _players.Add(callback.ChatterID);
                }
                else if (_players.Count == 1)
                {
                    if (_money[callback.ChatterID] < _currentBet)
                    {
                        SteamNerd.SendMessage(string.Format("{0}, you don't have ${1}!", chatter, _currentBet), callback.ChatRoomID, true);
                        return;
                    }

                    _money[callback.ChatterID] -= _currentBet;
                    SteamNerd.SendMessage(string.Format("{0} has entered. How brave...", chatter), callback.ChatRoomID, true);
                    _isInProgress = true;
                    _players.Add(callback.ChatterID);
                    StartBet(callback.ChatRoomID);
                }
            }

            // Check if someone enters when there are too many players
            else if (isEntry)
            {
                SteamNerd.SendMessage(string.Format("There are already two players, {0}. Good job, idiot.", chatter), callback.ChatRoomID, true);
                return;
            }
            
        }

        private void StartBet(SteamID chatroom)
        {
            // Get names
            var player1 = SteamNerd.ChatRoomChatters[_players[0]];
            var player2 = SteamNerd.ChatRoomChatters[_players[1]];

            // Set flags
            _betTimerOver = false;
            _isInProgress = true;

            // Calculate the player pool
            _pool = _currentBet * 2;

            var startMessage = string.Format("Wow! {0} and {1} are going head to head!\n" +
                "Place your bets using !bet [player] [amount].\n" +
                "You have 30 seconds. All bets are double or nothing.", player1, player2);
            SteamNerd.SendMessage(startMessage, chatroom, true);

            _betTimer = new Timer(30000);
            _betTimer.Elapsed += (src, e) => { StartRoulette(chatroom); };
            _betTimer.AutoReset = false;
            _betTimer.Start();

            for (int i = 3; i > 0; i--)
            {
                var timer = _countdown[i - 1];
                var countdownString = string.Format("{0}...", i);

                timer = new Timer(30000 - i * 1000);
                timer.AutoReset = false;
                timer.Elapsed += (src, e) => SteamNerd.SendMessage(countdownString, chatroom, true);;
                timer.Start();
            }
        }

        private void StartRoulette(SteamID chatroom)
        {
            var player1 = SteamNerd.ChatRoomChatters[_players[0]];
            var player2 = SteamNerd.ChatRoomChatters[_players[1]];

            _magicNumber = _rand.Next(1, 7);
            _currentSpinner = 0;
            _betTimerOver = true;
            SteamNerd.SendMessage(string.Format("Roulette has started!"), chatroom, true);
            SteamNerd.SendMessage(string.Format("{0} goes first...", player1), chatroom, true);
        }

        private void Spin(SteamID chatter, SteamID chat)
        {
            _spinning = true;
            var name = SteamNerd.ChatRoomChatters[chatter];
            var spin = _rand.Next(1, 7);
            
            SteamNerd.SendMessage(string.Format("{0} spins the barrel...", name), chat, true);


            var message = spin == _magicNumber ? "BOOM!" : "Click.";
            _delay = new Timer(3000);
            _delay.AutoReset = false;
            _delay.Start();
            _delay.Elapsed += (src, e) => { Delay(spin == _magicNumber, message, chatter, chat); };
        }

        private void Delay(bool loser, string message, SteamID chatter, SteamID chat)
        {
            _spinning = false;
            var name = SteamNerd.ChatRoomChatters[chatter];
            SteamNerd.SendMessage(message, chat, true);

            if (loser)
            {
                SteamNerd.SteamFriends.KickChatMember(chat, chatter);
                EndGame(chat);
            }
            else
            {
                SteamNerd.SendMessage(string.Format("{0} lives... for now.", name), chat, true);
                KeepSpinning(chat);
            }
        }

        private void KeepSpinning(SteamID chat)
        {
            _currentSpinner = _currentSpinner + 1 < _players.Count ? _currentSpinner + 1 : 0;

            var player = _players[_currentSpinner];
            var name = SteamNerd.ChatRoomChatters[player];

            SteamNerd.SendMessage(string.Format("It's {0}'s turn to spin", name), chat, true);
        }

        private void EndGame(SteamID chat)
        {
            var winner = _currentSpinner + 1 < _players.Count ? _currentSpinner + 1 : 0;
            var player = _players[winner];
            var name = SteamNerd.ChatRoomChatters[player];

            SteamNerd.SendMessage(string.Format("{0} wins! They win ${1}", name, _pool), chat, true);
            _money[player] += _pool;

            foreach(var bet in _bets)
            {
                if (bet.Side == player)
                {
                    var better = SteamNerd.ChatRoomChatters[bet.Better];
                    var amount = bet.Money * 2;
                    SteamNerd.SendMessage(string.Format("{0} wins ${1}", better, amount), chat, true);

                    _money[bet.Better] += amount;
                }
            }

            Save();
            Reset();
        }

        private void AddBet(SteamID chatter, SteamID chat, string side, int bet)
        {
            var chatterName = SteamNerd.ChatRoomChatters[chatter];

            if (bet <= 0)
            {
                SteamNerd.SendMessage(string.Format("{0}, you gotta bet more than $0.", chatterName), chat, true);
                return;
            }

            if (_money[chatter] < bet)
            {
                SteamNerd.SendMessage(string.Format("{0}, you don't have ${1}", chatterName, bet), chat, true);
                return;
            }

            foreach (var player in _players)
            {
                var name = SteamNerd.ChatRoomChatters[player].ToLower();

                if (name.Contains(side))
                {
                    _money[chatter] -= bet;
                    _bets.Add(new Bet { Better = chatter, Side = player, Money = bet });
                    return;
                }
            }

            SteamNerd.SendMessage(string.Format("Can't find player {0}", side), chat, true);
        }
    }
}
