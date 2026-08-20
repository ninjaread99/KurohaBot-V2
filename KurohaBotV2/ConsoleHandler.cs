using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
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
            if (command.Equals("help"))
            {
                string helpMessage = "Next: show next person in queue, and show name in chat\nclose: disable entry to queue\nopen: enable entry to queue\nclear: clears the current queue\nlist: lists the current queue\nclearstale: clears the stale list\nnostale: allows users to rejoin queue after being selected\nyesstale: prevents users from rejoining queue after being selected.";
                Log.Information(helpMessage);
            }
            else if (command.Equals("next"))
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
            else if (command.Equals("clearstale") || command.Equals("clear stale"))
            {
                Bot.queue.ClearStale();
                Log.Information("Cleared stale list");
            }
            else if (command.Equals("nostale") || command.Equals("no stale"))
            {
                Bot.queue.DisableStaleCheck();
                Log.Information("Disabled stale check");
            }
            else if (command.Equals("enablestale") || command.Equals("enable stale") || command.Equals("enable stale check") || command.Equals("enablestalecheck") || command.Equals("yesstale") ||  command.Equals("yes stale"))
            {
                Bot.queue.EnableStaleCheck();
                Log.Information("Enabled stale check");
            }

            else
            {
                Log.Warning("Unknown command: {Command}", command);
            }
        }
    }
}
