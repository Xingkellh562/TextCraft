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

        private readonly HashSet<string> _missingSpirits = new();

        public Atlas() 
        {
        }

        /// <summary>
        /// 按名字取精灵。缺失时返回零尺寸矩形并只警告一次，
        /// 避免 UI 因为资源里少了一个精灵就直接抛 KeyNotFoundException 导致启动失败。
        /// </summary>
        public Spirit GetSpirit(string name)
        {
            if (_spirits.TryGetValue(name, out var spirit))
                return spirit;

            if (_missingSpirits.Add(name))
                Console.WriteLine($"[Atlas] 警告: 精灵 '{name}' 不存在，已使用空矩形代替（请检查 spirits.xml 与 UI 配置）。");

            return new Spirit { name = name, rect = new[] { 0, 0, 0, 0 } };
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
