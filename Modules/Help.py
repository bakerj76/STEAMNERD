Module.Name = "Help"
Module.Description = "Help. When you need it, where you need it."
Module.Global = True

def GetHelp(callback, args):
	if len(args) == 1:
		ListModules(callback)
	else:
		GetHelpOnModule(callback, args) 
	
def ListModules(callback):
	message = "Help is here to help you.\nModules:\n"
	
	for module in SteamNerd.GetModules(callback.ChatRoomID):
		message += module.Name + "\n"
		
	message += "\nIf you would like more help on a certain module, type {}help [module]."
	message = message.format(SteamNerd.CommandChar)
	
	SteamNerd.SendMessage(message, callback.ChatRoomID)
	
def GetHelpOnModule(callback, args):
	moduleName = ' '.join(args[1:])
	module = SteamNerd.GetModule(moduleName, callback.ChatRoomID)
	
	if module == None:
		SteamNerd.SendMessage("{} not found!".format(moduleName), callback.ChatRoomID)
	else:
		message = "{}\n{}\n\n".format(module.Name, module.Description)
		
		for command in module.Commands:
			message += "{}{:<50}{}\n".format(SteamNerd.CommandChar, ' '.join(command.Match), command.Description)
	
		SteamNerd.SendMessage(message, callback.ChatRoomID)
	
Module.AddCommand("help", 
	"Helps you get help from Help. Usage: {0}help or {0}help [module]".format(SteamNerd.CommandChar),
	GetHelp
)