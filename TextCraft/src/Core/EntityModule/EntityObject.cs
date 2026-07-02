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
        public string typeName = "";
        public EntityType type;

        public EntityObject(string name, string typeName, EntityType type)
        {
            this.name = name;
            this.type = type;
            this.typeName = typeName;
        }
    }
    public struct Moving
    {
        public float moveSpeed;
        public float runningSpeed;
        public float flyingSpeed;

        public float jumpForce;
    }
}
