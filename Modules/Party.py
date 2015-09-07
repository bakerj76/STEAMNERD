Module.Name = "Party"
Module.Description = "Let's party~"

def lets_party(callback, args):
	friends = SteamNerd.SteamFriends
	count = 0

	for i in xrange(friends.GetFriendCount()):
		friend = friends.GetFriendByIndex(i)
		
		if not SteamNerd.ChatterNames.Keys.Contains(friend):
			friends.InviteUserToChat(friend, Module.Chatroom)
			count += 1
	
	SteamNerd.SendMessage(
		str.format('INVITING {} IDIOTS TO THE CHAT', count),
		Module.Chatroom
	)
	
Module.AddCommand("party", "Invites everyone for a party.", lets_party)