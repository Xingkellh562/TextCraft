using OpenTK.Mathematics;
using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Text;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Rendering;
using TextCraft.src.Table;

namespace TextCraft.src.Core.Physic
{
    internal struct RigidBody
    {
        float _mass;
        public float Mass => _mass;
        public Vector3 velocity;
        public Vector3 acceleration;
        public Vector3 damp = new Vector3(0.05f, 0.05f, 0.05f);

        public bool onGround;
        public bool useGravity;

        public RigidBody(float mass)
        {
            if (mass <= 0)
                mass = 0.01f;
            _mass = mass;
        }
    }
}
