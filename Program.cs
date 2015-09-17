using System;

namespace SteamNerd
{
    class Program
    {
        public static void Main(string[] args)
        {
            Login(args);
        }

        public static void Login(string[] args)
        {
            string user, password;

            CheckArgs(args, out user, out password);
            if (user == "") return;

            var steamNerd = new SteamNerd();

            while (true)
            {
                try
                {
                    steamNerd.Connect(user, password);
                }
                catch (ArgumentException e)
                {
                    steamNerd.IsRunning = false;
                    Console.WriteLine(e.Message);

                    CheckArgs(args, out user, out password);
                    if (user == "") return;
                }
            }
        }

        /// <summary>
        /// Checks if there are 2 string arguments and sets username and 
        /// password to them. If there aren't, then it uses FancyLogIn.
        /// </summary>
        /// <param name="args">The program arguments.</param>
        /// <param name="user"></param>
        /// <param name="password"></param>
        private static void CheckArgs(string[] args, out string user, out string password)
        {
            if (args.Length != 2)
            {
                FancyLogIn(out user, out password);
                return;
            }

            user = args[0];
            password = args[1];
        }

        /// <summary>
        /// Handles input for a "fancy" login. It doesn't show the password
        ///  when the user types it.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="password"></param>
        private static void FancyLogIn(out string user, out string password)
        {
            // Get the username
            Console.Write("Username: ");
            user = Console.ReadLine();

            // Get the password
            Console.Write("Password: ");
            var key = Console.ReadKey(true);
            password = "";

            while (key.Key != ConsoleKey.Enter)
            {
                password += key.KeyChar;
                key = Console.ReadKey(true);
            }

            Console.WriteLine();
        }
    }
}
