using SixLabors.ImageSharp.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TextCraft.src.Rendering.UI;
using TextCraft.src.Table;

namespace TextCraft.src.Rendering
{
    public class Atlas
    {
        public Texture texture = new Texture();

        private Dictionary<string, Spirit> _spirits = new();
        public Dictionary<string, Spirit> Spirits => _spirits;

        public Atlas() 
        {
        }

        public void AddSpirit(string name, int[] rect)
        {
            _spirits.Add(name,new Spirit() {name = name,rect = rect });
        }

        public void LoadAtlas(string path)
        {
            texture.Load(System.IO.Path.Combine(path + "//texture.png"), new Tools.TextureLoader());
            var xs = new XmlSerializer(typeof(List<Spirit>));
            using var fs = new FileStream(System.IO.Path.Combine(path + "//spirits.xml"), FileMode.Open);
            var spiritList = (List<Spirit>?)xs.Deserialize(fs) ?? new List<Spirit>();
            foreach (var spirit in spiritList)
                _spirits.Add(spirit.name,spirit);
        }

        public void SaveAtlas(string path) 
        {
            List<Spirit> spiritList = _spirits.Values.ToList();
            var xs = new XmlSerializer(typeof(List<Spirit>));
            using var fs = new FileStream(System.IO.Path.Combine(path + "//spirits.xml"), FileMode.Create);
            xs.Serialize(fs, spiritList);
        }
        
    }

    public class Spirit
    {
        public string name = "";
        public int[] rect = { };
    }
}
