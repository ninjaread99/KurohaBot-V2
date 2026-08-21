using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace KurohaBotV2
{
    internal class ChatQueue
    {
        [JsonInclude]
        List<ChatMember> queue = new List<ChatMember>();
        [JsonInclude]
        HashSet<ChatMember> stale = new HashSet<ChatMember>();
        [JsonInclude]
        bool CheckStale = true;

        public ChatQueue() { }
        [JsonInclude]
        public bool QueueOpen = true;

        public void Join(ChatMember chatter)
        {
            if (CheckStale && stale.Contains(chatter))
            {
                //sender is on the stale list, and can not join again.
                Log.Debug("User {Name} ({Id} on stale list.", chatter.name, chatter.id);
                return;
            }
            if (!QueueOpen)
            {
                return;
            }

            string name = chatter.name;
            string Id = chatter.id;

            if (queue.Any(x => x.id == Id))
            {
                Log.Debug("User {Name} ({Id}) is already in the queue", name, Id);
                return;
            }
            Log.Information("User {Name} ({Id}) joined the queue", name, Id);
            queue.Add(new ChatMember(name, Id));
            Bot.state.Save();
        }

        public void Leave(ChatMember chatter)
        {
            string Id = chatter.id;
            if (!queue.Contains(chatter))
            {
                return;
            }
            queue.RemoveAll(x => x.id == Id);
            Log.Information("User {Name} ({Id}) Left the queue", chatter.name, chatter.id);
            Bot.state.Save();
        }

        public ChatMember? GetNext()
        {
            if (queue.Count == 0)
            {
                return null;
            }
            ChatMember next = queue[0];
            stale.Add(next);
            queue.RemoveAt(0);
            Bot.state.Save();
            return next;
        }

        public List<ChatMember> GetQueue()
        {
            return new List<ChatMember>(queue);
        }

        public void ClearStale()
        {
            stale.Clear();
            Bot.state.Save();
        }

        public void RemoveIdFromStale(ChatMember chatter)
        {
            stale.Remove(chatter);
            Bot.state.Save();
        }

        public void DisableStaleCheck()
        {
            CheckStale = false;
        }
        
        public void EnableStaleCheck()
        {
            CheckStale = true;
        }

        public void ClearQueue()
        {
            queue.Clear();
            Bot.state.Save();
        }
    }
}
