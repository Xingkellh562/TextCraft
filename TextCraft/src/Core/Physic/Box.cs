using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core.Physic
{
    internal class Box
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

        public List<Vector3i> GetDetermin(Vector3 pos)
        {
            List<Vector3i> result = new List<Vector3i>();
            Vector3 worldPosA = _aPos + pos;
            Vector3 worldPosB = _bPos + pos;

            int ax = (int)MathF.Floor(worldPosA.X);
            int ay = (int)MathF.Floor(worldPosA.Y);
            int az = (int)MathF.Floor(worldPosA.Z);

            int bx = (int)MathF.Floor(worldPosB.X);
            int by = (int)MathF.Floor(worldPosB.Y);
            int bz = (int)MathF.Floor(worldPosB.Z);

            for (int x = ax; x <= bx; x++)
                for (int y = ay; y <= by; y++)
                    for (int z = az; z <= bz; z++)
                        result.Add(new Vector3i(x,y,z));

            return result;

        }
    }
}
