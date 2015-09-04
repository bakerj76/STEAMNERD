using System;
using System.Runtime.Remoting.Services;
using SteamKit2.GC.Dota.Internal;
using SteamNerd.Modules;

namespace SteamNerd
{
    class Program
    {
        public static void Main(string[] args)
        {
            //Login(args);
            var asdf = new ModuleManager();
        }

        public static void Login(string[] args)
        {
            string user, password;

            while (true)
            {
                if (args.Length != 2)
                {
                    FancyLogIn(out user, out password);

                    if (user == "")
                    {
                        break;
                    }
                }
                else
                {
                    user = args[0];
                    password = args[1];
                }

                var steamNerd = new SteamNerd(user, password);
                LoadModules(steamNerd);
                steamNerd.Connect();
            }
        }

        public static void LoadModules(SteamNerd steamNerd)
        {
            steamNerd.AddModule(new Money(steamNerd));
            steamNerd.AddModule(new LingT(steamNerd));
            steamNerd.AddModule(new DiceRoll(steamNerd));
            steamNerd.AddModule(new PersistentChat(steamNerd));
            steamNerd.AddModule(new Mingag(steamNerd));
            steamNerd.AddModule(new TrollSlayer(steamNerd));
            steamNerd.AddModule(new Party(steamNerd));
            steamNerd.AddModule(new Roulette(steamNerd));
            steamNerd.AddModule(new Help(steamNerd));
            steamNerd.AddModule(new Todo(steamNerd));
            steamNerd.AddModule(new LetterGames(steamNerd));
            steamNerd.AddModule(new AnimeRecommendationService(steamNerd));
            steamNerd.AddModule(new Blackjack(steamNerd));
            steamNerd.AddModule(new Democracy(steamNerd));
            steamNerd.AddModule(new Duel(steamNerd));
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
