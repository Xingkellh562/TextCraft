using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Rendering;

namespace TextCraft.src.Core
{
    internal class GameSession
    {
        public World? World {  get; private set; }
        public IRenderer? GameRender { get; private set; }

        private bool _isLoad = false;

        public bool IsLoad => _isLoad;
        public void LoadWorld(string name,int seed)
        {
            World = new World(name,seed);

            World.Load();
            GameRender = new GameRenderer(World);

            GameRender.Load();

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
            if(_isLoad)
                World?.Update(updateTime);
        }
    }
}
