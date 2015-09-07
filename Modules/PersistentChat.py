clr.AddReference("System.Core")
import System
clr.ImportExtensions(System.Linq)

Module.Name = "Persistent Chat"
Module.Description = "Message xXxTrollSlayerxXx to get invited to the chat."
Module.Global = True

def OnFriendMessage(callback, args):
	chat = SteamNerd.Chatrooms.Keys.First()
	SteamNerd.SteamFriends.InviteUserToChat(callback.Sender, chat)
