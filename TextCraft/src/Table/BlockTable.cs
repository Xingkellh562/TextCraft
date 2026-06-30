using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;
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

    internal class BlockTable:BaseSingleton<BlockTable>
    {
        ConcurrentDictionary<int,Block> blocks = new ConcurrentDictionary<int,Block>();

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
            AddBlock(0, 0,"air", BlockType.Air, new QuadFace[] { });
            
            AddBlock(1, 0, "stone", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 1 }, new int[] { 1 }));

            AddBlock(2, 0, "grass", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 3,3,2,1,3,3 }, new int[] { 0 }));

            AddBlock(3, 0, "sand", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 2 }, new int[] { 1 }));

            AddBlock(4, 0, "dirt", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 1 }, new int[] { 0 }));

            AddBlock(5, 0, "wood", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 4 }, new int[] { 0 }));

            AddBlock(6, 0, "log", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 5, 5, 6, 6, 5, 5 }, new int[] { 0 }));
            AddBlock(6, 1, "log", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 6, 6, 7, 7, 7, 7 }, new int[] { 0 }));
            AddBlock(6, 2, "log", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 7, 7, 5, 5, 6, 6 }, new int[] { 0 }));

            AddBlock(7, 0, "roundstone", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 0}, new int[] { 1 }));

            AddBlock(8, 0, "glass", BlockType.IncompleteBlock, ModelCreator.CreateModel(ModelTable.cube, new int[] { 0 }, new int[] { 2 }), true);

            AddBlock(9, 0, "water", BlockType.Liquid, ModelCreator.CreateModel(ModelTable.cube15pxH, new int[] { 1,1,0,1,1,1 }, new int[] { 3 }),true);
            AddBlock(10, 0, "leave", BlockType.Solid, ModelCreator.CreateModel(ModelTable.cube, new int[] { 1 }, new int[] { 2 }));
        }

        public void AddBlock(int id,int type,string name, BlockType blockType, QuadFace[] model,bool isLucency = false)
        {
            blocks[id * 16 + type] = new Block(id, name, blockType,model,isLucency);
        }
    }

    internal class Block
    {
        int _id;
        string _name;
        BlockType _blockType;
        QuadFace[] _model;
        bool _isLucency = false;

        public int Id => _id;
        public string Name => _name;
        public BlockType BlockType => _blockType;

        public QuadFace[] Model => _model;

        public bool IsLucency => _isLucency;

        public Block(int id, string name, BlockType blockType, QuadFace[] model,bool isLucency = false)
        {
            _id = id;
            _name = name;
            _blockType = blockType;
            _model = model;
            _isLucency=isLucency;
        }
    }
}
