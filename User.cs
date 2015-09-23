using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;

namespace SteamNerd
{
    /// <summary>
    /// A class containing Steam user information.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The user's SteamID.
        /// </summary>
        public SteamID SteamID { get; private set; }
        
        /// <summary>
        /// The user's persona name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The user's current persona state (Online, Away, etc.).
        /// </summary>
        public EPersonaState PersonaState { get; set; }

        /// <summary>
        /// If the user can do SteamNerd admin stuff.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// The current SteamNerd instance.
        /// </summary>
        private SteamNerd _steamNerd;

        /// <summary>
        /// Creates a SteamNerd user that contains user information.
        /// </summary>
        /// <param name="steamNerd">The SteamNerd instance.</param>
        /// <param name="steamID">The user's SteamID.</param>
        /// <param name="name">The user's persona name.</param>
        /// <param name="personaState">The user's persona state.</param>
        public User(SteamNerd steamNerd, SteamID steamID, string name, EPersonaState personaState)
        {
            _steamNerd = steamNerd;
            SteamID = steamID;
            Name = name;
            PersonaState = personaState;
        }

        /// <summary>
        /// Sends a private message to the user.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void SendMessage(string message)
        {
            _steamNerd.SendMessage(message, SteamID);
        }
    }
}
