import os
import cPickle as pickle
from threading import Timer

Module.Name = "TODO"
Module.Description = "Things to do."
Module.Global = True

var.TodoList = []
var.File = "TODO.p"
var.Path = os.path.join(os.getenv('APPDATA'), "SteamNerd", var.File)
var.Changed = False

def LoadTodo():
	try:
		var.TodoList = pickle.load(open(var.Path, 'rb'))
	except:
		pass

def SaveTodo():
	if var.Changed:
		var.Changed = False
		pickle.dump(var.TodoList, open(var.Path, 'wb'))

def PrintTodo(callback, args):
	message = ""
	
	if len(var.TodoList) == 0:
		message = "Wow! There's nothing to do!"
	else:
		message = "TODO:\n"
		
		for i in xrange(len(var.TodoList)):
			message += "{}. {}\n".format(i + 1, var.TodoList[i])
		
	Say(message, callback.ChatRoomID)

def AddTodo(callback, args):
	if len(args) < 3:
		Say(
			"Usage: {0}todo add [item] or {0}todo add [line number] [item]".format(SteamNerd.CommandChar),
			callback.ChatRoomID
		)
		return

	line = CheckNumber(args[2])
	
	tempList = []
	tempItem = ""
	skip = 2 if line < 0 else 3
	
	for arg in args[skip:]:
		if arg.find('\n') < 0:
			tempItem += arg + " "
		else:
			tempList.append(tempItem + arg.replace('\n', ''))
			tempItem = ""
	
	tempList.append(tempItem)
	
	if line >= 0:
		var.TodoList = var.TodoList[:line] + tempList + var.TodoList[line:]
	else:
		var.TodoList.extend(tempList)
		
	var.Changed = True

def RemoveTodo(callback, args):
	if len(args) < 3:
		Say(
			"Usage: {}todo remove [line number]".format(SteamNerd.CommandChar),
			callback.ChatRoomID
		)
		return
		
	line = CheckNumber(args[2])
	
	if line >= 0:
		del var.TodoList[line]
		var.Changed = True

def CheckNumber(arg):
	line = 0
		
	try:
		line = int(arg) - 1
	except ValueError:
		return -1
	
	if line < 0 or line > len(var.TodoList):
		return -1
	
	return line

LoadTodo()
saveTimer = Timer(60, SaveTodo)
saveTimer.start()
	
Module.AddCommand("todo", "Print the todo list.", PrintTodo)

Module.AddCommand(
	("todo", "add"), 
	"Add to the todo list. Usage: {}todo add [item]".format(SteamNerd.CommandChar),
	AddTodo
)
Module.AddCommand(
	("todo", "remove"),
	"Removes a thing to do from the todo list. Usage: {}todo remove [item number]".format(SteamNerd.CommandChar),
	RemoveTodo
)
