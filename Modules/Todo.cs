using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Timers;
using SteamKit2;

namespace SteamNerd.Modules
{
    class Todo : Module
    {
        private const double SAVE_TIME = 60000;
        private static readonly string PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"SteamNerd"
        );
        private const string FILE_NAME = @"todo.txt";

        private List<string> _todoList;
        private bool _changed;
        private Timer _saveTimer;

        public Todo(SteamNerd steamNerd) : base(steamNerd)
        {
            Name = "TODO";
            Description = "Things to do.";

            AddCommand(
                "todo",
                "Print the todo list.",
                GetTodo
            );

            // Dummy command
            AddCommand(
                "todo add [thing to do]",
                "Adds a thing to do to the todo list.",
                null
            );

            // Dummy command
            AddCommand(
                "todo add [position] [thing to do]",
                "Adds a thing to do to the todo list at a position.",
                null
            );

            // Dummy command
            AddCommand(
                "todo remove [position]",
                "Adds a thing to do to the todo list.",
                null
            );

            _todoList = new List<string>();

            _changed = false;
            _saveTimer = new Timer(SAVE_TIME);
            _saveTimer.Elapsed += (src, e) => Save();
            _saveTimer.Start();

            Load();
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
                    writer.Write(_todoList.Count);

                    foreach (var todo in _todoList)
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
                    _todoList.Add(reader.ReadString());
                }
            }
        }

        public void GetTodo(SteamFriends.ChatMsgCallback callback, string[] args)
        {
            if (args.Length == 1)
            {
                PrintTodo(callback);
            }
            else
            {
                var subcommand = args[1];

                switch (subcommand)
                {
                    case "add":
                        AddTodo(callback.ChatRoomID, args);
                        break;
                    case "remove":
                        RemoveTodo(callback.ChatRoomID, args);
                        break;
                    default:
                        var message = string.Format("Unknown subcommand. Use {0}help for help.", SteamNerd.CommandChar);
                        SteamNerd.SendMessage(message, callback.ChatRoomID, true);
                        break;
                }
            }
        }

        private void PrintTodo(SteamFriends.ChatMsgCallback callback)
        {
            var chat = callback.ChatRoomID;

            if (_todoList.Count == 0)
            {
                SteamNerd.SendMessage("Wow! There's nothing to do!", chat, true);
            }
            else
            {
                var message = "TODO:\n";

                for (var i = 0; i < _todoList.Count; i++)
                {
                    // The most real i
                    var theRealI = i + 1;

                    message += string.Format("{0}. {1}\n", theRealI, _todoList[i]);
                }

                SteamNerd.SendMessage(message, chat, true);
            }
        }

        private void AddTodo(SteamID chat, string[] args)
        {
            if (args.Length < 3)
            {
                return;
            }

            int position;
            if (int.TryParse(args[2], out position))
            {
                if (position > _todoList.Count || position < 1)
                {
                    position = -1;
                }
            }
            else
            {
                position = -1;
            }

            var temp = new List<string>();
            var sentence = "";
            var skip = position != -1 ? 3 : 2;

            foreach (var word in args.Skip(skip))
            {
                if (word.Contains("\n"))
                {
                    sentence += word.Replace("\n", "");
                    temp.Add(sentence.Trim());

                    sentence = "";
                }
                else
                {
                    sentence += word + " ";
                }
            }

            temp.Add(sentence.Trim());

            if (position == -1)
            {
                _todoList.AddRange(temp);
            }
            else
            {
                _todoList.InsertRange(position - 1, temp);
            }

            _changed = true;
        }

        private void RemoveTodo(SteamID chat, string[] args)
        {
            if (args.Length < 2)
            {
                SteamNerd.SendMessage("Invalid position!", chat, true);
            }

            int position;
            if (!int.TryParse(args[2], out position) || position > _todoList.Count || position < 1)
            {
                SteamNerd.SendMessage("Invalid position!", chat, true);
                return;
            }

            _todoList.RemoveAt(position - 1);
            _changed = true;
        }
    }
}
