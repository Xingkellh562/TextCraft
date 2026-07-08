using OpenTK.Mathematics;
using SixLabors.ImageSharp.Drawing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using TextCraft.src.Core;
using TextCraft.src.Core.Config;
using TextCraft.src.Tools;

namespace TextCraft.src.Table
{

    public enum BlockType
    {
        Air,
        Solid,
        Liquid,
        IncompleteBlock
    }

    public class BlockTable : BaseSingleton<BlockTable>
    {
        ConcurrentDictionary<int, Block> blocks = new ConcurrentDictionary<int, Block>();
        List<BlockConfig> blockConfigs = new();

        public Block this[int id]
        {
            get
            {
                if (blocks.ContainsKey(id))
                    return blocks[id];
                else if (blocks.ContainsKey(id - id % 16))
                    return blocks[id - id % 16];
                else
                    return blocks[0];
            }
        }

        public BlockTable()
        {
            //AddBlock(0, 0, "air", BlockType.Air, new QuadFace[] { });

            //AddBlock(1, 0, "stone", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 1 }, new int[] { 1 }));

            //AddBlock(2, 0, "grass", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 3, 3, 2, 1, 3, 3 }, new int[] { 0 }));

            //AddBlock(3, 0, "sand", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 2 }, new int[] { 1 }));

            //AddBlock(4, 0, "dirt", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 1 }, new int[] { 0 }));

            //AddBlock(5, 0, "wood", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 4 }, new int[] { 0 }));

            //AddBlock(6, 0, "log", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 5, 5, 6, 6, 5, 5 }, new int[] { 0 }));
            //AddBlock(6, 1, "log", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 6, 6, 7, 7, 7, 7 }, new int[] { 0 }));
            //AddBlock(6, 2, "log", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 7, 7, 5, 5, 6, 6 }, new int[] { 0 }));

            //AddBlock(7, 0, "roundstone", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 0 }, new int[] { 1 }));

            //AddBlock(8, 0, "glass", BlockType.IncompleteBlock, ModelCreator.CreateModel(ModelTable.cube, new int[] { 0 }, new int[] { 2 }), true);

            //AddBlock(9, 0, "water", BlockType.Liquid, ModelCreator.CreateModel(ModelTable.cube15pxH, new int[] { 1, 1, 0, 1, 1, 1 }, new int[] { 3 }), true);
            //AddBlock(10, 0, "leave", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 1 }, new int[] { 2 }));

            Load(System.IO.Path.Combine(AppContext.BaseDirectory + "Config\\Tables\\blockTable.xml"));
        }

        public void AddBlock(int id, int type, string name, BlockType blockType, QuadFace[] model, bool isLucency = false)
        {
            blocks[id * 16 + type] = new Block(id, name, blockType, model, isLucency) { Type = type };
            BlockConfig block = new BlockConfig() { id = id, type = type, name = name,
                blockType = blockType, BaseModel = "cube" ,
                materialIndexU = new int[] { 0}, materialIndexV = new int[] { 0 },
                isLucency = isLucency};
            blockConfigs.Add(block);
        }

        public void AddBlock(BlockConfig block)
        {
            blocks[block.id * 16 + block.type] = new Block(block.id, block.name, block.blockType,
                ModelCreator.CreateModel(ModelTable.Ins[block.BaseModel],block.materialIndexU, block.materialIndexV),
                block.isLucency);
        }

        public void Save(string path)
        { 
            var xs = new XmlSerializer(typeof(List<BlockConfig>));
            using var fs = new FileStream(path, FileMode.Create);
            xs.Serialize(fs, blockConfigs);
        }

        public void Load(string path)
        {
            var xs = new XmlSerializer(typeof(List<BlockConfig>));
            using var fs = new FileStream(path, FileMode.Open);
            var blockList = (List<BlockConfig>?)xs.Deserialize(fs) ?? new List<BlockConfig>();
            foreach (var block in blockList)
            {
                AddBlock(block);
            }
        }
    }
    public class Block 
    {

        public int Id { get; set; } = 0;

        public int Type { get; set; } = 0;
        public string Name { get; set; } = "Air";
        public BlockType BlockType { get; set; } = BlockType.Air;

        public QuadFace[] Model { get; set; } = new QuadFace[0];

        public Vector3 damp = new Vector3(0.1f, 0.02f, 0.1f);
        public bool IsLucency { get; set; } = false;
        public Block()
        {

        }

        public Block(int id, string name, BlockType blockType, QuadFace[] model,bool isLucency = false)
        {
            Id = id;
            Name = name;
            BlockType = blockType;
            Model = model;
            IsLucency=isLucency;
        }
    }

    public class BlockConfig 
    {
        public int id;
        public int type;
        public string name = "";
        public BlockType blockType;
        public string BaseModel = "cube";
        public int[] materialIndexU = {0 };
        public int[] materialIndexV = {0 };
        public bool isLucency;
    }

}
