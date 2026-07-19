using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.UI;
using static System.Collections.Specialized.BitVector32;
using OpenTK.Graphics.OpenGL4;

namespace TextCraft.src.Core
{
    public enum GameState { MainMenu, InGame }
    internal class GameStateMgr
    {
        public GameState _state = GameState.MainMenu;

        Game game;
        public GameStateMgr(Game game) 
        {
            this.game = game;
        }

        public void SwitchState(GameState state)
        {
            if (_state == GameState.InGame && game.session.IsLoad)
            {
                game.session.UnLoadWorld();
                Console.WriteLine("正在卸载世界中");
            }

            _state = state;

            if (state == GameState.MainMenu)
            {
                GL.ClearColor(0, 0, 0, 0);
                game.uIMgr.uITable["gamePanel"].Sleep();
                game.uIMgr.uITable["mainMenuPanel"].Wake();
            }
            else
            {
                //game.uIMgr.uITable["gamePanel"].Wake();
                game.uIMgr.uITable["loadingPanel"].Wake();
                game.uIMgr.uITable["mainMenuPanel"].Sleep();
            }
        }
    }
}
