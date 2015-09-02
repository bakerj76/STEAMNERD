using System.Timers;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Countdown
    {
        public Timer MainTimer;
        private Timer[] _countdownTimers;

        /// <summary>
        /// Creates a 
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="time"></param>
        /// <param name="countdownStart"></param>
        public Countdown(SteamNerd steamNerd, SteamID chat, ElapsedEventHandler callback, float seconds, int countdownStart)
        {
            _countdownTimers = new Timer[countdownStart];

            MainTimer = new Timer(seconds * 1000);
            MainTimer.AutoReset = false;
            MainTimer.Elapsed += callback;
            MainTimer.Start();

            for (var i = 1; i <= countdownStart; i++)
            {
                if (seconds - i <= 0) return;

                var timer = new Timer(seconds - i);
                var countdownString = string.Format("{0}...", i);

                timer = new Timer((seconds - i) * 1000);
                timer.AutoReset = false;
                timer.Elapsed += (src, e) => steamNerd.SendMessage(countdownString, chat);
                timer.Start();

                _countdownTimers[i - 1] = timer;
            }
        }

        public void Stop()
        {
            MainTimer.Stop();

            foreach (var timer in _countdownTimers)
            {
                timer.Stop();
            }
        }
    }
}
