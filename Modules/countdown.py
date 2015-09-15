from threading import Timer

class Countdown(object):
	def __init__(self, interval, start, callback, say):
		self.Interval = interval
		self.Start = start
		self.Callback = callback
		self.Say = say
		self.Timers = []
		
	def start(self):
		self.Timers.append(Timer(self.Interval, self.Callback))
		
		for i in xrange(1, self.Start + 1):
			if self.Interval - i < 0:
				break
				
			count = "{}...".format(i)
			self.Timers.append(Timer(self.Interval - i, lambda count=count: self.Say(count)))
		
		for timer in self.Timers:
			timer.start()
			
	def stop(self):
		for timer in self.Timers:
			timer.cancel()
	