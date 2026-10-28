using System;

namespace TextCraft.src.Table
{
    /// <summary>
    /// 兼容层：旧代码通过 <c>AtlasTable.Ins.uiAtlas</c> 取 UI 图集。
    /// 现在图集由 <see cref="TextCraft.src.UI.UIAssets"/> 按 ui.json 加载，
    /// 这里只做名字转发，保持 Program/调试代码不需要大改。
    /// </summary>
    public class AtlasTable : Core.BaseSingleton<AtlasTable>
    {
        /// <summary>默认 UI 图集（ui.json 中第一张图集，通常名为 "ui"）。</summary>
        public Rendering.Atlas uiAtlas
        {
            get
            {
                var assets = UI.UIAssets.Ins;
                var atlas = assets.GetAtlas("ui");
                if (atlas != null) return atlas;
                return assets.AtlasOrder.Count > 0 ? assets.AtlasOrder[0] : new Rendering.Atlas();
            }
        }

        public Rendering.Atlas? Get(string name) => UI.UIAssets.Ins.GetAtlas(name);
    }
}
