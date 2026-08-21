//#define DEBUG_SAVING

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace KurohaBotV2
{
    internal class BotState
    {
        public string? NextPageToken;
        public ChatQueue queue;
        public string? StreamId;
#if DEBUG_SAVING
        [JsonIgnore]
        public static int saves;
#endif

        private static JsonSerializerOptions JsonOptions = new JsonSerializerOptions();
        static BotState()
        {
            JsonOptions.WriteIndented = true;
            JsonOptions.IncludeFields = true;
        }

        public BotState() { }


        /// <summary>
        /// Updates the bot state, then writes state to a file.
        /// </summary>
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
#if DEBUG_SAVING
            SaveDebugHelper();
#endif
        }

        public static BotState? Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "State.json");
            if (!File.Exists(path))
            {
                return null;
            }
            string json = File.ReadAllText(path);
            try
            {
                return JsonSerializer.Deserialize<BotState>(json, JsonOptions);
            }
            catch (JsonException e)
            {
                Log.Warning("Malformed Json found, defaulting to first-time initialization.");
                return null;
            }
        }

#if DEBUG_SAVING
        private static void SaveDebugHelper()
        {
            Log.Verbose("Save count: {SaveCount}", saves);
            saves++;
        }
#endif
    }
}
