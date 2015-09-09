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
            

            if (args.Length != 2)
            {
                FancyLogIn(out user, out password);

                if (user == "")
                {
                    return;
                }
            }
            else
            {
                user = args[0];
                password = args[1];
            }

            var steamNerd = new SteamNerd(user, password);

            while (true)
            {
                steamNerd.Connect();
            }
        }

        static void FancyLogIn(out string user, out string password)
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
