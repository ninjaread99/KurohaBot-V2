using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.Util.Store;

using Youtube.Api.V3;

using Serilog;

using Grpc.Core;
using Grpc.Net.Client;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Security;
using System.ComponentModel.Design;
using System.Globalization;
using System.Text;
using System.Diagnostics.Tracing;

namespace KurohaBotV2
{
    internal static class MessageHandler
    {
        private static DateTime LastHelpIssued = DateTime.MinValue;
        
        public static void HandleMessage(LiveChatMessage message)
        {
            string text = message.Snippet.DisplayMessage;
            if (text.Length < 1)
            {
                return;
            }
            if (!text.StartsWith('!'))
            {
                return;
            }
            string sender = message.AuthorDetails.DisplayName;
            string senderId = message.AuthorDetails.ChannelId;

            string[] textParts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = textParts[0].Substring(1);
            if (command.Equals("help"))
            {
                if (DateTime.UtcNow - LastHelpIssued > TimeSpan.FromMinutes(1))
                {
                    LastHelpIssued = DateTime.UtcNow;
                    HelpChatOutput();
                }
            }

            else if (command.Equals("join"))
            {
                Log.Debug("Adding {Sender} ({SenderId}) to queue", sender, senderId);
                Bot.queue.Join(sender, senderId);
            }
            else if (command.Equals("leave"))
            {
                Log.Debug("Removing {Sender} ({SenderId}) from queue", sender, senderId);
                Bot.queue.Leave(senderId);
            }


            else
            {
                return;
            }

        }

        /// <summary>
        /// Outputs help message for chatters
        /// </summary>
        private static void HelpChatOutput()
        {
            string message = "Queue commands: !join, !leave. Misc: !help";
            
        }



        
    }
}
