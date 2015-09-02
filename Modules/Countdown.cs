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
        public Countdown(SteamNerd steamNerd, SteamID chat, ElapsedEventHandler callback, float time, int countdownStart)
        {
            MainTimer = new Timer(time);
            MainTimer.AutoReset = false;
            MainTimer.Elapsed += callback;
            MainTimer.Start();

            for (var i = 1; i <= countdownStart; i++)
            {
                if (time - i <= 0) return;

                var timer = new Timer(time - i);
                var countdownString = string.Format("{0}...", i);

                timer = new Timer(30000 - i * 1000);
                timer.AutoReset = false;
                timer.Elapsed += (src, e) => steamNerd.SendMessage(countdownString, chat);
                timer.Start();
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
