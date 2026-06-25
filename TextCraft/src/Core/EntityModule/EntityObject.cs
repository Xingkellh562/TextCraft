using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core.EntityModule
{
    public enum EntityType
    {
        living,
    }
    internal struct EntityObject
    {
        public string name = "";
        public EntityType type;

        public EntityObject(string name,EntityType type)
        {
            this.name = name;
            this.type = type;
        }
    }
}
