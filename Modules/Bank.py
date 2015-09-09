import math
from datetime import datetime, timedelta

Module.Name = "Bank"
Module.Description = "Handles money."

class Loan:
	def __init__(self, amount, interest, fee):
		self.Amount = amount
		self.Interest = interest
		self.InterestCheck = datetime.now()
		self.Fee = fee
		
	def GetAmount(self):
		# Divide the difference in seconds by the seconds in a day, then 
		# divide that by the seconds in the day.
		interestTime = ((datetime.now() - self.InterestCheck).total_seconds() \
			/ timedelta(1).total_seconds()) \
				/ 86400
		print interestTime
		return self.Amount * math.e ** (self.Interest * interestTime)
		

var.PlayerMoney = {}
var.PlayerLoans = {}
var.LoanTypes = [Loan(100, 0.10, 10), Loan(500, 0.15, 50), Loan(1000, 0.20, 100)]
		
def AddChatter(steamID):
	var.PlayerMoney[steamID] = 200
	var.PlayerLoans[steamID] = []
	
def ViewMoney(callback, args):
	chatter = callback.ChatterID
	name = SteamNerd.ChatterNames[chatter]
	
	if not chatter in var.PlayerMoney:
		AddChatter(chatter)
		
	Say("{} has ${}.".format(name, var.PlayerMoney[chatter]))
	
def ViewDebt(callback, args):
	chatter = callback.ChatterID
	name = SteamNerd.ChatterNames[chatter]
	
	if not chatter in var.PlayerLoans:
		AddChatter(chatter)
	
	debt = sum(loan.Amount for loan in var.PlayerLoans[chatter])

	for loan in var.PlayerLoans[chatter]:
		print loan.GetAmount()
	
	
	Say("{} is ${} in debt.".format(name, debt))

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
		
		if loanIndex < 0 or loanIndex > len(var.LoanTypes):
			Say("Invalid loan number!")
			return
		
		loanType = var.LoanTypes[loanIndex]
		loan = Loan(loanType.Amount, loanType.Interest, loanType.Fee)
		var.PlayerLoans[chatter].append(loan)
		var.PlayerMoney[chatter] += loan.Amount

def ViewLoans():
	message = "Loans:\n"

	for i, loan in enumerate(var.LoanTypes):
		message += \
			"{}.  {:<15} ${:<4}\n".format(i + 1, "Amount:", loan.Amount) + \
			"    {:<15} {:<6.0%}\n".format("Daily Interest:", loan.Interest) + \
			"    {:<15} ${:<3}\n".format("Initial Fee:", loan.Fee)
		
	Say(message)

Module.AddCommand(
	"money", 
	"View your money.", 
	ViewMoney
)

Module.AddCommand(
	"debt", 
	"View your debt.", 
	ViewDebt
)

Module.AddCommand(
	"bank", 
	"View your account information.", 
	None
)

Module.AddCommand(
	"loan", 
	"Loan money from the bank. Usage: {0}loan and {0}loan [loan number]".format(SteamNerd.CommandChar), 
	BuyLoans
)
Module.AddCommand(
	"payback", 
	"Payback your debt.", 
	None
)
Module.AddCommand(
	"bankrupt", 
	"Removes your assets and debt, and gives you $200", 
	None
)
Module.AddCommand(
	"give", 
	"Give money to another chatter. Usage: {}give [chatter] [amount]".format(SteamNerd.CommandChar),
	None
)