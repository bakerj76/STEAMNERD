using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class AnimeRecommendationService : Module
    {
        private const double SAVE_TIME = 60000;
        private static readonly string PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"SteamNerd"
        );
        private const string FILE_NAME = @"anime.txt";

        /// <summary>
        /// A list of anime to recommend.
        /// </summary>
        private List<string> _animes;

        private Random _rand;

        private bool _changed;
        private Timer _saveTimer;

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

            _animes = new List<string> { };
            _rand = new Random();

            _changed = false;

            _saveTimer = new Timer(SAVE_TIME);
            _saveTimer.Elapsed += (src, e) => Save();
            _saveTimer.Start();

            Load();
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
            var anime = args.Skip(2).Aggregate((current, next) => current + " " + next);
            _animes.Add(anime);
            _changed = true;
        }

        /// <summary>
        /// Save the todo list
        /// </summary>
        private void Save()
        {
            if (_changed)
            {
                var path = Path.Combine(PATH, FILE_NAME);

                using (var fileStream = File.Open(path, FileMode.Create))
                {
                    var writer = new BinaryWriter(fileStream);
                    writer.Write(_animes.Count);

                    foreach (var todo in _animes)
                    {
                        writer.Write(todo);
                    }

                    writer.Flush();
                }

                _changed = false;
            }
        }

        /// <summary>
        /// Load the todo list
        /// </summary>
        private void Load()
        {
            var path = Path.Combine(PATH, FILE_NAME);

            if (!Directory.Exists(PATH))
            {
                Directory.CreateDirectory(PATH);
            }

            if (!File.Exists(path))
            {
                File.Create(path);
                return;
            }

            using (var fileStream = File.Open(path, FileMode.Open))
            {
                var reader = new BinaryReader(fileStream);
                uint count = 0;

                try
                {
                    count = reader.ReadUInt32();
                }
                // File is empty
                catch (EndOfStreamException)
                {
                    return;
                }

                for (var i = 0; i < count; i++)
                {
                    _animes.Add(reader.ReadString());
                }
            }
        }
    }
}
