using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace TextCraft.src.Core.Event
{
    internal class EventMgr:BaseSingleton<EventMgr>
    {
        ConcurrentDictionary<Type,Action<EventArg>> events = new();

        public void Publish<T>(T args) where T : EventArg
        {
            if (events.ContainsKey(typeof(T)))
            {
                events[typeof(T)].Invoke(args);
            }
            else
                Console.WriteLine("执行失败");
        }

        public void Subscribe(Type type,Action<EventArg> action)
        {
            if (!events.ContainsKey(type))
            {
                events[type] = new Action<EventArg>(action);
            }
            else
                events[type] += action;
        }

        public void Unsubscribe(Type type, Action<EventArg> action)
        {
        }
    }   
}
