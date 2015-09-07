Module.Name = "Money"
Module.Description = "Handles money."

var.Money = {}
var.Loans = {}

class Loan:
	def __init__(self):
		self.Amount = 0
		self.Interest = 0.0
		self.StartTime = 0
		
	def GetFees(self):
		pass

def AddChatter(steamID):
	var.Money[steamID] = 200
	var.Loans[steamID] = 0
	
def ViewMoney(callback, args):
	chatter = callback.ChatterID
	name = SteamNerd.ChatterNames[chatter]
	
	if not chatter in var.Money:
		AddChatter(chatter)
		
	Say("{} has ${}.".format(name, var.Money[chatter]))
	
def ViewDebt(callback, args):
	chatter = callback.ChatterID
	name = SteamNerd.ChatterNames[chatter]
	
	if not chatter in var.Loans:
		AddChatter(chatter)
		
	Say("{} is ${} in debt.".format(name, var.Loans[chatter]))

Module.AddCommand("money", "View your money.", ViewMoney)
Module.AddCommand("debt", "View your debt.", ViewDebt)
Module.AddCommand("bank", "View your account information.", None)
Module.AddCommand("loan", "Loan money from the bank.", None)
Module.AddCommand("payback", "Payback your debt.", None)