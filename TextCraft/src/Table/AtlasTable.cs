using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;
using TextCraft.src.Rendering;

namespace TextCraft.src.Table
{
    public class AtlasTable : BaseSingleton<AtlasTable>
    {
        public Atlas uiAtlas = new Atlas();

        public AtlasTable()
        {
            uiAtlas.LoadAtlas(AppContext.BaseDirectory + "Resources//atlas//uiAtlas");
        }
    }
}
