using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;

namespace TextCraft.src.Table
{
    internal class TeatureTable:BaseSingleton<TeatureTable>
    {
        Dictionary<string, Teature> _teature = new();

        public Teature this[string name]
        {
            get
            {
                if (_teature.ContainsKey(name))
                    return _teature[name];
                return default(Teature) ?? new Teature() ;
            }
        }
        
        public TeatureTable()
        {

        }

        public void Load(string path)
        {

        }
    }

    class Teature
    {
        public Vector3i Size { get; set; } = Vector3i.One;
        public int[] Voxels = {16 };
        public string Name { get; set; } = "";
    }
}
