import random

Module.Name = "Greetings"
Module.Description = "A friendly greeting."
Module.Global = True

var.greetings = ('selamat pagi', 'ling t', 'selamat malam')

def OnChatMessage(callback, args):
	message = callback.Message.lower()
	
	if message.find('ling t') != -1:
		SteamNerd.SendMessage(random.choice(var.greetings), callback.ChatRoomID)
