using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;

namespace SteamNerd
{
    public class ChatRoomManager
    {
        /// <summary>
        /// The chat rooms to automatically join.
        /// </summary>
        private string _autoJoinPath;
        private SteamNerd _steamNerd;
        private Dictionary<SteamID, ChatRoom> _chatrooms;

        public ChatRoomManager(SteamNerd steamNerd, string autoJoinPath)
        {
            _steamNerd = steamNerd;
            _autoJoinPath = autoJoinPath;
        }

        private void LoadAutoJoinChatFile()
        {
            // If the file doesn't exist, create it.
            if (!File.Exists(_autoJoinPath))
            {
                File.Create(_autoJoinPath);
            }

            using (var file = new StreamReader(File.OpenRead(_autoJoinPath)))
            {
                // Read the file and join each chat SteamID.
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();

                    var steamID = new SteamID(line);
                    _steamNerd.SteamFriends.JoinChat(steamID);
                }
            }
        }

        public void AddChatRoomToAutoJoin(SteamID steamID)
        {
            using (var file = new StreamWriter(File.OpenWrite(_autoJoinPath)))
            {
                file.WriteLine(steamID.Render());
                file.Flush();
            }
        }

        /// <summary>
        /// Adds a chat room to the list of currently joined chat rooms.
        /// </summary>
        /// <param name="steamID">The SteamID of the chat room.</param>
        /// <param name="name">The name of the chat room.</param>
        public void AddChatRoom(SteamID steamID, string name)
        {
            var chatRoom = new ChatRoom(_steamNerd, steamID, name);
            _chatrooms[steamID] = chatRoom;
        }

        /// <summary>
        /// Gets information for a currently joined chat room.
        /// </summary>
        /// <param name="steamID"></param>
        /// <returns></returns>
        public ChatRoom GetChatRoom(SteamID steamID)
        {
            return _chatrooms[steamID];
        }
    }
}
