using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TextCraft.src.Core.Config
{
    public class ConfigMgr:BaseSingleton<ConfigMgr>
    {
        public GameConfig gameConfig = new GameConfig();
        public GraphicConfig graphicConfig = new GraphicConfig();

        public void Save(string path)
        {
            var xs = new XmlSerializer(typeof(ConfigMgr));
            using var fs = new FileStream(path, FileMode.Create);
            xs.Serialize(fs, ConfigMgr.Ins);
        }
        public void Load(string path)
        {
            var xs = new XmlSerializer(typeof(ConfigMgr));
            using var fs = new FileStream(path, FileMode.Open);
            var loader = (ConfigMgr?)xs.Deserialize(fs);
            // 关键：将数据注入单例（清空原有，添加新数据）
            if(loader != null) {
                gameConfig = loader.gameConfig;
                graphicConfig = loader.graphicConfig;
            }
        }

        public void OnLoad(string path)
        {
            try
            {
                Load(path);
            }
            catch
            {
                Save(path);
            }
        }
    }


    [XmlInclude(typeof(GameConfig))]
    [XmlInclude(typeof(GraphicConfig))]
    public class BaseConfig 
    { 
    
    }


    public class GameConfig : BaseConfig
    {
        public int SeaLevel { get;set; } = 0;
        public int ChunkSizeX { get; set; } = 32;
        public int ChunkSizeY { get; set; } = 32;
        public int ChunkSizeZ { get; set; } = 32;
        public int PoolMaxCapacity { get;set; } = 3000;
    }

    public class GraphicConfig: BaseConfig
    {
        public int ViewRange { get; set; } = 192;
        public bool fog = true;
    }
}
