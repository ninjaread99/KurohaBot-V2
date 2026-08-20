using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KurohaBotV2
{
    internal static class StateSaveLoad
    {
        private static JsonSerializerOptions JsonOptions = new JsonSerializerOptions();
        static StateSaveLoad()
        {
            JsonOptions.WriteIndented = true;
        }
        //Cheap backwards compatability trick, reference BotState instance directly.
        public static void Save(BotState state)
        {
            state?.Save();
        }
        //Cheap backwards compatability trick, reference BotState directly.
        public static BotState? Load()
        {
            BotState? result = BotState.Load();
            return result;
        }
    }

}
