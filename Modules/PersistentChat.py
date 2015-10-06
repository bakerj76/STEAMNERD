import System

clr.AddReference("System.Core")
clr.ImportExtensions(System.Linq)

Module.Name = "Persistent Chat"
Module.Description = "Message xXxTrollSlayerxXx to get invited to the chat."
Module.Global = True


def OnFriendMessage(callback, args):
    if len(SteamNerd.Chatrooms) == 0:
        return

    chat = SteamNerd.Chatrooms.Keys.First()
    SteamNerd.SteamFriends.InviteUserToChat(callback.Sender, chat)
