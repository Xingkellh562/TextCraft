using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core
{
    public abstract class BaseSingleton<T> where T : new()
    {
        private static readonly Lazy<T> _lazy = new Lazy<T>(() => new T());
        public static T Ins => _lazy.Value;
    }
}
