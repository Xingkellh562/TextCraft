using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace TextCraft.src.Core.Event
{
    public class EventArg
    {
    }

    public class LoadWorldEventArg:EventArg
    {
        public string name = "";
        public int seed;
    }

    public class UnLoadWorldEventArg : EventArg
    {
    }

    public class PlayerTpEventArg : EventArg
    {
        public Vector3d pos;
    }

    public class SpawnModelEventArg : EventArg
    {
        public string name = "";
    }

    public class TeatureBulidEventArg : EventArg
    {}

    public class WorldLoadingFinishEventArg : EventArg
    {}

    /// <summary>请求退出游戏（主菜单的"退出游戏"按钮发出）。</summary>
    public class RequestExitEventArg : EventArg
    {}

    /// <summary>切换垂直同步（设置窗口的开关发出）。</summary>
    public class ToggleVSyncEventArg : EventArg
    {
        public bool enabled;
    }

    /// <summary>UI 快捷栏点击后，同步游戏内"当前方块"（Program 订阅）。</summary>
    public class SelectBlockEventArg : EventArg
    {
        public int blockId;
    }

    /// <summary>继续游戏（暂停面板的"继续游戏"按钮 / ESC 发出）。</summary>
    public class ResumeGameEventArg : EventArg
    {
    }

    /// <summary>保存当前世界并退回主菜单（暂停面板发出）。</summary>
    public class SaveAndQuitWorldEventArg : EventArg
    {
    }
}