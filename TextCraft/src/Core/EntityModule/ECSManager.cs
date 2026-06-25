using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core.EntityModule
{
    internal class ECSManager
    {
        int _nextId = 0;
        Dictionary<int, Dictionary<Type, object>> _components = new();

        public void AddComponent<T>(int entity,T component) where T: struct
        {
            if (!_components.ContainsKey(entity)) _components[entity] = new();
            _components[entity][typeof(T)] = component;
        }

        public T GetComponent<T>(int entity) => (T)_components[entity][typeof(T)];

        public List<int> GetEntitiesWith(Type[] types)
        {
            var result = new List<int>();
            foreach(var kv in _components)
            {
                int count = 0;
                for (int i = 0; i < types.Length; i++)
                    if (kv.Value.ContainsKey(types[i]))
                        count++;
                if (count == types.Length)
                    result.Add(kv.Key);
            }
            return result;
        }
    }
}
