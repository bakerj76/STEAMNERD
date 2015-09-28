from collections import OrderedDict

Module.Name = "Movies"
Module.Description = "Creates a list of movies."

var.Movies = OrderedDict()

class Movie:
	def __init__(self, title, year, date):
		self.title = title
		self.year = year
		self.date = date
	
	def __str__(self):
		return "{} ({}) {}".format(self.title, self.year, self.date)
		
def AddMovie(callback, args):
	if len(args) < 4:
		Say('Usage: {}addmovie [title] [year] [date]'.format(SteamNerd.CommandChar))
		return
		
	movie = Movie(args[1], args[2], args[3])
	var.Movies[movie.year] = movie
	
def ShowMovies(callback, args):
	msg = ''
	
	for year in var.Movies:
		movie = var.Movies[year]
		msg += str(movie) + '\n'
		
	Say(msg)
	
Module.AddCommand('addmovie', 'Adds a movie.', AddMovie)
Module.AddCommand('showmovies', 'Shows all the movies', ShowMovies)