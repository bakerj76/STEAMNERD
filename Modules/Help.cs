using System.Linq;
using SteamKit2;

namespace STEAMNERD.Modules
{
    class Help : Module
    {
        public Help(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "Help";
            Description = "Helps you get help.";

            AddCommand(
                "help",
                string.Format("Helps you get help from Help. Usage: {0}help or {0}help [module]", SteamNerd.CommandChar),
                GetHelp
            );
        }

        public void GetHelp(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            var chat = callback.ChatRoomID;

            if (args.Length == 1)
            {
                var message = "Help is here to help you.\nModules:\n";

                foreach (var module in SteamNerd.Modules)
                {
                    if (module.Name != null && module.Name != "")
                    {
                        message += module.Name + "\n";
                    }
                }

                message += "\nIf you would like more help on a certain module, type {0}help [module]";

                SteamNerd.SendMessage(string.Format(message, SteamNerd.CommandChar), chat, true);
            }
            else
            {
                var modString = args.Skip(1).Aggregate((mod, next) => mod + " " + next);

                var module = SteamNerd.GetModule(modString);

                if (module == null)
                {
                    SteamNerd.SendMessage(string.Format("Module {0} not found!", args[1]), chat, true);
                    return;
                }

                ModuleHelp(module, chat);
            }
        }

        /// <summary>
        /// Prints out help information to a chat
        /// </summary>
        /// <param name="chatRoom"></param>
        public void ModuleHelp(Module module, SteamID chatRoom)
        {
            var message = string.Format("{0}\n{1}\n\n", module.Name, module.Description);

            foreach (var command in module.Commands)
            {
                if (command.Match == "" || command.Description == "")
                {
                    continue;
                }

                message += string.Format("{0}{1,-50}{2}\n", SteamNerd.CommandChar, command.Match, command.Description);
            }

            SteamNerd.SendMessage(message, chatRoom, true);
        }
    }
}
