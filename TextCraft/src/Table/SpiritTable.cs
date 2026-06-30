using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;

namespace TextCraft.src.Table
{
    internal class SpiritTable : BaseSingleton<SpiritTable>
    {
        public SpiritTable() 
        {
            _blockSpirits[0] = new int[] { 0, 0, 16, 16 };
            _blockSpirits[1] = new int[] { 16,16,32,32 };
            _blockSpirits[2] = new int[] { 32, 0, 48, 16 };
            _blockSpirits[3] = new int[] { 32, 16, 48, 32 };
            _blockSpirits[4] = new int[] { 16, 0, 32, 16 };
            _blockSpirits[5] = new int[] { 64, 0, 80, 16 };
            _blockSpirits[6] = new int[] { 80, 0, 96, 16 };
            _blockSpirits[7] = new int[] { 0, 16, 16, 32 };
            _blockSpirits[8] = new int[] { 0, 32, 16, 48 };
            _blockSpirits[9] = new int[] { 0, 48, 16, 64 };
            _blockSpirits[10] = new int[] { 16, 32, 32, 48 };
        }

        private Dictionary<int, int[]> _blockSpirits = new();

        public Dictionary<int, int[]> BlockSpirits => _blockSpirits;

        public int[] none = { 0, 256, 1, 257 };
        public int[] sight = {0,256,16, 272};
        public int[] title = { 16, 256, 160, 272 };
        public int[] displayTable = {0,272,24,296 };
    }
}
