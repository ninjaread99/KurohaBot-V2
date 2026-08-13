using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serilog;

namespace KurohaBotV2
{
    internal static class MessageSender
    {
        /// <summary>
        /// Splits text to an array to keep each part under MaxLen. Each message ends with a formatted "({currentPartNumber/TotalParts})"
        /// </summary>
        /// <param name="text">The string to be split</param>
        /// <param name="MaxLen">Max length of a string, including the ending text. Default is 200</param>
        /// <returns>Array of all formatted parts</returns>
        public static string[] SplitText(string text, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(text))
                return new[] { text ?? string.Empty };

            // Start with a naive split (no suffix reserved) to get an initial page estimate.
            int totalPages = SplitRaw(text, maxLength).Length;

            // Iterate until the suffix length (driven by digit count of totalPages)
            // stops changing the resulting page count.
            while (true)
            {
                int digits = totalPages.ToString().Length;
                // Worst case: current page has as many digits as totalPages, e.g. " (12/12)"
                int suffixLength = " (".Length + digits + "/".Length + digits + ")".Length;

                int effectiveMax = maxLength - suffixLength;
                if (effectiveMax <= 0)
                    throw new ArgumentException(
                        $"maxLength ({maxLength}) is too small to fit page suffix.");

                var chunks = SplitRaw(text, effectiveMax);

                if (chunks.Length == totalPages)
                {
                    // Stable — format and return.
                    var result = new string[chunks.Length];
                    for (int i = 0; i < chunks.Length; i++)
                        result[i] = $"{chunks[i]} ({i + 1}/{chunks.Length})";
                    return result;
                }

                // Page count changed (e.g. reserving suffix space pushed it from 9 to 10 pages,
                // which itself changes digit count) — recompute with the new total.
                totalPages = chunks.Length;
            }
        }

        // Word-boundary-aware splitter; falls back to a hard cut for words longer than maxLength.
        private static string[] SplitRaw(string text, int maxLength)
        {
            var chunks = new List<string>();
            int start = 0;

            while (start < text.Length)
            {
                int remaining = text.Length - start;
                if (remaining <= maxLength)
                {
                    chunks.Add(text.Substring(start));
                    break;
                }

                int splitAt = text.LastIndexOf(' ', start + maxLength - 1, maxLength);
                if (splitAt <= start) // no space found — hard cut
                    splitAt = start + maxLength;

                chunks.Add(text.Substring(start, splitAt - start).TrimEnd());
                start = splitAt;
                while (start < text.Length && text[start] == ' ')
                    start++; // skip leading space on next chunk
            }

            return chunks.ToArray();
        }

        public static async Task SendMessage(string message)
        {
            Log.Debug($"Message to parse: {message}");
            #region length checking
            int len = message.Length;
            List<string> parts = new List<string>(new string[] { "If you are seeing this, there was an error parsing the intended message. Please contact the bot maintenance team and provide the log." });
            if (len <= 200)
            {
                parts = new List<string>(new string[] { message });
            }
            else
            {
                parts = SplitText(message, 200).ToList();
            }

            #endregion


            #region message stuff

            const int MaxDelay = 3500;
            const int MinDelay = 1000;
            int WaitBetweenMessagesMs = MinDelay;
            const int MaxRetries = 3;
            int RetriesAtMaxDelay = 0;

            bool breakFlag = false;

            bool firstIteration = true;
            for (int i = 0; i < parts.Count;)
            {
                if (firstIteration)//don't delay the very first message attempt
                {
                    firstIteration = false;
                }
                else
                {
                    await Task.Delay(WaitBetweenMessagesMs);
                }

                var m = new Google.Apis.YouTube.v3.Data.LiveChatMessage()
                {
                    Snippet = new Google.Apis.YouTube.v3.Data.LiveChatMessageSnippet
                    {
                        LiveChatId = Bot.stream.Snippet.LiveChatId,
                        Type = "textMessageEvent",
                        TextMessageDetails = new Google.Apis.YouTube.v3.Data.LiveChatTextMessageDetails
                        {
                            MessageText = parts[i]
                        }
                    }
                };


                #endregion

                var request = Bot.youtube.LiveChatMessages.Insert(m, "snippet");
                Log.Debug("Attempting to send message snippet: {Message}", parts[i]);
                try
                {
                    var response = await request.ExecuteAsync();

                    if (WaitBetweenMessagesMs > MinDelay)
                    {
                        WaitBetweenMessagesMs -= 500;
                    }

                    i++;
                    WaitBetweenMessagesMs = MinDelay;
                    RetriesAtMaxDelay = 0;
                }
                catch (Google.GoogleApiException ex)
                {
                    Log.Error("HTTP error! Info: HTTP {Status}: {Message}", ex.HttpStatusCode, ex.Message);
                    Log.Debug($"Following is logged as debug to prevent leakage into production console");

                    foreach (var error in ex.Error?.Errors ?? [])
                    {
                        Log.Debug(
                            "Reason: {Reason}, Message: {Message}",
                            error.Reason,
                            error.Message);
                        if (error.Reason == "liveChatNotFound")
                        {
                            breakFlag = true;
                            break;
                        }
                    }
                    if (RetriesAtMaxDelay > MaxRetries)
                    {
                        Log.Error("Failed to send a message! Info will dump to log file as debug.");

                        string partDump = string.Join("\n\t\t", parts.Select((p) => $"\"{p}\""));
                        int longestPart = parts.Count > 0 ? parts.Max(p => p.Length) : 0;

                        Log.Debug("Info:\n\tfull message: \"{message}\"\n\tParts: {parts}\n\tLongest: {longestLength}", message, partDump, longestPart);
                        break;
                    }
                    if (WaitBetweenMessagesMs < MaxDelay)
                    {
                        WaitBetweenMessagesMs += 500;
                    }
                    else
                    {
                        RetriesAtMaxDelay++;
                    }
                }
                if (breakFlag)
                {
                    break;
                }   
            }
        }
    }
}
