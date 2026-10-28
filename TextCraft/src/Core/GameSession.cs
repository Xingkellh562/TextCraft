using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Rendering;
using static System.Collections.Specialized.BitVector32;

namespace TextCraft.src.Core
{
    internal class GameSession
    {
        public World? World {  get; private set; }
        public IRenderer? GameRender { get; private set; }

        private bool _isLoad = false;

        public bool IsLoad => _isLoad;

        /// <summary>暂停时跳过世界逻辑更新（渲染与 UI 仍然继续）。</summary>
        public bool Paused { get; set; }
        public void LoadWorld(string name,int seed)
        {
            World = new World(name,seed);

            World.Load();
            if(GameRender == null)
            {
                GameRender = new GameRenderer(World);
                GameRender.Load();
            }
            else if(GameRender is GameRenderer gameRenderer)
            {
                gameRenderer.ResetWorld(World);
            }
            
            _isLoad = true;
        }

        public void UnLoadWorld()
        {
            _isLoad = false;

            World?.UnLoad();
            World = null;
        }
        public void Render()
        {
            if (_isLoad && World != null)
            {
                GameRender?.GetCamera(World.playerPos,World.playerDir);
                GameRender?.Draw();
            }
        }
        public void Update(float updateTime)
        {
            if(_isLoad && !Paused)
                World?.Update(updateTime);
        }
    }
}
