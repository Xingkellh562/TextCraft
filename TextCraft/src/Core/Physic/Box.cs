using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core.Physic
{
    internal struct Box
    {
        Vector3 _aPos;
        Vector3 _bPos;

        public Vector3 MinPos => _aPos;
        public Vector3 MaxPos => _bPos;

        public Box(Vector3 aPos, Vector3 bPos)
        {
            _aPos = aPos;
            _bPos = bPos;
        }
    }
}
