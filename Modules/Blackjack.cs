using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Threading;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Blackjack : Module
    {
        private float PREROUND_TIMER = 30 * 1000;
        private const string SUIT_CHARS = "♣♦♥♠";

        public enum State { NoGame, WaitingForPlayers, Betting, Starting, PlayerTurn, DealerTurn }
        public enum Suit { Clubs, Diamonds, Hearts, Spades }
        public enum Rank { Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }
        public enum HandState { None, Stand, DoubleDown, Surrender, Blackjack, Bust, AceSplit, Charlie }

        private Money _moneyModule;
        private Random _rand;

        private bool _canInsure;
        private State _gameState;

        private int _betsPlaced;

        private List<SteamID> _waiting;
        private Dictionary<SteamID, Player> _players;
        private Hand _dealerHand;

        private System.Timers.Timer _joinTimer;
        private System.Timers.Timer _preRoundTimer;
        private System.Timers.Timer[] _countdown;

        private List<Card> _deck;
        private int _deckPos;

        public class Player
        {
            public int Bet;
            public List<Hand> Hands;
            public bool HasInsurance;

            public Player()
            {
                Bet = 0;
                Hands = new List<Hand>();
                HasInsurance = false;
            }
        }

        public class Hand
        {
            public List<Card> Cards;
            public HandState State;

            public Hand()
            {
                Cards = new List<Card>();
                State = HandState.None;
            }

            public int GetValue()
            {
                var total = Cards.Sum(card => card.GetValue());

                foreach (var card in Cards)
                {
                    if (total > 21 && card.Rank == Rank.Ace)
                    {
                        total -= 10;
                    }
                }

                return total;
            }

            public override string ToString()
            {
                var handString = "";

                foreach (var card in Cards)
                {
                    handString += card + " ";
                }

                return handString.Trim();
            }
        }

        public struct Card
        {
            public Suit Suit;
            public Rank Rank;

            public int GetValue()
            {
                switch (Rank)
                {
                    case Rank.Ace:
                        return 11;
                    case Rank.Jack:
                    case Rank.Queen:
                    case Rank.King:
                        return 10;
                    default:
                        return (int)Rank;
                }
            }

            public override string ToString()
            {
                var rankString = "";

                switch (Rank)
                {
                    case Rank.Ace:
                        rankString += "A";
                        break;
                    case Rank.Jack:
                        rankString += "J";
                        break;
                    case Rank.Queen:
                        rankString += "Q";
                        break;
                    case Rank.King:
                        rankString += "K";
                        break;
                    default:
                        rankString += (int)Rank;
                        break;
                }

                return rankString + SUIT_CHARS[(int)Suit];
            }
        }

        public Blackjack(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Blackjack";
            Description = "The card game.";

            AddCommand(
                "blackjack",
                "Enter the game of blackjack!",
                BlackjackCommands
            );

            AddCommand(
                "bj",
                "",
                BlackjackCommands
            );

            AddCommand(
                "blackjack bet",
                "Place your bets if you're playing blackjack.",
                null
            );

            AddCommand(
                "hit",
                "Add a card to your hand.",
                Hit
            );

            AddCommand(
                "stand",
                "Stick with your hand.",
                Stand
            );

            AddCommand(
                "double",
                "Double your bet, hit, then stand",
                DoubleDown
            );

            AddCommand(
                "split",
                "If your cards have the same value, split your hand into two hands.",
                Split
            );

            AddCommand(
                "surrender",
                "Quit and get half your bet back.",
                Surrender
            );

            AddCommand(
                "insurance",
                "Make a sidebet of half your bet that the dealer has a blackjack.",
                Insure
            );

            AddCommand(
                "hand",
                "Look at your hand.",
                (callback, args) =>
                {
                    if (_players.ContainsKey(callback.ChatterID) && _gameState == State.PlayerTurn)
                    {
                        PrintPlayersHands(callback.ChatterID, callback.ChatRoomID);
                    }
                }
            );

            _moneyModule = (Money)SteamNerd.GetModule("Money");
            _rand = new Random();

            _waiting = new List<SteamID>();
            _players = new Dictionary<SteamID, Player>();
            _countdown = new System.Timers.Timer[3];

            BuildDeck();
        }

        public void BlackjackCommands(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var subcommand = "";

            if (args.Length > 2)
            {
                subcommand = args[1];
            }

            switch (subcommand)
            {
                case "bet":
                    PlayerBet(callback, args);
                    break;
                default:
                    Join(callback, args);
                    break;
            }

        }

        public void AddPlayer(SteamID steamID, SteamID chat, bool announce = true)
        {
            var name = SteamNerd.ChatterNames[steamID];
            var player = new Player();
            _players[steamID] = player;

            if (announce)
            {
                SteamNerd.SendMessage(string.Format("{0} is joining blackjack!", name), chat, true);
            }
        }

        public void StartGame(SteamFriends.ChatMsgCallback callback)
        {
            _gameState = State.WaitingForPlayers;

            var chat = callback.ChatRoomID;
            SteamNerd.SendMessage(string.Format("Starting blackjack!\n" +
                "Waiting 30 seconds for players to join up!\n" +
                "Type {0}blackjack to join.\n",
                SteamNerd.CommandChar), chat, true);

            _joinTimer = new System.Timers.Timer(PREROUND_TIMER);
            _joinTimer.AutoReset = false;
            _joinTimer.Elapsed += (src, e) => StartBetting(callback);
            _joinTimer.Start();

            for (int i = 3; i > 0; i--)
            {
                var timer = _countdown[i - 1];
                var countdownString = string.Format("{0}...", i);

                timer = new System.Timers.Timer(PREROUND_TIMER - i * 1000);
                timer.AutoReset = false;
                timer.Elapsed += (src, e) => SteamNerd.SendMessage(countdownString, chat, true); ;
                timer.Start();
            }
        }

        public void StartBetting(SteamFriends.ChatMsgCallback callback)
        {
            _gameState = State.Betting;

            var chat = callback.ChatRoomID;

            // Move all waiting players into the playing queue
            if (_waiting.Count != 0)
            {
                foreach (var player in _waiting)
                {
                    var name = SteamNerd.ChatterNames[player];
                    AddPlayer(player, chat);
                }

                _waiting = new List<SteamID>();
            }

            var message = string.Format("Betting has started now!\n" +
                "You have 30 seconds to place your bets.\n" +
                "Use {0}blackjack bet [money] to place your bets.\n" +
                "If you don't, you're gonna get kicked out of the game!\n" +
                "If you're not in the game, use {0}blackjack to join!",
                SteamNerd.CommandChar);

            SteamNerd.SendMessage(message, chat, true);

            _preRoundTimer = new System.Timers.Timer(PREROUND_TIMER);
            _preRoundTimer.AutoReset = false;
            _preRoundTimer.Elapsed += (src, e) => StartBlackjack(callback);
            _preRoundTimer.Start();
        }

        public void StartBlackjack(SteamFriends.ChatMsgCallback callback)
        {
            _gameState = State.Starting;

            // Reset bet counter
            _betsPlaced = 0;

            var chat = callback.ChatRoomID;

            foreach(var playerKV in _players.Where(kvp => kvp.Value.Bet == 0).ToList())
            {
                _players.Remove(playerKV.Key);
            }  

            if (_players.Count == 0)
            {
                SteamNerd.SendMessage("No players! Quitting blackjack.", chat, true);
                _gameState = State.NoGame;

                return;
            }

            var message = "Current Players:\n";

            for (var i = 0; i < _players.Count; i++)
            {
                var player = _players.Keys.ElementAt(i);
                var name = SteamNerd.ChatterNames[player];
                message += string.Format("{0}. {1}\n", i + 1, name);
            }

            SteamNerd.SendMessage(message, chat, true);
            StartRound(callback);
        }

        public void StartRound(SteamFriends.ChatMsgCallback callback)
        {
            var chat = callback.ChatRoomID;

            Shuffle();

            // Deal to players
            foreach (var playerKV in _players)
            {
                var playerID = playerKV.Key;
                var player = playerKV.Value;
                var name = SteamNerd.ChatterNames[playerID];

                var hand = new Hand();
                Deal(hand);
                Deal(hand);


                player.Hands.Add(hand);

                // You win! And can't do anything!
                if (hand.GetValue() == 21)
                {
                    hand.State = HandState.Blackjack;
                }

                PrintPlayersHands(playerID, chat);
            }

            // Deal to dealer
            _dealerHand = new Hand();
            Deal(_dealerHand);
            Deal(_dealerHand);
            SteamNerd.SendMessage(string.Format("Dealer\n{0}🂠", _dealerHand.Cards[0]), chat, true);

            _canInsure = _dealerHand.Cards[0].Rank == Rank.Ace;

            if (_canInsure)
            {
                SteamNerd.SendMessage(string.Format("Dealer has an ace. Insurance can be bought with {0}insurance", SteamNerd.CommandChar),
                    chat, true);
            }

            _gameState = State.PlayerTurn;
        }

        public void CheckHands(SteamFriends.ChatMsgCallback callback)
        {
            foreach (var playerKV in _players)
            {
                var player = playerKV.Value;

                foreach (var hand in player.Hands)
                {
                    // Someone hasn't bet!!!
                    if (hand.State == HandState.None)
                    {
                        return;
                    }
                }
            }

            // End Round
            DealerTurn(callback);
        }

        public void DealerTurn(SteamFriends.ChatMsgCallback callback)
        {
            _gameState = State.DealerTurn;

            var chat = callback.ChatRoomID;
            var blackjack = _dealerHand.GetValue() == 21;
            var hasAce = _dealerHand.Cards.Any(card => card.Rank == Rank.Ace);

            while (true)
            {
                var value = _dealerHand.GetValue();

                PrintDealer(chat);

                Thread.Sleep(3000);

                if (blackjack)
                {
                    SteamNerd.SendMessage("Dealer has blackjack!", chat, true);

                    if (_canInsure)
                    {
                        PayInsurance(callback);
                    }

                    break;
                }

                if (value < 17)
                {
                    hasAce |= DealerHit(chat);
                }
                else if (value == 17)
                {
                    if (hasAce)
                    {
                        DealerHit(chat);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            Payout(callback);
        }

        public void Payout(SteamFriends.ChatMsgCallback callback)
        {
            var chat = callback.ChatRoomID;
            var dealerBusts = _dealerHand.GetValue() > 21;

            if (dealerBusts)
            {
                SteamNerd.SendMessage("Dealer busts!", chat, true);
            }

            foreach (var playerKV in _players)
            {
                var playerID = playerKV.Key;
                var name = SteamNerd.ChatterNames[playerID];
                var player = playerKV.Value;

                var message = string.Format("{0} [Bet: {1}]\n", name, player.Bet);
                var format = player.Hands.Count > 1 ? "Hand {0}: {1} {2}\n" : "{1} {2}";

                var dealerNatural = _dealerHand.Cards.Count == 2 && _dealerHand.GetValue() == 21;
                var dealerValue = _dealerHand.GetValue();

                var total = 0;

                for (var i = 0; i < player.Hands.Count; i++)
                {
                    var j = i + 1;
                    var hand = player.Hands[i];
                    var winnings = player.Bet;

                    var handValue = hand.GetValue();

                    switch (hand.State)
                    {
                        case HandState.Blackjack:
                            if (dealerNatural)
                            {
                                message += string.Format(format, j, hand, "Push");
                            }
                            else
                            {
                                winnings += (int)(winnings * (3.0 / 2.0));
                                message += string.Format(format, j, hand, "Blackjack");
                            }
                            break;

                        case HandState.Surrender:
                            winnings = winnings / 2;
                            message += string.Format(format, j, hand, hand.State);
                            break;

                        case HandState.Bust:
                            winnings = 0;
                            message += string.Format(format, j, hand, hand.State);
                            break;

                        case HandState.DoubleDown:
                            if (handValue > dealerValue)
                            {
                                winnings *= 4;
                                message += string.Format(format, j, hand, "Double Down Win");
                            }
                            else if (handValue == dealerValue && !dealerNatural)
                            {
                                winnings *= 2;
                                message += string.Format(format, j, hand, "Double Down Push");
                            }
                            else
                            {
                                winnings = 0;
                                message += string.Format(format, j, hand, "Double Down Loss");
                            }
                            break;

                        case HandState.Charlie:
                            if (!dealerNatural)
                            {
                                winnings *= 2;
                                message += string.Format(format, j, hand, "8 Card Charlie");
                            }
                            else
                            {
                                winnings = 0;
                                message += string.Format(format, j, hand, "8 Card Charlie Loss");
                            }
                            break;
                        
                        default:
                            if (dealerBusts || handValue > dealerValue)
                            {
                                winnings *= 2;
                                message += string.Format(format, j, hand, "Win");
                            }
                            else if (handValue == dealerValue && !dealerNatural)
                            {
                                winnings *= 1;
                                message += string.Format(format, j, hand, "Push");
                            }
                            else
                            {
                                winnings = 0;
                                message += string.Format(format, j, hand, "Loss");
                            }
                            break;
                    }

                    total += winnings;
                    _moneyModule.AddMoney(playerID, chat, winnings);
                }

                var net = total - player.Bet;
                SteamNerd.SendMessage(message, chat, true);

                if (net > 0)
                {
                    SteamNerd.SendMessage(string.Format("{0} wins ${1}!", name, net), chat, true);
                }
                else if (net < 0)
                {
                    SteamNerd.SendMessage(string.Format("{0} loses ${1}!", name, net), chat, true);
                }
            }

            RestartRound(callback);
        }

        public void RestartRound(SteamFriends.ChatMsgCallback callback)
        {
            var steamIDs = new List<SteamID>();
            foreach (var player in _players)
            {
                steamIDs.Add(player.Key);
            }

            _players = new Dictionary<SteamID, Player>();

            foreach (var steamID in steamIDs)
            {
                AddPlayer(steamID, callback.ChatRoomID, false);
            }

            Thread.Sleep(5000);

            StartBetting(callback);
        }

        private void PayInsurance(SteamFriends.ChatMsgCallback callback)
        {
            var chat = callback.ChatRoomID;
            foreach (var playerKV in _players)
            {
                var playerID = playerKV.Key;
                var player = playerKV.Value;
                var name = SteamNerd.ChatterNames[playerID];

                if (player.HasInsurance)
                {
                    var winnings = (player.Bet / 2) * 3;
                    SteamNerd.SendMessage(string.Format("{0} won ${1} in insurance!", name, winnings), chat, true);
                    _moneyModule.AddMoney(playerID, chat, winnings);
                }
            }
        }

        public void PlayerBet(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chat = callback.ChatRoomID;
            var chatter = callback.ChatterID;
            var name = SteamNerd.ChatterNames[chatter];
            var money = _moneyModule.GetPlayerMoney(chatter);

            if (_gameState != State.Betting || !_players.ContainsKey(chatter))
            {
                return;
            }

            if (args.Length < 3)
            {
                SteamNerd.SendMessage(string.Format("Usage: {0}blackjack bet [amount]", SteamNerd.CommandChar), chat, true);
                return;
            }

            int amount;

            if (!int.TryParse(args[2], out amount) || amount < 0)
            {
                SteamNerd.SendMessage("You need to bet over $0.", chat, true);
                return;
            }

            if (amount > money)
            {
                SteamNerd.SendMessage(string.Format("{0}, you don't have that kind of money!", name), chat, true);
                return;
            }

            var better = _players[chatter];

            if (better.Bet == 0)
            {
                _betsPlaced++;
            }
            else
            {
                // Payback the previous bet
                _moneyModule.AddMoney(chatter, chat, better.Bet);
            }

            better.Bet = amount;
            _moneyModule.AddMoney(chatter, chat, -amount);

            SteamNerd.SendMessage(string.Format("{0} bet ${1}", name, amount), chat, true);

            CheckBets(callback);
        }

        public void Join(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chat = callback.ChatRoomID;
            var chatter = callback.ChatterID;

            if ((_gameState == State.NoGame || _gameState == State.WaitingForPlayers || _gameState == State.Betting)
                && !_players.ContainsKey(chatter))
            {
                AddPlayer(chatter, chat);

                if (_gameState == State.NoGame)
                {
                    StartGame(callback);
                }
            }
            else
            {
                if (!_players.ContainsKey(chatter) && !_waiting.Contains(chatter))
                {
                    _waiting.Add(chatter);
                    SteamNerd.SendMessage(string.Format("{0} is in the waiting queue.", SteamNerd.ChatterNames[chatter]), chat, true);
                }
            }
        }

        public void Hit(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_gameState != State.PlayerTurn || !_players.ContainsKey(callback.ChatterID))
            {
                return;
            }

            var chat = callback.ChatRoomID;
            var playerID = callback.ChatterID;
            var name = SteamNerd.ChatterNames[playerID];
            var player = _players[playerID];
            var handNum = ParseHand(player, args);
            var hand = player.Hands[handNum];  

            if (hand.State != HandState.None)
            {
                var errMsg = string.Format("{0}, you can't hit with this hand.", name);
                SteamNerd.SendMessage(errMsg, chat, true);
                return;
            }

            Deal(hand);

            var value = hand.GetValue();
            if (value == 21)
            {
                hand.State = HandState.Stand;
            }
            else if (value > 21)
            {
                hand.State = HandState.Bust;
            }

            if (hand.Cards.Count == 8 && hand.State != HandState.Bust)
            {
                hand.State = HandState.Charlie;
            }

            PrintPlayersHands(playerID, chat);
            CheckHands(callback);
        }

        public void Stand(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_gameState != State.PlayerTurn || !_players.ContainsKey(callback.ChatterID))
            {
                return;
            }

            var chat = callback.ChatRoomID;
            var playerID = callback.ChatterID;
            var name = SteamNerd.ChatterNames[playerID];
            var player = _players[playerID];
            var handNum = ParseHand(player, args);
            var hand = player.Hands[handNum];

            if (hand.State != HandState.None)
            {
                var errMsg = string.Format("{0}, you can't stand with this hand.", name);
                SteamNerd.SendMessage(errMsg, chat, true);
                return;
            }

            hand.State = HandState.Stand;

            PrintPlayersHands(playerID, chat);
            CheckHands(callback);
        }

        public void DoubleDown(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_gameState != State.PlayerTurn || !_players.ContainsKey(callback.ChatterID))
            {
                return;
            }

            var chat = callback.ChatRoomID;
            var playerID = callback.ChatterID;
            var name = SteamNerd.ChatterNames[playerID];
            var player = _players[playerID];
            var handNum = ParseHand(player, args);
            var hand = player.Hands[handNum];

            if (hand.State != HandState.None || hand.Cards.Count > 2)
            {
                var errMsg = string.Format("{0}, you can't double down with this hand.", name);
                SteamNerd.SendMessage(errMsg, chat, true);
                return;
            }

            if (_moneyModule.GetPlayerMoney(playerID) < player.Bet)
            {
                var errMsg = string.Format("{0}, don't have enough money to double down!", name);
                SteamNerd.SendMessage(errMsg, chat, true);
                return;
            }

            _moneyModule.AddMoney(playerID, chat, -player.Bet);
            Deal(hand);

            var value = hand.GetValue();
            if (value > 21)
            {
                hand.State = HandState.Bust;
            }
            else
            {
                hand.State = HandState.DoubleDown;
            }

            PrintPlayersHands(playerID, chat);
            CheckHands(callback);
        }

        public void Split(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_gameState != State.PlayerTurn || !_players.ContainsKey(callback.ChatterID))
            {
                return;
            }

            var chat = callback.ChatRoomID;
            var playerID = callback.ChatterID;
            var name = SteamNerd.ChatterNames[playerID];
            var player = _players[playerID];
            var handNum = ParseHand(player, args);
            var hand1 = player.Hands[handNum];
            var bet = player.Bet;

            if (hand1.State != HandState.None || hand1.Cards.Count != 2 || hand1.Cards[0].GetValue() != hand1.Cards[1].GetValue())
            {
                var errMsg = string.Format("{0}, you can't split this hand.", name);
                SteamNerd.SendMessage(errMsg, chat, true);
                return;
            }

            if (_moneyModule.GetPlayerMoney(playerID) < player.Bet)
            {
                var errMsg = string.Format("{0}, don't have enough money to split!", name);
                SteamNerd.SendMessage(errMsg, chat, true);
                return;
            }

            _moneyModule.AddMoney(playerID, chat, -player.Bet);
            
            // Split the hand
            var hand2 = new Hand();
            player.Hands.Add(hand2);

            hand2.Cards.Add(hand1.Cards[1]);
            hand1.Cards.RemoveAt(1);

            // Check if aces
            var aceSplit = hand1.Cards[0].Rank == Rank.Ace;

            // Deal to both hands
            Deal(hand1);
            Deal(hand2);

            foreach (var hand in new[] { hand1, hand2 })
            {
                if (aceSplit)
                {
                    // Splits aren't considered natural
                    hand.State = hand.GetValue() == 21 ? HandState.Stand : HandState.AceSplit;
                }
            }

            PrintPlayersHands(playerID, chat);
            CheckHands(callback);
        }

        public void Surrender(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_gameState != State.PlayerTurn || !_players.ContainsKey(callback.ChatterID))
            {
                return;
            }

            var chat = callback.ChatRoomID;
            var playerID = callback.ChatterID;
            var name = SteamNerd.ChatterNames[playerID];
            var player = _players[playerID];
            var handNum = ParseHand(player, args);
            var hand = player.Hands[handNum];

            if (hand.State != HandState.None)
            {
                var errMsg = string.Format("{0}, you can't surrender with this hand.", name);
                SteamNerd.SendMessage(errMsg, chat, true);
                return;
            }

            hand.State = HandState.Surrender;

            PrintPlayersHands(playerID, chat);
            CheckHands(callback);
        }

        public void Insure(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (_gameState != State.PlayerTurn || !_canInsure || !_players.ContainsKey(callback.ChatterID)) return;

            var chat = callback.ChatRoomID;
            var playerID = callback.ChatterID;
            var name = SteamNerd.ChatterNames[playerID];
            var player = _players[playerID];
            var bet = player.Bet / 2;

            if (player.HasInsurance == false)
            {
                if (_moneyModule.GetPlayerMoney(playerID) < bet)
                {
                    SteamNerd.SendMessage(string.Format("{0}, you don't have ${1}! You can't buy insurance!", name, bet), chat, true);
                    return;
                }

                player.HasInsurance = true;

                SteamNerd.SendMessage(string.Format("{0} bought insurance for ${1}.", name, bet), chat, true);
                _moneyModule.AddMoney(playerID, chat, -bet);
            }

            // Done paying out
            _canInsure = false;
        }

        public void PrintPlayersHands(SteamID playerID, SteamID chat)
        {
            var name = SteamNerd.ChatterNames[playerID];
            var player = _players[playerID];
            var message = string.Format("{0} [Bet: {1}]\n", name, player.Bet);

            var format = player.Hands.Count == 1 ? "{1} {2}" : "Hand {0}: {1} {2}\n";

            for (var i = 0; i < player.Hands.Count; i++)
            {
                var hand = player.Hands[i];
                message += string.Format(format, i + 1, hand, (hand.State == HandState.None ? "" : hand.State.ToString()));
            }

            SteamNerd.SendMessage(message, chat, true);
        }

        /// <summary>
        /// Checks the message to see if there's an argument for the hand
        /// </summary>
        /// <param name="player"></param>
        /// <param name="args"></param>
        /// <returns>The index of the hand (0 default)</returns>
        private int ParseHand(Player player, string[] args)
        {
            var hand = 0;

            if (args.Length > 1)
            {
                if (int.TryParse(args[1], out hand))
                {
                    hand--;

                    if (hand < 0 && hand >= player.Hands.Count)
                    {
                        hand = 0;
                    }
                }
            }

            return hand;
        }

        /// <summary>
        /// Check the bets to see if we can start the game early.
        /// </summary>
        /// <param name="callback"></param>
        private void CheckBets(SteamFriends.ChatMsgCallback callback)
        {
            var chat = callback.ChatRoomID;
            var message = string.Format("{0}/{1} bets placed.", _betsPlaced, _players.Count);
            SteamNerd.SendMessage(message, chat, true);

            if (_betsPlaced == _players.Count)
            {
                _preRoundTimer.Stop();
                StartBlackjack(callback);
            }
        }

        /// <summary>
        /// Hits the dealer
        /// </summary>
        /// <param name="chat">Chatroom Steam ID</param>
        /// <returns>Is the card an Ace?</returns>
        private bool DealerHit(SteamID chat)
        {
            SteamNerd.SendMessage("Dealer hits!", chat, true);
            Deal(_dealerHand);

            if (_dealerHand.Cards.Last().Rank == Rank.Ace)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Prints the dealer's hand
        /// </summary>
        /// <param name="chat">The chatroom Steam ID</param>
        private void PrintDealer(SteamID chat)
        {
            SteamNerd.SendMessage(string.Format("Dealer:\n{0}", _dealerHand), chat, true);
        }

        /// <summary>
        /// Fisher-Yates shuffle the stuff
        /// </summary>
        private void Shuffle()
        {
            _deckPos = 0;

            for (var i = _deck.Count - 1; i > 0; i--)
            {
                var j = _rand.Next(i + 1);
                var temp = _deck[j];
                _deck[j] = _deck[i];
                _deck[i] = temp;
            }
        }

        /// <summary>
        /// Draws a card and adds it to the hand
        /// </summary>
        /// <param name="hand">Hand to add the card to</param>
        private void Deal(Hand hand)
        {
            hand.Cards.Add(_deck[_deckPos++]);
        }

        /// <summary>
        /// Fills the deck with cards
        /// </summary>
        private void BuildDeck()
        {
            _deck = new List<Card>();

            // Build deck
            foreach (var suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (var rank in Enum.GetValues(typeof(Rank)))
                {
                    var card = new Card
                    {
                        Suit = (Suit)suit,
                        Rank = (Rank)rank
                    };

                    _deck.Add(card);
                }
            }
        }
    }
}
