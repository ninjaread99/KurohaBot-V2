using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KurohaBotV2
{
    internal class ChatQueue
    {
        List<ChatMember> queue = new List<ChatMember>();

        public ChatQueue() { }

        public bool QueueOpen = true;

        public void Join(string name, string Id)
        {
            if (queue.Any(x => x.id == Id))
            {
                Log.Debug("User {Name} ({Id}) is already in the queue", name, Id);
                return;
            }
            Log.Debug("User {Name} ({Id}) joined the queue", name, Id);
            queue.Add(new ChatMember(name, Id));
        }

        public void Leave(string Id)
        {
            queue.RemoveAll(x => x.id == Id);
        }

        public ChatMember? GetNext()
        {
            if (queue.Count == 0)
            {
                return null;
            }
            ChatMember next = queue[0];
            queue.RemoveAt(0);
            return next;
        }

        public List<ChatMember> GetQueue()
        {
            return new List<ChatMember>(queue);
        }

        public void ClearQueue()
        {
            queue.Clear();
        }
    }
}
