import math
import os
import cPickle as pickle
from threading import Timer
from datetime import datetime, timedelta

Module.Name = "Bank"
Module.Description = "Handles money."

class Loan:
	def __init__(self, amount, interest, fee):
		self.Amount = amount
		self._currentAmount = amount
		self.Interest = interest
		self.InterestStart = datetime.now()
		self.Fee = fee
		
		
	""" 
		Gets the amount of the loan + interest. 
	"""
	def GetAmount(self):
		# Divide the difference in seconds by the seconds in a day to get the 
		# time in fractional days.
		interestTime = ((datetime.now() - self.InterestStart).total_seconds() \
			/ timedelta(1).total_seconds())
		
		# Use the continuous interest formula to FUCK PEOPLE OVER EVEN MORE.
		amount = self._currentAmount * math.e ** (self.Interest * interestTime)
		return int(amount)
	
	
	""" 
		Pays off the loan and resets the amount.
		Returns True if the loan was totally paid off, else False.
	"""
	def Payoff(self, amount):
		difference = self.GetAmount() - amount
		
		if difference <= 0:
			self._currentAmount = 0
			return True
		
		# Reset the interest start time.
		self.InterestStart = datetime.now()
		self._currentAmount = difference
		

var.PlayerMoney = {}
var.PlayerLoans = {}
var.LoanTypes = [Loan(100, 0.10, 10), Loan(500, 0.15, 50), Loan(1000, 0.20, 100)]
	
var.MoneyFile = "Money.p"
var.MoneyPath = os.path.join(os.getenv('APPDATA'), "SteamNerd", var.MoneyFile)	
var.LoanFile = "Loans.p"
var.LoanPath = os.path.join(os.getenv('APPDATA'), "SteamNerd", var.LoanFile)
var.Changed = False


def Load():
	try:
		var.PlayerMoney = pickle.load(open(var.MoneyPath, 'rb'))
		var.PlayerLoans = pickle.load(open(var.LoanPath, 'rb'))
	except:
		pass


def Save():
	if var.Changed:
		var.Changed = False
		pickle.dump(var.PlayerMoney, open(var.MoneyPath, 'wb'))
		pickle.dump(var.PlayerLoans, open(var.LoanPath, 'wb'))
		
		
def AddChatter(steamID):
	var.PlayerMoney[steamID] = 200
	var.PlayerLoans[steamID] = []
	var.Changed = True
	
	
def GetMoney(chatter):
	if not chatter in var.PlayerMoney:
		AddChatter(chatter)
	
	return var.PlayerMoney[chatter]
	
	
def GetDebt(chatter):
	if not chatter in var.PlayerLoans:
		AddChatter(chatter)
		
	return sum(loan.GetAmount() for loan in var.PlayerLoans[chatter])


def GiveMoney(chatter, amount):
	if not chatter in var.PlayerMoney:
		AddChatter(chatter)
		
	var.PlayerMoney[chatter] += amount
	var.Changed = True


def ViewMoney(callback, args):
	chatter = callback.ChatterID
	name = SteamNerd.GetName(chatter)
	debt = GetDebt(chatter)
	
	message = "{} has ${}.\n".format(name, GetMoney(chatter))
	
	if debt > 0:
		message += "{} is ${} in debt.".format(name, GetDebt(chatter))
		
	Say(message)


def BuyLoans(callback, args):
	chatter = callback.ChatterID
	
	if not chatter in var.PlayerLoans:
		AddChatter(chatter)

	if len(args) < 2:
		ViewLoans()
	else:
		loanIndex = -1
	
		try:
			loanIndex = int(args[1]) - 1
		except ValueError:
			Say("That isn't a number!")
			return
		
		if loanIndex < 0 or loanIndex >= len(var.LoanTypes):
			Say("Invalid loan number!")
			return
		
		loanType = var.LoanTypes[loanIndex]
		
		if GetMoney(chatter) < loanType.Fee:
			Say("You can't afford this loan!")
			return
			
		loan = Loan(loanType.Amount, loanType.Interest, loanType.Fee)
		var.PlayerLoans[chatter].append(loan)
		GiveMoney(chatter, -loan.Fee)
		GiveMoney(chatter, loan.Amount)


def Payback(callback, args):
	chatter = callback.ChatterID
	name = SteamNerd.GetName(chatter)
	payback = 0
	loanIndex = -1
	
	if not chatter in var.PlayerLoans:
		AddChatter(chatter)
	
	money = var.PlayerMoney[chatter]	
	loans = var.PlayerLoans[chatter]
	loan = None

	if len(loans) == 0:
		Say("{}, you do not have any loans to payback.".format(name))
		return
	
	
	if len(args) < 2:
		Say("Usage: {0}payback [amount] or {0}payback [loan] [amount]" \
			.format(SteamNerd.CommandChar))
		return
	
	if len(args) == 2:
		try:
			payback = int(args[1])
		except ValueError:
			Say("That is not a number!")
			return
		
		loan = loans[0]
			
	else:
		try: 
			loanIndex = int(args[1]) - 1
		except ValueError:
			Say("That is not a number!")
			return
		
		if loanIndex < 0 or loanIndex > len(loans):
			Say("Invalid loan!")
			return
		
		try:
			payback = int(args[1])
		except ValueError:
			Say("That is not a number!")
			return
			
		loan = loans[loanIndex]
		
	if payback > loan.GetAmount():
		payback = loan.GetAmount()
	
	if money < payback:
		Say("You don't have enough money to payback that much!")
		return
		
	if payback < 0:
		Say("You can't payback a negative amount!")
		return
	
	GiveMoney(chatter, -payback)
	
	if loan.Payoff(payback):
		loans.remove(loan)	
	
	var.Changed = True			
	
			
def ViewLoans():
	message = "Loans:\n"

	for i, loan in enumerate(var.LoanTypes):
		message += \
			"{}.  {:<15} ${:<4}\n".format(i + 1, "Amount:", loan.Amount) + \
			"    {:<15} {:<6.0%}\n".format("Daily Interest:", loan.Interest) + \
			"    {:<15} ${:<3}\n".format("Initial Fee:", loan.Fee)
		
	Say(message)

def ViewDebts(callback, args):
	chatter = callback.ChatterID
	name = SteamNerd.GetName(chatter)
	
	if not chatter in var.PlayerLoans:
		AddChatter(chatter)
		
	loans = var.PlayerLoans[chatter]
	
	if len(loans) == 0:
		Say("{} has no debts!".format(name))
		return
		
	message = "Debts:\n"
	for i, loan in enumerate(loans):
		message += \
		"{}. {:<15} ${:<4}\n".format(i + 1, "Initial Amount", loan.Amount) + \
		"    {:<15} ${:<4}\n".format("Current Amount:", loan.GetAmount()) + \
		"    {:<15} {:<6.0%}\n".format("Daily Interest:", loan.Interest) + \
		"    {:<15} ${:<3}\n".format("Initial Fee:", loan.Fee)
		
	Say(message)
	
	
def Bank(callback, args):
	ViewMoney(callback, args)
	ViewDebts(callback, args)
	
def Give(callback, args):
	if len(args) < 3:
		Say("Usage: {}give [chatter] [amount]".format(SteamNerd.CommandChar))
	
	giver = callback.ChatterID
	name = SteamNerd.GetName(giver)
	recipient = None
	
	for chatter in SteamNerd.Chatrooms[Module.Chatroom].Chatters:
		chatterName = SteamNerd.GetName(chatter).lower()
		
		if chatterName.find(args[1].lower()) >= 0:
			recipient = chatter
			break
	
	if recipient == None:
		Say("{} not found!".format(args[1]))
		return
	
	amount = 0
	recipientName = SteamNerd.GetName(recipient)
	
	try:
		amount = int(args[2])
	except ValueError:
		Say("That is not a number!")
		return
	
	if amount > GetMoney(giver):
		Say("You don't have that much money!")
		return
		
	if amount < 0:
		Say("You must give more than $0.")
		return
	
	GiveMoney(giver, -amount)
	GiveMoney(recipient, amount)

	Say("{} gave {} ${}!".format(name, recipientName, amount))


def Bankrupt(callback, args):
	chatter = callback.ChatterID
	var.PlayerMoney[chatter] = 200
	var.PlayerLoans[chatter] = []
	
	Say("{} declared bankrupcy!"
		.format(SteamNerd.GetName(callback.ChatterID)))
		
	
#Load()
#saveTimer = Timer(60, Save)
#saveTimer.start()

Module.AddCommand(
	"money",
	"View your money.", 
	ViewMoney
)

Module.AddCommand(
	"bank", 
	"View your account information.", 
	Bank
)

Module.AddCommand(
	"debt",
	"View your debts.",
	ViewDebts
)

Module.AddCommand(
	"loan", 
	"Loan money from the bank. Usage: {0}loan and {0}loan [loan type]".format(SteamNerd.CommandChar), 
	BuyLoans
)
Module.AddCommand(
	"payback", 
	"Payback your debt.", 
	Payback
)
Module.AddCommand(
	"bankrupt", 
	"Removes your assets and debt, and gives you $200", 
	Bankrupt
)
Module.AddCommand(
	"give", 
	"Give money to another chatter. Usage: {}give [chatter] [amount]".format(SteamNerd.CommandChar),
	Give 
)