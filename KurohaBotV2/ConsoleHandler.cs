using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serilog;

namespace KurohaBotV2
{
    internal static class ConsoleHandler
    {
        public static void HandleConsoleCommand(string command)
        {
            command = command.Trim().ToLower();
            if (command.Equals("next"))
            {
                ChatMember? next = Bot.queue.GetNext();
                if (next == null)
                {
                    Log.Information("No one is in the queue.");
                }
                else
                {
                    Log.Information("Next in queue: {Name} ({Id})", next.name, next.id);
                    MessageSender.SendMessage($"Next in queue: {next.name}").Wait();
                }
            }
            else if (command.Equals("close") || command.Equals("pause"))
            {
                Bot.queue.QueueOpen = false;
                Log.Information("Queue is now closed");
                MessageSender.SendMessage("Queue is now closed.").Wait();
            }
            else if (command.Equals("open") || command.Equals("resume"))
            {
                Bot.queue.QueueOpen = true;
                Log.Information($"Queue is now open");
                MessageSender.SendMessage("Queue is now open.").Wait();
            }
            else if (command.Equals("clear"))
            {
                Bot.queue.ClearQueue();
                Log.Information("Queue cleared");
            }
            else if (command.Equals("list"))
            {
                Log.Information("Current queue: {Queue}", string.Join(", ", Bot.queue.GetQueue().Select(m => m.name)));
            }
            else
            {
                Log.Warning("Unknown command: {Command}", command);
            }
        }
    }
}
