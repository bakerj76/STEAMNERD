using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SteamKit2;

namespace SteamNerd.Modules
{
    static class BlackjackCardExtensions
    {
        public static int GetValue(this Deck.Card card)
        {
            switch (card.Rank)
            {
                case Deck.Rank.Ace:
                    return 11;
                case Deck.Rank.Jack:
                case Deck.Rank.Queen:
                case Deck.Rank.King:
                    return 10;
                default:
                    return (int)card.Rank;
            }
        }
    }

    class Blackjack : Module
    {
        private float PREROUND_TIMER = 30;
        public enum State { NoGame, Betting, Dealing, PlayerTurn, DealerTurn, Payout }
        public enum HandState { None, Stand, DoubleDown, Surrender, Blackjack, Bust, AceSplit, Charlie }

        private Money _moneyModule;
        private Random _rand;

        private List<SteamID> _waiting;
        private Dictionary<SteamID, Player> _players;
        private Hand _dealerHand;

        private bool _canInsure;
        private State _gameState;
        private int _betsPlaced;

        private Countdown _preRoundTimer;
        private Deck _deck;
        private SteamID _chat;

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
            public List<Deck.Card> Cards;
            public HandState State;

            public Hand()
            {
                Cards = new List<Deck.Card>();
                State = HandState.None;
            }

            public int GetValue()
            {
                var total = GetMax();

                foreach (var card in Cards)
                {
                    if (total > 21 && card.Rank == Deck.Rank.Ace)
                    {
                        total -= 10;
                    }
                }

                return total;
            }

            public int GetMax()
            {
                return Cards.Sum(card => card.GetValue());
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

        public Blackjack(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Blackjack";
            Description = "The card game.";

            AddCommand(
                "",
                "",
                PlayingCommands
            );

            AddCommand(
                "blackjack",
                "Enter the game of blackjack!",
                Join
            );

            AddCommand(
                "bj",
                "",
                Join
            );

            AddCommand(
                "bet",
                "Place your bets if you're playing blackjack.",
                null
            );

            AddCommand(
                "hit",
                "Add a card to your hand.",
                null
            );

            AddCommand(
                "stand",
                "Stick with your hand.",
                null
            );

            AddCommand(
                "double",
                "Double your bet, hit, then stand",
                null
            );

            AddCommand(
                "split",
                "If your cards have the same value, split your hand into two hands.",
                null
            );

            AddCommand(
                "surrender",
                "Quit and get half your bet back.",
                null
            );

            AddCommand(
                "insurance",
                "Make a sidebet of half your bet that the dealer has a blackjack.",
                null
            );

            AddCommand(
                "hand",
                "Look at your hand.",
                null
            );

            AddCommand(
                "quit",
                "Quits blackjack.",
                null
            );

            //_moneyModule = (Money)SteamNerd.GetModule("Money");
            _rand = new Random();
            _waiting = new List<SteamID>();
            _players = new Dictionary<SteamID, Player>();
            _deck = new Deck();
        }

        /// <summary>
        /// Changes the game state and calls the appropriate function.
        /// </summary>
        /// <param name="state"></param>
        public void ChangeState(State state)
        {
            _gameState = state;

            switch (_gameState)
            {
                case State.NoGame:
                    break;
                case State.Betting:
                    PlaceBets();
                    break;
                case State.Dealing:
                    Deal();
                    break;
                case State.PlayerTurn:
                    break;
                case State.DealerTurn:
                    PlayDealer();
                    break;
                case State.Payout:
                    Payout();
                    break;
            }
        }

        /// <summary>
        /// When players can place bets.
        /// </summary>
        public void PlaceBets()
        {
            MoveWaitingToPlaying();

            var message = string.Format("Betting has started now!\n" +
                "You have 30 seconds to place your bets.\n" +
                "Join the game with '{0}blackjack'! Quit the game with 'quit'!\n" +
                "Use 'bet [money]' to place your bets.\n" +
                "If you don't, you're gonna get kicked out of the game!\n",
                SteamNerd.CommandChar);

            SteamNerd.SendMessage(message, _chat);

            // Wait for bets.
            _preRoundTimer = new Countdown(SteamNerd, _chat, (src, e) => StartBlackjack(), PREROUND_TIMER, 3);
        }

        /// <summary>
        /// Move all waiting players into playing.
        /// </summary>
        private void MoveWaitingToPlaying()
        {
            if (_waiting.Count != 0)
            {
                foreach (var player in _waiting)
                {
                    var name = SteamNerd.ChatterNames[player];
                    AddPlayer(player);
                }

                _waiting = new List<SteamID>();
            }
        }

        /// <summary>
        /// Sets up the game.
        /// </summary>
        public void StartBlackjack()
        {
            // Reset the bet counter.
            _betsPlaced = 0;

            // Remove players who didn't bet.
            foreach (var playerKV in _players.Where(kvp => kvp.Value.Bet == 0).ToList())
            {
                _players.Remove(playerKV.Key);
            }

            // Check if no one's playing the game.
            if (_players.Count == 0)
            {
                EndGame();
                return;
            }

            ChangeState(State.Dealing);
        }

        /// <summary>
        /// Deals cards to the players, then the dealer.
        /// </summary>
        public void Deal()
        {
            _deck.Shuffle();

            // Deal to all of the players.
            foreach (var playerKV in _players)
            {
                var playerID = playerKV.Key;
                var player = playerKV.Value;
                
                // Deal two cards. 
                var hand = new Hand();
                Deal(hand, 2);
                player.Hands.Add(hand);

                // Blackjack! You win! And can't do anything!
                if (hand.GetValue() == 21)
                {
                    hand.State = HandState.Blackjack;
                }

                // Show the player their hand.
                PrintPlayersHands(playerID);
            }

            // Deal to dealer, and hide the hole card.
            _dealerHand = new Hand();
            Deal(_dealerHand, 2);
            PrintDealer(true);

            CheckInsurance();
            ChangeState(State.PlayerTurn);

            // What if everyone playing got a blackjack?!?!
            CheckHands();
        }

        /// <summary>
        /// Checks if players can buy insurance.
        /// </summary>
        private void CheckInsurance()
        {
            // If the face-up dealer card is an ace, then players can buy insurance.
            _canInsure = _dealerHand.Cards[0].Rank == Deck.Rank.Ace;

            if (!_canInsure) return;

            SteamNerd.SendMessage(string.Format("Dealer has an ace. " +
                    "Insurance can be bought with {0}insurance",
                SteamNerd.CommandChar),
                _chat);
        }

        /// <summary>
        /// Plays out the dealer's turn.
        /// </summary>
        public void PlayDealer()
        {
            var blackjack = _dealerHand.GetValue() == 21;
            var hasAce = _dealerHand.Cards.Any(card => card.Rank == Deck.Rank.Ace);
            int value = _dealerHand.GetValue();
            
            // Keep playing until the dealers hand is a hard-17 or above 17.
            while (true)
            {
                value = _dealerHand.GetValue();

                // Show the dealer's hand then pause for readability.
                PrintDealer();
                Thread.Sleep(3000);

                if (blackjack)
                {
                    SteamNerd.SendMessage("Dealer has blackjack!", _chat);

                    if (_canInsure)
                    {
                        PayInsurance();
                    }
                }
                // Hit if the dealer's hand is less than 17.
                else if (value < 17)
                {
                    hasAce |= DealerHit();
                    continue;
                }
                else if (value == 17)
                {
                    // If the dealer has a 17, check if it's a soft-17.
                    // If the dealer has an ace and the value of their hand
                    // is equal to the maximum (all ace's are 11) value of 
                    // their hand, then it's a soft-17.
                    if (hasAce && value == _dealerHand.GetMax())
                    {
                        DealerHit();
                        continue;
                    }
                }

                break;
            }

            ChangeState(State.Payout);
        }

        /// <summary>
        /// Pay the players if they won.
        /// </summary>
        /// <param name="callback"></param>
        public void Payout()
        {
            var dealerBusted = _dealerHand.GetValue() > 21;
            if (dealerBusted)
            {
                SteamNerd.SendMessage("Dealer busts!", _chat);
            }

            foreach (var playerKV in _players)
            {
                var playerID = playerKV.Key;
                var player = playerKV.Value;
                var name = SteamNerd.ChatterNames[playerID];

                // The dealer got a natural 21 (ace + 10 card)
                var dealerNatural = _dealerHand.Cards.Count == 2 && _dealerHand.GetValue() == 21;
                var dealerValue = _dealerHand.GetValue();

                var message = new StringBuilder(string.Format("{0} [Bet: {1}]\n", name, player.Bet));
                var format = player.Hands.Count > 1 ? "Hand {0}: {1} {2}\n" : "{1} {2}";

                var total = 0;

                for (var i = 0; i < player.Hands.Count; i++)
                {
                    var printIndex = i + 1;
                    var hand = player.Hands[i];
                    var winnings = player.Bet;

                    var handValue = hand.GetValue();

                    switch (hand.State)
                    {
                        case HandState.Blackjack:
                            // Ties only on dealer natural 21s.
                            if (dealerNatural)
                            {
                                message.Append(string.Format(format, printIndex, hand, "Push"));
                            }
                            else
                            {
                                // Blackjacks win 3:2 
                                winnings += (int)(winnings * (3.0 / 2.0));
                                message.Append(string.Format(format, printIndex, hand, "Blackjack"));
                            }
                            break;

                        case HandState.Surrender:
                            // Give the player back half their money
                            winnings /= 2;
                            message.Append(string.Format(format, printIndex, hand, hand.State));
                            break;

                        case HandState.Bust:
                            winnings = 0;
                            message.Append(string.Format(format, printIndex, hand, hand.State));
                            break;

                        case HandState.DoubleDown:
                            if (handValue > dealerValue)
                            {
                                winnings *= 4;
                                message.Append(string.Format(format, printIndex, hand, "Double Down Win"));
                            }
                            else if (handValue == dealerValue && !dealerNatural)
                            {
                                winnings *= 2;
                                message.Append(string.Format(format, printIndex, hand, "Double Down Push"));
                            }
                            else
                            {
                                winnings = 0;
                                message.Append(string.Format(format, printIndex, hand, "Double Down Loss"));
                            }
                            break;

                        case HandState.Charlie:
                            // Natural 21's beat 8-card charlies.
                            if (!dealerNatural)
                            {
                                winnings *= 2;
                                message.Append(string.Format(format, printIndex, hand, "8 Card Charlie"));
                            }
                            else
                            {
                                winnings = 0;
                                message.Append(string.Format(format, printIndex, hand, "8 Card Charlie Loss"));
                            }
                            break;
                        
                        default:
                            if (dealerBusted || handValue > dealerValue)
                            {
                                winnings *= 2;
                                message.Append(string.Format(format, printIndex, hand, "Win"));
                            }
                            // Natural 21s beat unnatural 21s.
                            else if (handValue == dealerValue && !dealerNatural)
                            {
                                winnings *= 1;
                                message.Append(string.Format(format, printIndex, hand, "Push"));
                            }
                            else
                            {
                                winnings = 0;
                                message.Append(string.Format(format, printIndex, hand, "Loss"));
                            }
                            break;
                    }

                    total += winnings;
                    _moneyModule.AddMoney(playerID, _chat, winnings);
                }

                SteamNerd.SendMessage(message.ToString(), _chat);
                PrintNet(player, total, name);
            }

            RestartRound();
        }

        /// <summary>
        /// Print the player's net winnings.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="totalWinnings"></param>
        /// <param name="playerName"></param>
        private void PrintNet(Player player, int totalWinnings, string playerName)
        {
            var net = totalWinnings - player.Bet;

            if (net > 0)
            {
                SteamNerd.SendMessage(string.Format("{0} wins ${1}!", playerName, net), _chat);
            }
            else if (net < 0)
            {
                SteamNerd.SendMessage(string.Format("{0} loses ${1}!", playerName, -net), _chat);
            }
        }

        /// <summary>
        /// Restart to a new round.
        /// </summary>
        public void RestartRound()
        {
            // Get the players currently playing.
            var steamIDs = _players.Select(player => player.Key);

            // Erase the current players
            _players = new Dictionary<SteamID, Player>();

            // Add each player
            foreach (var steamID in steamIDs)
            {
                AddPlayer(steamID, false);
            }

            // Wait for 5 seconds.
            var delay = new System.Timers.Timer(5000);
            delay.AutoReset = false;
            delay.Elapsed += (src, e) => ChangeState(State.Betting);
            delay.Start();
        }

        /// <summary>
        /// Joins the blackjack game.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void Join(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chat = callback.ChatRoomID;
            var chatter = callback.ChatterID;

            if ((_gameState == State.NoGame || _gameState == State.Betting) && !_players.ContainsKey(chatter))
            {
                _chat = chat;
                AddPlayer(chatter);

                if (_gameState == State.NoGame)
                {
                    ChangeState(State.Betting);
                }
            }
            else
            {
                if (!_players.ContainsKey(chatter) && !_waiting.Contains(chatter))
                {
                    _waiting.Add(chatter);
                    SteamNerd.SendMessage(string.Format("{0} is in the waiting queue.", SteamNerd.ChatterNames[chatter]), chat);
                }
            }
        }

        public void PlayingCommands(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (!_players.ContainsKey(callback.ChatterID))
                return;

            var command = args[0].ToLower();

            if (_gameState == State.Betting)
            {
                switch (command)
                {
                    case "bet":
                        PlayerBet(callback, args);
                        break;
                    case "quit":
                        Quit(callback, args);
                        break;
                }
            }
            else if (_gameState == State.PlayerTurn)
            {
                switch (command)
                {
                    case "hit":
                    case "twist":
                        Hit(callback, args);
                        break;
                    case "stand":
                    case "stick":
                    case "stay":
                        Stand(callback, args);
                        break;
                    case "double":
                        DoubleDown(callback, args);
                        break;
                    case "split":
                        Split(callback, args);
                        break;
                    case "surrender":
                        Surrender(callback, args);
                        break;
                    case "insurance":
                        Insure(callback, args);
                        break;
                    case "hand":
                        PrintPlayersHands(callback.ChatterID);
                        break;
                }
            }

        }

        public void PlayerBet(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chatter = callback.ChatterID;
            var name = SteamNerd.ChatterNames[chatter];
            var money = _moneyModule.GetPlayerMoney(chatter);

            if (_gameState != State.Betting || !_players.ContainsKey(chatter))
            {
                return;
            }

            if (args.Length < 2)
            {
                SteamNerd.SendMessage("Usage: bet [amount]", _chat);
                return;
            }

            int amount;

            if (!int.TryParse(args[1], out amount) || amount <= 0)
            {
                SteamNerd.SendMessage("You need to bet over $0.", _chat);
                return;
            }

            if (amount > money)
            {
                SteamNerd.SendMessage(string.Format("{0}, you don't have that kind of money!", name), _chat);
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
                _moneyModule.AddMoney(chatter, _chat, better.Bet);
            }

            better.Bet = amount;
            _moneyModule.AddMoney(chatter, _chat, -amount);

            SteamNerd.SendMessage(string.Format("{0} bet ${1}", name, amount), _chat);

            CheckBets(callback);
        }

        public void Quit(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chatter = callback.ChatterID;

            _players.Remove(chatter);
            SteamNerd.SendMessage(
                string.Format("{0} is leaving the game.", SteamNerd.ChatterNames[chatter]),
                _chat
            );

            if (_players.Count == 0)
            {
                EndGame();
            }
        }

        public void Hit(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var playerID = callback.ChatterID;
            var player = _players[playerID];
            var name = SteamNerd.ChatterNames[playerID];

            var handNum = ParseHand(player, args);
            var hand = player.Hands[handNum];  

            if (hand.State != HandState.None)
            {
                var errMsg = string.Format("{0}, you can't hit with this hand.", name);
                SteamNerd.SendMessage(errMsg, _chat);
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

            PrintPlayersHands(playerID);
            CheckHands();
        }

        public void Stand(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var playerID = callback.ChatterID;
            var player = _players[playerID];
            var name = SteamNerd.ChatterNames[playerID];

            var handNum = ParseHand(player, args);
            var hand = player.Hands[handNum];

            if (hand.State != HandState.None)
            {
                var errMsg = string.Format("{0}, you can't stand with this hand.", name);
                SteamNerd.SendMessage(errMsg, _chat);
                return;
            }

            hand.State = HandState.Stand;

            PrintPlayersHands(playerID);
            CheckHands();
        }

        public void DoubleDown(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var playerID = callback.ChatterID;
            var player = _players[playerID];
            var name = SteamNerd.ChatterNames[playerID];

            var handNum = ParseHand(player, args);
            var hand = player.Hands[handNum];

            if (hand.State != HandState.None || hand.Cards.Count > 2)
            {
                var errMsg = string.Format("{0}, you can't double down with this hand.", name);
                SteamNerd.SendMessage(errMsg, _chat);
                return;
            }

            if (_moneyModule.GetPlayerMoney(playerID) < player.Bet)
            {
                var errMsg = string.Format("{0}, don't have enough money to double down!", name);
                SteamNerd.SendMessage(errMsg, _chat);
                return;
            }

            _moneyModule.AddMoney(playerID, _chat, -player.Bet);
            Deal(hand);

            var value = hand.GetValue();
            hand.State = value > 21 ? HandState.Bust : HandState.DoubleDown;

            PrintPlayersHands(playerID);
            CheckHands();
        }

        public void Split(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var playerID = callback.ChatterID;
            var player = _players[playerID];
            var name = SteamNerd.ChatterNames[playerID];

            var handNum = ParseHand(player, args);
            var hand1 = player.Hands[handNum];
            var bet = player.Bet;

            if (hand1.State != HandState.None || hand1.Cards.Count != 2 || hand1.Cards[0].GetValue() != hand1.Cards[1].GetValue())
            {
                var errMsg = string.Format("{0}, you can't split this hand.", name);
                SteamNerd.SendMessage(errMsg, _chat);
                return;
            }

            if (_moneyModule.GetPlayerMoney(playerID) < player.Bet)
            {
                var errMsg = string.Format("{0}, don't have enough money to split!", name);
                SteamNerd.SendMessage(errMsg, _chat);
                return;
            }

            _moneyModule.AddMoney(playerID, _chat, -player.Bet);
            
            // Split the hand
            var hand2 = new Hand();
            player.Hands.Add(hand2);

            hand2.Cards.Add(hand1.Cards[1]);
            hand1.Cards.RemoveAt(1);

            // Check if aces
            var aceSplit = hand1.Cards[0].Rank == Deck.Rank.Ace;

            // Deal to both hands
            Deal(hand1);
            Deal(hand2);

            foreach (var hand in new[] { hand1, hand2 })
            {
                if (aceSplit)
                {
                    // Splits aren't considered natural blackjacks
                    hand.State = hand.GetValue() == 21 ? HandState.Stand : HandState.AceSplit;
                }
            }

            PrintPlayersHands(playerID);
            CheckHands();
        }

        public void Surrender(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var playerID = callback.ChatterID;
            var player = _players[playerID];
            var name = SteamNerd.ChatterNames[playerID];

            var handNum = ParseHand(player, args);
            var hand = player.Hands[handNum];

            if (hand.State != HandState.None)
            {
                var errMsg = string.Format("{0}, you can't surrender with this hand.", name);
                SteamNerd.SendMessage(errMsg, _chat);
                return;
            }

            hand.State = HandState.Surrender;

            PrintPlayersHands(playerID);
            CheckHands();
        }

        public void Insure(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (!_canInsure) return;

            var chat = callback.ChatRoomID;
            var playerID = callback.ChatterID;
            var name = SteamNerd.ChatterNames[playerID];
            var player = _players[playerID];
            var bet = player.Bet / 2;

            if (player.HasInsurance == false)
            {
                if (_moneyModule.GetPlayerMoney(playerID) < bet)
                {
                    SteamNerd.SendMessage(string.Format("{0}, you don't have ${1}! You can't buy insurance!", name, bet), chat);
                    return;
                }

                player.HasInsurance = true;

                SteamNerd.SendMessage(string.Format("{0} bought insurance for ${1}.", name, bet), chat);
                _moneyModule.AddMoney(playerID, chat, -bet);
            }

            // Done paying out
            _canInsure = false;
        }

        public void PrintPlayersHands(SteamID playerID)
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

            SteamNerd.SendMessage(message, _chat);
        }

        public void AddPlayer(SteamID steamID, bool announce = true)
        {
            var name = SteamNerd.ChatterNames[steamID];
            var player = new Player();
            _players[steamID] = player;

            if (announce)
            {
                SteamNerd.SendMessage(string.Format("{0} is joining blackjack!", name), _chat);
            }
        }

        /// <summary>
        /// Display the ending message and end the game.
        /// </summary>
        private void EndGame()
        {
            SteamNerd.SendMessage("No players! Quitting blackjack.", _chat);
            _preRoundTimer.Stop();
            ChangeState(State.NoGame);
        }

        /// <summary>
        /// Pays out insurance to players that bought it.
        /// </summary>
        /// <param name="callback"></param>
        private void PayInsurance()
        {
            foreach (var playerKV in _players)
            {
                var playerID = playerKV.Key;
                var player = playerKV.Value;
                var name = SteamNerd.ChatterNames[playerID];

                if (player.HasInsurance)
                {
                    var winnings = (player.Bet / 2) * 3;
                    SteamNerd.SendMessage(string.Format("{0} won ${1} in insurance!", name, winnings), _chat);
                    _moneyModule.AddMoney(playerID, _chat, winnings);
                }
            }
        }

        /// <summary>
        /// Checks the message to see if there's an argument for the hand.
        /// </summary>
        /// <param name="player">The player doing the action.</param>
        /// <param name="args">The command.</param>
        /// <returns>The index of the hand (0 default).</returns>
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
            SteamNerd.SendMessage(message, chat);

            if (_betsPlaced == _players.Count)
            {
                _preRoundTimer.Stop();
                StartBlackjack();
            }
        }

        /// <summary>
        /// Checks if all of the hands have been played completely.
        /// </summary>
        private void CheckHands()
        {
            foreach (var playerKV in _players)
            {
                var player = playerKV.Value;

                foreach (var hand in player.Hands)
                {
                    // Someone hasn't finished playing their hand!!!
                    if (hand.State == HandState.None)
                    {
                        return;
                    }
                }
            }

            // End the players turn since all hands are done.
            ChangeState(State.DealerTurn);
        }

        /// <summary>
        /// Hits the dealer.
        /// </summary>
        /// <param name="chat">The chatroom for printing.</param>
        /// <returns>The dealt card was an Ace.</returns>
        private bool DealerHit()
        {
            SteamNerd.SendMessage("Dealer hits!", _chat);
            Deal(_dealerHand);
            
            return _dealerHand.Cards.Last().Rank == Deck.Rank.Ace;
        }

        /// <summary>
        /// Prints the dealer's hand.
        /// </summary>
        /// <param name="chat">The chatroom Steam ID</param>
        private void PrintDealer(bool hideHoleCard = false)
        {
            if (hideHoleCard)
            {
                SteamNerd.SendMessage(string.Format("Dealer:\n{0} 🂠", _dealerHand.Cards[0]), _chat);
            }
            else
            {
                SteamNerd.SendMessage(string.Format("Dealer:\n{0} {1}", _dealerHand, _dealerHand.GetValue()), _chat);
            }
        }

        /// <summary>
        /// Draws cards and puts them in the hand.
        /// </summary>
        /// <param name="hand">The hand to put the cards into.</param>
        /// <param name="cards">The number of cards to deal.</param>
        private void Deal(Hand hand, int cards = 1)
        {
            for (var i = 0; i < cards; i++)
            {
                hand.Cards.Add(_deck.DealCard());
            }
        }
    }
}
