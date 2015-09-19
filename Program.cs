using System;

namespace SteamNerd
{
    class Program
    {
        public static void Main(string[] args)
        {
            var steamNerd = new SteamNerd();

            if (args.Length >= 2)
            {
                steamNerd.Connect(args[0], args[1]);
            }
            else
            {
                steamNerd.Connect();
            }
        }
    }
}
