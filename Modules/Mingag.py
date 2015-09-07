import SteamKit2

Module.Name = "Mingag"
Module.Description =  "If you say \"Mingag\", it alerts Mingag that you're talking shit. Also, it logs Mingag's chat to blackmail him."
Module.Global = True

def OnChatMessage(callback, args):
    mingag = SteamKit2.SteamID("STEAM_0:0:5153026")
    message = callback.Message

    if message.lower().find('mingag') > 0:
        print "Messaging Mingag."
        SteamNerd.SendMessage(
            str.format('{}: {}', SteamNerd.GetName(callback.ChatterID), message), 
            mingag
        )