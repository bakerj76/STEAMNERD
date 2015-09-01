using System;
using System.Collections.Generic;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class AnimeRecommendationService : Module
    {
        /// <summary>
        /// A list of anime to recommend.
        /// </summary>
        private List<string> _animes;
        private Random _rand;

        public AnimeRecommendationService(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Anime Recommendation Service";
            Description = "Recommends anime!";

            AddCommand(
                "ars",
                "Get recommended an anime.",
                Recommend
            );

            AddCommand(
                new[] { "ars", "add" },
                "Add an anime to the ARS.",
                AddAnime,
                3
            );

            _animes = new List<string> { "Death Note" };
            _rand = new Random();
        }

        public void Recommend(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (args.Length > 1) { return; }

            var name = SteamNerd.ChatterNames[callback.ChatterID];
            var randomAnime = _animes[_rand.Next(_animes.Count)];
            SteamNerd.SendMessage(string.Format("{0}, watch {1}!", name, randomAnime), callback.ChatRoomID, true);
        }

        public void AddAnime(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            _animes.Add(args[2]);
        }
    }
}
