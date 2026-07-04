using OpenTK.Compute.OpenCL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.Event;
using static System.Collections.Specialized.BitVector32;

namespace TextCraft.src.Core.ConsoleModule
{
    internal class ConsoleMgr
    {
        ConcurrentQueue<string> _commandQueue = new();
        public void Load()
        {
            Console.WriteLine("输入 /LoadWorld [name] [seed]");
            Console.WriteLine("  或 /LoadWorld [name]        加载世界");
            Console.WriteLine("[seed]参数对于已有存档的世界不会起作用");
            Console.WriteLine("输入 /UnLoadWorld 卸载世界");

            Task.Run(async () => {
                while (true)
                {
                    string command = await Console.In.ReadLineAsync() ?? "";
                    if (command != null)
                    {
                        _commandQueue.Enqueue(command);
                    }
                }
            });
        }
        public void Update()
        {
            if (_commandQueue.TryDequeue(out var command))
            {
                string[] s = command.Split();
                if (s.Length >= 2 && s[0] == "/LoadWorld")
                {
                    int seed = 0;
                    if (s.Length == 3)
                        seed = int.Parse(s[2]);

                    EventMgr.Ins.Publish(new LoadWorldEventArg() { name = s[1],seed = seed });
                    
                    //this.WindowState = WindowState.Fullscreen;
                }
                else if (s.Length == 1 && s[0] == "/UnLoadWorld")
                {
                    EventMgr.Ins.Publish(new UnLoadWorldEventArg() {});
                }
            }
        }
    }
}
