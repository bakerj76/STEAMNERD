import random
import deck
from enum import Enum
import countdown
import time

# Enums
BlackjackStates = Enum('BlackjackStates', 'NoGame Waiting Dealing ' + \
	'PlayerTurn DealerTurn Payout')

HandStates = Enum('HandStates', 'None Stand DoubleDown Surrender ' + \
	'Blackjack Bust Split AceSplit Charlie')

# Classes
class Player:
	def __init__(self, bet):
		self.Bet = bet
		self.Hands = []
		self.HasInsurance = False
		
	def __str__(self):
		if len(self.Hands) == 1:
			return str(self.Hands[0])
		elif len(self.Hands) > 1:
			return ''.join("{}. {}".format(i, hand) for i, hand in enumerate(self.Hands))
		
		return ""

	def CheckDone(self):
		return all(hand.Done for hand in self.Hands)

def GetValue(card):
	if card.Rank == deck.Ranks.Ace:
		return 11
	elif card.Rank in (deck.Ranks.Jack, deck.Ranks.Queen, deck.Ranks.King): 
		return 10
	else:
		return card.Rank.value

class Hand:
	def __init__(self):
		self.Cards = []
		self.State = HandStates.None
		self.Soft = False
		self.Done = False

	def __str__(self):
		return "{} {}"  \
			.format(' ' \
				.join([str(card) for card in self.Cards]),
					  self.StateString())

	def GetPoints(self):
		points = 0
		
		for card in self.Cards:
			points += GetValue(card)
			
		for card in self.Cards:
			if card.Rank == deck.Ranks.Ace:
				if points > 21:
					self.Soft = False
					points -= 10
				else:
					self.Soft = True
					break
				
		return points
	
	def Deal(self, cards):
		self.Cards.extend(cards)
		self.CheckState()
			
	def SetState(self, state):
		self.State = state

		if not state in (HandStates.None, HandStates.Split):
			self.Done = True
		
	def CheckState(self):
		points = self.GetPoints()
		
		if points > 21:
			self.SetState(HandStates.Bust)
		elif len(self.Cards) == 2 and points == 21 and \
			 self.State == HandStates.None:
			self.SetState(HandStates.Blackjack)
		elif points == 21:
			self.SetState(HandStates.Stand)
		elif len(self.Cards) >= 8:
			self.SetState(HandStates.Charlie)
	
	def StateString(self):
		if any(card.FaceDown for card in self.Cards):
			return ""
		elif self.State == HandStates.Stand:
			return "Stand"
		elif self.State == HandStates.DoubleDown:
			return "Double Down"
		elif self.State == HandStates.Surrender:
			return "Surrender"
		elif self.State == HandStates.Blackjack:
			return "Blackjack"
		elif self.State == HandStates.Bust:
			return "Bust"
		elif self.State == HandStates.Charlie:
			return "8-card Charlie"
		else:
			return ""

	def Payout(self, bet, dealerHand):
		points = self.GetPoints()
		dealerPoints = dealerHand.GetPoints()

		if self.State in (HandStates.Surrender, HandStates.Bust):
			return -bet

		if dealerHand.State == HandStates.Bust:
			dealerPoints = 0

		# Dealer wins with more points except on 8-card Charlies
		if dealerPoints > points and self.State != HandStates.Charlie:
			return -bet

		if dealerPoints == points:
			# Lose on un-natural 21 vs natural 21
			if dealerHand.State == HandStates.Blackjack and self.State != HandStates.Blackjack:
				return -bet
			# Win on natural 21 vs un-natural 21
			elif self.State == HandStates.Blackjack and not dealerHand.State == HandStates.Blackjack:
				return int(bet * 3./2)
			# Otherwise, push
			else:
				return 0

		if self.State == HandStates.DoubleDown:
			return bet * 2

		if self.State == HandStates.Blackjack:
			return int(bet * (3./2))

		if self.State == HandStates.Charlie:
			# Lose on charlie vs natural 21
			if dealerHand.State == HandStates.Blackjack:
				return 0
			else:
				return bet

		if self.State == HandStates.Surrender:
			return -bet / 2

		return bet

# Code
Module.Name = "Blackjack"
Module.Description = "Hey you. Yeah, you. Come and play some Blackjack."

var.WaitingQueue = {}
var.Players = {}
var.CanInsure = False
var.GameState = BlackjackStates.NoGame
var.Skippers = []
var.Usage = "Usage: {0}blackjack [bet amount] or {0}bj [bet amount]".format(SteamNerd.CommandChar)
var.DealerHand = Hand()

def Start():
	var.Bank = Module.GetModule('Bank')
	var.Deck = deck.Deck()
	
def SetState(state):
	if var.GameState == BlackjackStates.NoGame and state != BlackjackStates.Waiting:
		return

	var.GameState = state
	
	if state == BlackjackStates.NoGame:
		pass
	elif state == BlackjackStates.Waiting:
		Waiting()
	elif state == BlackjackStates.Dealing:
		Dealing()
	elif state == BlackjackStates.PlayerTurn:
		pass
	elif state == BlackjackStates.DealerTurn:
		DealerTurn()
	elif state == BlackjackStates.Payout:
		Payout()

def JoinBlackjack(callback, args):
	chatter = callback.ChatterID 
	name = SteamNerd.GetName(chatter) 
	
	if chatter in var.WaitingQueue or chatter in var.Players:
		return
	
	if len(args) < 2:
		Say(var.Usage) 
		return
	
	bet = 0 
	
	try:
		bet = int(args[1])
	except ValueError:
		Say(var.Usage)
		return
	
	if bet <= 0:
		Say("You must bet more than $0!")
		return
	
	if var.Bank.GetMoney(chatter) < bet:
		Say("You don't have ${}!".format(bet))
		return
		
	if not var.GameState in (BlackjackStates.NoGame, BlackjackStates.Waiting):
		var.WaitingQueue[chatter] = Player(bet)
		Say("{} is in the waiting queue.".format(name))
	else:
		var.Players[chatter] = Player(bet)
		Say("{} has joined blackjack!".format(name))

	if var.GameState == BlackjackStates.NoGame:
		SetState(BlackjackStates.Waiting)
	
def Waiting():
	for steamID in var.WaitingQueue:
		player = var.WaitingQueue[steamID]
		var.Players[steamID] = player
		
	var.WaitingQueue.clear()
	
	for steamID in var.Players:
		player = var.Players[steamID]
		var.Bank.GiveMoney(steamID, -player.Bet)

	Say(("Blackjack is starting in 30 seconds.\n" + \
	"Join with {0}blackjack [bet amount] or {0}bj [bet amount].\n" + \
	"Change your bet with 'bet [amount]'.")
	.format(SteamNerd.CommandChar))
	
	var.Countdown = countdown.Countdown(
		30, 
		3, 
		lambda: SetState(BlackjackStates.Dealing), 
		Say
	)
	var.Countdown.start()
	
def Dealing():
	del var.Skippers[:]
	_dealerDeal()
	_playerDeal()

	done = all(player.CheckDone() for player in var.Players.values())

	if not done:
		SetState(BlackjackStates.PlayerTurn)
	else:
		SetState(BlackjackStates.DealerTurn)
	
def _dealerDeal():
	var.DealerHand.Deal(var.Deck.GetCards(2))
	var.DealerHand.Cards[1].FaceDown = True
	Say("Dealer:\n{}".format(var.DealerHand))

	if var.DealerHand.Cards[0].Rank == deck.Ranks.Ace:
		Say("Dealer has an Ace! Use 'insure' to buy insurance.")
		var.CanInsure = True
	else:
		var.CanInsure = False
	
def _playerDeal():
	for steamID in var.Players:
		player = var.Players[steamID]
		name = SteamNerd.GetName(steamID)
		hand = Hand()
		hand.Deal(var.Deck.GetCards(2))
		player.Hands.append(hand)
		Say("{} [Bet: {}]:\n{}".format(name, player.Bet, player))

def DealerTurn():
	hand = var.DealerHand
	hand.Cards[1].FaceDown = False
	points = hand.GetPoints()

	while points < 17 or (points == 17 and hand.Soft):
		time.sleep(2)
		Say("Dealer:\n{} {}".format(hand, points))
		hand.Deal(var.Deck.GetCards())
		points = hand.GetPoints()

		if hand.State != HandStates.None:
			break

	time.sleep(5)		
	Say("Dealer:\n{} {}".format(hand, points))
	SetState(BlackjackStates.Payout)

def Payout():
	temp = {}

	for steamID in var.Players:
		player = var.Players[steamID]
		name = SteamNerd.GetName(steamID)
		temp[steamID] = Player(player.Bet)
		message = "{} [Bet: {}]:\n".format(name, player.Bet)
		total = 0

		for hand in player.Hands:
			payout = hand.Payout(player.Bet, var.DealerHand)
			message += "{} [{}]\n".format(hand, payout)
			total += payout

		if var.CanInsure:
			insurancePay = 0

			if var.DealerHand.State == HandStates.Blackjack:
				insurancePay = (player.Bet / 2) * 2
			else:
				insurancePay = -player.Bet / 2

			message += "Insurance [{}]".format(insurancePay)

		var.Bank.GiveMoney(steamID, player.Bet + total)

		message += ("Total: (${})" if total < 0 else "Total: ${}").format(abs(total))
		Say(message)


	var.Players = temp
	var.DealerHand = Hand()
	SetState(BlackjackStates.Waiting)

def OnChatLeave(callback):
	_quit(callback.StateChangeInfo.ChatterActedOn)

def OnChatMessage(callback, args):
	chatter = callback.ChatterID
	command = args[0].lower()

	if command == "quit":
		_quit(chatter)
		return

	if not chatter in var.Players:
		return

	player = var.Players[chatter]

	if var.GameState == BlackjackStates.PlayerTurn:
		if command in ("hit", "twist"):
			_hit(callback, args, player)
		elif command in ("stand", "stay", "stick"):
			_stand(callback, args, player)
		elif command == "surrender":
			_surrender(callback, args, player)
		elif command == "double":
			_double(callback, args, player)
		elif command == "split":
			_split(callback, args, player)
		elif command == "insure":
			_insure(chatter, player)

		CheckPlayersDone()
	elif var.GameState == BlackjackStates.Waiting:
		if command == "bet":
			_bet(callback, args, player)
		elif command == "skip":
			_skip(chatter)

def CheckPlayersDone():
	if all(player.CheckDone() for player in var.Players.values()):
		SetState(BlackjackStates.DealerTurn)

def _hit(callback, args, player):
	name = SteamNerd.GetName(callback.ChatterID)
	handNum = GetHandIndex(args, player)

	if handNum < 0:
		Say("Invalid hand number!")
		return

	hand = player.Hands[handNum]

	if not hand.State in (HandStates.None, HandStates.Split):
		Say("Cannot hit on {}.".format(hand.StateString()))
		return

	hand.Deal(var.Deck.GetCards())
	Say("{} [Bet: {}]:\n{}".format(name, player.Bet, str(player)))

def _stand(callback, args, player):
	name = SteamNerd.GetName(callback.ChatterID)
	handNum = GetHandIndex(args, player)

	if handNum < 0:
		Say("Invalid hand number!")
		return

	hand = player.Hands[handNum]

	if not hand.State in (HandStates.None, HandStates.Split):
		Say("Cannot stand on {}.".format(hand.StateString()))
		return

	hand.SetState(HandStates.Stand)
	Say("{} [Bet: {}]:\n{}".format(name, player.Bet, str(player)))

def _surrender(callback, args, player):
	name = SteamNerd.GetName(callback.ChatterID)
	handNum = GetHandIndex(args, player)

	if handNum < 0:
		Say("Invalid hand number!")
		return

	hand = player.Hands[handNum]

	if not hand.State in (HandStates.None, HandStates.Split):
		Say("Cannot surrender on {}.".format(hand.StateString()))
		return

	hand.SetState(HandStates.Surrender)
	Say("{} [Bet: {}]:\n{}".format(name, player.Bet, str(player)))

def _double(callback, args, player):
	name = SteamNerd.GetName(callback.ChatterID)
	handNum = GetHandIndex(args, player)

	if handNum < 0:
		Say("Invalid hand number!")
		return

	hand = player.Hands[handNum]

	if not hand.State in (HandStates.None, HandStates.Split):
		Say("Cannot double down on {}.".format(hand.StateString()))
		return

	if len(hand.Cards) > 2:
		Say("Cannot double down after hitting!")
		return

	hand.Deal(var.Deck.GetCards())
	hand.SetState(HandStates.DoubleDown)
	Say("{} [Bet: {}]:\n{}".format(name, player.Bet, str(player)))

def _split(callback, args, player):
	name = SteamNerd.GetName(callback.ChatterID)
	handNum = GetHandIndex(args, player)

	if handNum < 0:
		Say("Invalid hand number!")
		return

	hand = player.Hands[handNum]

	if hand.Cards[0].Rank != hand.Cards[1].Rank:
		Say("Cannot split on unequal cards!")
		return

	if len(hand.Cards) > 2:
		Say("Cannot split after hitting!")
		return

	if any(plHand.State == HandStates.AceSplit for plHand in player.Hands):
		Say("Cannot split on aces twice!")

	card = hand.Cards.pop()
	newHand = Hand()
	newHand.Deal(card)

	if card.Rank == deck.Ranks.Ace:
		newHand.State = HandStates.AceSplit
		hand.State = HandStates.AceSplit
	else:
		newHand.State = HandStates.Split
		hand.State = HandStates.Split

	hand.Deal(var.Deck.GetCards())
	newHand.Deal(var.Deck.GetCards())
	player.Hands.append(newHand)
	Say("{} [Bet: {}]:\n{}".format(name, player.Bet, str(player)))

def GetHandIndex(args, player):
	if len(args) > 1:
		try:
			return int(args[1]) - 1
		except ValueError:
			return 0

		if handNum < 0 or handNum >= len(player.Hands):
			return -1

	return 0

def _insure(steamID, player):
	if not CanInsure:
		return

	insurance = player.Bet / 2

	if var.Bank.GetMoney(steamID) < insurance:
		Say("You don't have enough money to insure!")
		return

	var.Bank.GiveMoney(steamID, -insurance)
	player.HasInsurance = True

def _bet(callback, args, player):
	chatter = callback.ChatterID
	name = SteamNerd.GetName(chatter)

	if len(args) < 2:
		Say("Usage: bet [amount]")

	bet = 0

	try:
		bet = int(args[1])
	except ValueError:
		Say("That's not a number!")
		return

	if bet <= 0:
		Say("You must bet more than $0!")
		return

	if bet > var.Bank.GetMoney(chatter):
		Say("You don't have ${}!".format(bet))
		return

	var.Bank.GiveMoney(chatter, player.Bet)
	var.Bank.GiveMoney(chatter, -bet)
	player.Bet = bet

	Say("{} changed their bet to ${}!".format(name, bet))

def _skip(player):
	name = SteamNerd.GetName(player)

	if player in var.Skippers:
		return

	var.Skippers.append(player)

	if len(var.Skippers) != len(var.Players):
		Say("{} voted to skip. ({}/{})", name, len(var.Skippers), len(var.Players))
	else:
		Say("Starting the game!")
		var.Countdown.stop()
		SetState(BlackjackStates.Dealing)

def _quit(steamID):
	if steamID in var.WaitingQueue:
		del var.WaitingQueue[steamID]

		Say("{} is quitting the game!".format(SteamNerd.GetName(steamID)))
	elif steamID in var.Players:
		player = var.Players[steamID]

		if var.GameState == BlackjackStates.Waiting:
			var.Bank.GiveMoney(steamID, player.Bet)

		del var.Players[steamID]

		Say("{} is quitting the game!".format(SteamNerd.GetName(steamID)))
		
		if len(var.Players) > 0:
			CheckPlayersDone()

		CheckEnd()

def CheckEnd():
	if len(var.Players) == 0:
		if var.Countdown:
			var.Countdown.stop()

		var.DealerHand = Hand()
		SetState(BlackjackStates.NoGame)
		Say("Blackjack... is OVER!")


Module.AddCommand(
	"blackjack", 
	"Play blackjack. " + var.Usage, 
	JoinBlackjack
)

Module.AddCommand(
	"bj", 
	"", 
	JoinBlackjack
) 
