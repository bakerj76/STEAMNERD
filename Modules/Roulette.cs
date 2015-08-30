using System;
using System.Collections.Generic;
using System.Timers;
using System.IO;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Roulette : Module
    {
        private Money _moneyModule;

        private List<Bet> _bets;
        private List<SteamID> _players;
        private int _currentSpinner;
        private Random _rand;

        private int _magicNumber;
        private int _currentBet;
        private int _pool;

        private bool _inProgress;
        private bool _betTimerOver;
        private bool _spinning;

        private Timer _betTimer;
        private Timer[] _countdown;
        private Timer _delay;

        public struct Bet
        {
            public SteamID Better;
            public SteamID Side;
            public int Money;
        }

        public Roulette(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Roulette";
            Description = "A friendly game of roulette.";

            AddCommand(
                "enter",
                string.Format("Enters the roulette game. Usage: {0}enter [money] or {0}enter", SteamNerd.CommandChar),
                Enter
            );

            AddCommand(
                "bet",
                string.Format("Bet on a person in an ongoing roulette game. Usage: {0}bet [player] [money]", SteamNerd.CommandChar),
                DoTheBet
            );

            AddCommand(
                "spin",
                "Spin!",
                Spin
            );

            _moneyModule = (Money)SteamNerd.GetModule("Money");
            _rand = new Random();
            Reset();
            _countdown = new Timer[3];
        }

        public void Enter(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chatter = callback.ChatterID;
            var name = SteamNerd.ChatterNames[chatter];
            var chat = callback.ChatRoomID;

            // If someone enters at the right time (less than 2 players and a game isn't in progress)
            if (_players.Count < 2 && !_inProgress)
            {
                if (_players.Contains(callback.ChatterID))
                {
                    SteamNerd.SendMessage(string.Format("{0}, you're already playing. Dumbass.", name), chat, true);
                    return;
                }

                if (_players.Count == 0)
                {
                    if (args.Length < 2)
                    {
                        SteamNerd.SendMessage(string.Format("Usage: {0}enter [money]", SteamNerd.CommandChar), chat, true);
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[1], out amount))
                    {
                        SteamNerd.SendMessage(string.Format("{0}, that isn't a number.", name), chat, true);
                        return;
                    }

                    if (amount <= 0)
                    {
                        SteamNerd.SendMessage(string.Format("{0}, you gotta bet more than $0.", name), chat, true);
                        return;
                    }

                    if (_moneyModule.GetPlayerMoney(chatter) < amount)
                    {
                        SteamNerd.SendMessage(string.Format("{0}, you don't have that kind of money!", name), chat, true);
                        return;
                    }

                    _currentBet = amount;
                    _moneyModule.AddMoney(chatter, chat, -amount);
                    SteamNerd.SendMessage(string.Format("{0} bet ${1}. Type {2}enter to join and match it.", name, amount, SteamNerd.CommandChar), chat, true);
                    _players.Add(chatter);
                }
                else if (_players.Count == 1)
                {
                    if (_moneyModule.GetPlayerMoney(chatter) < _currentBet)
                    {
                        SteamNerd.SendMessage(string.Format("{0}, you don't have ${1}!", name, _currentBet), chat, true);
                        return;
                    }

                    _moneyModule.AddMoney(chatter, chat, -_currentBet);
                    
                    SteamNerd.SendMessage(string.Format("{0} has entered, matching ${1}. How brave...", name, _currentBet), chat, true);

                    _inProgress = true;
                    _players.Add(callback.ChatterID);
                    StartBetting(callback.ChatRoomID);
                }
            }

            // Someone enters at the WRONG time
            else
            {
                SteamNerd.SendMessage(string.Format("There are already two players, {0}. Good job, idiot.", chatter), callback.ChatRoomID, true);
            }
        }

        public void Spin(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_inProgress && _betTimerOver && !_spinning && callback.ChatterID == _players[_currentSpinner])
            {
                PlayerSpin(callback.ChatterID, callback.ChatRoomID);
            }
        }

        public void DoTheBet(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chat = callback.ChatRoomID;
            var chatter = callback.ChatterID;
            var name = SteamNerd.ChatterNames[chatter];

            // If someone bets early
            if (!_inProgress)
            {
                SteamNerd.SendMessage(string.Format("There's no match to bet on, {0}. Good job, idiot.", name), chat, true);
            }
            else if (!_betTimerOver)
            {
                if (args.Length < 3)
                {
                    SteamNerd.SendMessage(string.Format("Usage: {0}bet [player] [money]", SteamNerd.CommandChar), chat, true);
                    return;
                }

                int bet;

                if (!int.TryParse(args[2], out bet))
                {
                    SteamNerd.SendMessage(string.Format("Usage: {0}bet [player] [money]", SteamNerd.CommandChar), chat, true);
                    return;
                }

                AddBet(callback.ChatterID, callback.ChatRoomID, args[1], bet);
            }
        }

        private void StartBetting(SteamID chatroom)
        {
            // Get names
            var player1 = SteamNerd.ChatterNames[_players[0]];
            var player2 = SteamNerd.ChatterNames[_players[1]];

            // Set flags
            _betTimerOver = false;
            _inProgress = true;

            // Calculate the player pool
            _pool = _currentBet * 2;

            var startMessage = string.Format("Wow! {0} and {1} are going head to head!\n" +
                "Place your bets using !bet [player] [money].\n" +
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
            var player1 = SteamNerd.ChatterNames[_players[0]];
            var player2 = SteamNerd.ChatterNames[_players[1]];

            _magicNumber = _rand.Next(1, 7);
            _currentSpinner = 0;
            _betTimerOver = true;
            SteamNerd.SendMessage(string.Format("Roulette has started!"), chatroom, true);
            SteamNerd.SendMessage(string.Format("{0} goes first...", player1), chatroom, true);
        }

        private void PlayerSpin(SteamID chatter, SteamID chat)
        {
            _spinning = true;
            var name = SteamNerd.ChatterNames[chatter];
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
            var name = SteamNerd.ChatterNames[chatter];
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
            var name = SteamNerd.ChatterNames[player];

            SteamNerd.SendMessage(string.Format("It's {0}'s turn to spin", name), chat, true);
        }

        private void EndGame(SteamID chat)
        {
            var winner = _currentSpinner + 1 < _players.Count ? _currentSpinner + 1 : 0;
            var player = _players[winner];
            var name = SteamNerd.ChatterNames[player];

            SteamNerd.SendMessage(string.Format("{0} wins! They win ${1}", name, _pool), chat, true);
            _moneyModule.AddMoney(player, chat, _pool);

            foreach(var bet in _bets)
            {
                if (bet.Side == player)
                {
                    var better = bet.Better;
                    var betterName = SteamNerd.ChatterNames[better];
                    var amount = bet.Money * 2;
                    SteamNerd.SendMessage(string.Format("{0} wins ${1}", betterName, amount), chat, true);

                    _moneyModule.AddMoney(better, chat, amount);
                }
            }
            Reset();
        }

        private void AddBet(SteamID chatter, SteamID chat, string side, int bet)
        {
            var chatterName = SteamNerd.ChatterNames[chatter];

            if (bet <= 0)
            {
                SteamNerd.SendMessage(string.Format("{0}, you gotta bet more than $0.", chatterName), chat, true);
                return;
            }

            if (_moneyModule.GetPlayerMoney(chatter) < bet)
            {
                SteamNerd.SendMessage(string.Format("{0}, you don't have ${1}", chatterName, bet), chat, true);
                return;
            }

            foreach (var player in _players)
            {
                var name = SteamNerd.ChatterNames[player].ToLower();

                if (name.Contains(side))
                {
                    _moneyModule.AddMoney(chatter, chat, -bet);
                    _bets.Add(new Bet { Better = chatter, Side = player, Money = bet });
                    return;
                }
            }

            SteamNerd.SendMessage(string.Format("Can't find player {0}", side), chat, true);
        }

        private void Reset()
        {
            _currentBet = 0;
            _bets = new List<Bet>();
            _players = new List<SteamID>();

            _pool = 0;

            _inProgress = false;
            _betTimerOver = false;
            _spinning = false;
        }
    }
}
