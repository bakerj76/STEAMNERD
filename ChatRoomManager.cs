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

        /// <summary>
        /// Creates a Chat Room Manager that handles chat rooms.
        /// </summary>
        /// <param name="steamNerd">
        /// The SteamNerd instance.
        /// </param>
        /// <param name="autoJoinPath">
        /// The file containing the auto-joined chat rooms.
        /// </param>
        public ChatRoomManager(SteamNerd steamNerd, string autoJoinPath)
        {
            _steamNerd = steamNerd;
            _autoJoinPath = autoJoinPath;
            _chatrooms = new Dictionary<SteamID, ChatRoom>();
        }

        private void LoadAutoJoinChatFile()
        {
            // Open the file or create it if it doesn't exist.
            using (var file = new StreamReader(
                new FileStream(_autoJoinPath, FileMode.OpenOrCreate)
            ))
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

        /// <summary>
        /// Adds a chat room that is automatically joined on start-up.
        /// </summary>
        /// <param name="steamID">The SteamID of the chat room.</param>
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
        /// <returns>The created chat room.</returns>
        public ChatRoom AddChatRoom(SteamID steamID, string name)
        {
            var chatRoom = new ChatRoom(_steamNerd, steamID, name);
            _chatrooms[steamID] = chatRoom;

            return chatRoom;
        }

        /// <summary>
        /// Gets information for a currently joined chat room.
        /// </summary>
        /// <param name="steamID"></param>
        /// <returns>The chat room.</returns>
        public ChatRoom GetChatRoom(SteamID steamID)
        {
            return _chatrooms[steamID];
        }

        /// <summary>
        /// Checks if the chat room exists.
        /// </summary>
        /// <param name="steamID">The chat room's steamID.</param>
        /// <returns>True if the chat room exists, false otherwise.</returns>
        public bool ChatRoomExists(SteamID steamID)
        {
            return _chatrooms.ContainsKey(steamID);
        }
    }
}
