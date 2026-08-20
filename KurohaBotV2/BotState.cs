using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace KurohaBotV2
{
    internal class BotState
    {
        public string? NextPageToken;
        public ChatQueue queue;
        public string? StreamId;

        private static JsonSerializerOptions JsonOptions = new JsonSerializerOptions();
        static BotState()
        {
            JsonOptions.WriteIndented = true;
        }

        public BotState() { }

        public void Save()
        {
            this.NextPageToken = Bot.NextPageToken;
            this.queue = Bot.queue;
            this.StreamId = Bot.stream.Id;
            string tempPath = Path.Combine(AppContext.BaseDirectory, "State.json.tmp");
            string finalPath = Path.Combine(AppContext.BaseDirectory, "State.json");
            string stateJson = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(tempPath, stateJson);
            File.Move(tempPath, finalPath, true);
        }

        public static BotState? Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "State.json");
            if (!File.Exists(path))
            {
                return null;
            }
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BotState>(json);
        }
    }
}
