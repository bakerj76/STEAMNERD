using SteamKit2;

namespace STEAMNERD.Modules
{
    class AnimeRecommendationService : Module
    {
        public AnimeRecommendationService(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Anime Recommendation Service";
            Description = "Recommends anime!";

            AddCommand(
                "ars",
                "Get recommended an anime.",
                Recommend
            );
        }

        public void Recommend(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var name = SteamNerd.ChatterNames[callback.ChatterID];
            SteamNerd.SendMessage(string.Format("{0}, watch Death Note!", name), callback.ChatRoomID, true);
        }
    }
}
