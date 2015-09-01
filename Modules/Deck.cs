using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STEAMNERD.Modules
{
    class Deck
    {
        private const string SUIT_CHARS = "♣♦♥♠";
        public enum Suit { Clubs, Diamonds, Hearts, Spades }
        public enum Rank { Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }

        public List<Card> Cards;
        private int _deckPosition;

        public struct Card
        {
            public Suit Suit;
            public Rank Rank;

            public override string ToString()
            {
                var rankString = new StringBuilder();

                switch (Rank)
                {
                    case Rank.Ace:
                    case Rank.Jack:
                    case Rank.Queen:
                    case Rank.King:
                        rankString.Append(Rank.ToString()[0]);
                        break;
                    default:
                        rankString.Append((int)Rank);
                        break;
                }

                return rankString.Append(SUIT_CHARS[(int)Suit]).ToString();
            }
        }

        /// <summary>
        ///  A deck of playing cards.
        /// </summary>
        /// <param name="numberOfDecks">
        /// The amount of 52-card decks in this deck.
        /// </param>
        public Deck(int numberOfDecks = 1)
        {
            Cards = new List<Card>();
            BuildDeck(numberOfDecks);
            Shuffle();
        }
        
        /// <summary>
        /// Shuffles the cards in the deck.
        /// </summary>
        public void Shuffle()
        {
            // Reset the deck position
            _deckPosition = 0;
            var rand = new Random();

            for (var i = Cards.Count - 1; i > 0; i--)
            {
                var j = rand.Next(i + 1);
                var temp = Cards[j];
                Cards[j] = Cards[i];
                Cards[i] = temp;
            }
        }

        /// <summary>
        /// Fills the card array.
        /// </summary>
        private void BuildDeck(int numberOfDecks)
        {
            for (var i = 0; i < numberOfDecks; i++)
            {
                foreach (var suit in Enum.GetValues(typeof(Suit)))
                {
                    foreach (var rank in Enum.GetValues(typeof(Rank)))
                    {
                        var card = new Card
                        {
                            Suit = (Suit)suit,
                            Rank = (Rank)rank
                        };

                        Cards.Add(card);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the topmost card from the deck and discards it.
        /// </summary>
        /// <returns>The topmost card</returns>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown when the end of the deck is reached.
        /// </exception>
        public Card DealCard()
        {
            if (_deckPosition < Cards.Count)
            {
                return Cards[_deckPosition++];
            }
            else
            {
                throw new IndexOutOfRangeException("No more cards in the deck.");
            }
        }
    }
}
