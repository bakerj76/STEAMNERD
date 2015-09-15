 # -*- coding: utf-8 -*-
from enum import Enum
import random

class Suits(Enum):
	Clubs = u'♣'
	Diamonds = u'♦'
	Hearts = u'♥'
	Spades = u'♠'

class Ranks(Enum):
	Ace = 'A'
	Two = 2
	Three = 3
	Four = 4
	Five = 5
	Six = 6
	Seven = 7
	Eight = 8
	Nine = 9
	Ten = 10
	Jack = 'J'
	Queen = 'Q'
	King = 'K'

class Card(object):
	def __init__(self, suit, rank):
		self.Suit = suit
		self.Rank = rank
		self.FaceDown = False
		
	def __str__(self):
		return u'🂠' if self.FaceDown else self.Suit.value + str(self.Rank.value)

class Deck(object):
	def __init__(self, decks = 1):
		self.Cards = []
		self._cardIndex = 0
		self._buildDeck(decks)
		self.Shuffle()
		
	def _buildDeck(self, decks):
		for suit in Suits:
			for rank in Ranks:
				self.Cards.append(Card(suit, rank))
		
	def Shuffle(self):
		random.shuffle(self.Cards)

	def GetCards(self, n = 1):
		cards = []
		
		for i in xrange(n):
			if self._cardIndex >= len(self.Cards):
				self.Shuffle()
				self._cardIndex = 0
			
			cards.append(self.Cards[self._cardIndex])
			self._cardIndex += 1
			
		return cards