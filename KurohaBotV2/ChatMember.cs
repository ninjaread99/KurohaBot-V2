using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KurohaBotV2
{
    internal class ChatMember
    {
        public string name { get; private set; }
        public string id { get; private set; }

        public ChatMember(string name, string id)
        {
            this.name = name;
            this.id = id;
        }

        public override string ToString()
        {
            return $"{name} ({id})";
        }

        public override bool Equals(object? obj) => Equals(obj as ChatMember);

        private bool Equals(ChatMember? other)
        {
            if (other is null) return false;
            return this.id == other.id;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }
}
