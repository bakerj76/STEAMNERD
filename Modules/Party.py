Module.Name = "Party"
Module.Description = "Let's party~"

def LetsParty(callback, args):
	friends = SteamNerd.SteamFriends
	count = 0

	for i in xrange(friends.GetFriendCount()):
		friend = friends.GetFriendByIndex(i)
		
		if not SteamNerd.ChatterNames.Keys.Contains(friend):
			friends.InviteUserToChat(friend, Module.Chatroom)
			count += 1
	
	Say('INVITING {} IDIOTS TO THE CHAT'.format(count))
	
Module.AddCommand("party", "Invites everyone for a party.", LetsParty)