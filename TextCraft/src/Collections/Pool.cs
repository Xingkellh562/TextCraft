using System;
using System.Collections.Concurrent;
using System.Threading;

namespace TextCraft.src.Collections
{
    /// <summary>
    /// 线程安全的对象池，支持容量限制、对象创建和重置。
    /// </summary>
    /// <typeparam name="T">池中对象的类型</typeparam>
    internal class Pool<T> : IDisposable
    {
        private readonly ConcurrentStack<T> _stack = new ConcurrentStack<T>();
        private readonly int _maxCapacity;
        private int _count;                         // 当前池中对象数量（原子操作）
        private readonly Func<T> _factory;          // 对象工厂（可选）
        private readonly Action<T> _resetAction;    // 对象重置回调（可选）
        private bool _disposed;

        /// <summary>
        /// 当前池中的对象数量
        /// </summary>
        public int Count => Interlocked.CompareExchange(ref _count, 0, 0);

        /// <summary>
        /// 最大容量
        /// </summary>
        public int MaxCapacity => _maxCapacity;

        /// <summary>
        /// 仅限制容量的池（不提供自动创建和重置）
        /// </summary>
        /// <param name="capacity">最大容量</param>
        public Pool(int capacity)
            : this(capacity, null, null)
        {
        }

        /// <summary>
        /// 完整初始化的池
        /// </summary>
        /// <param name="capacity">最大容量</param>
        /// <param name="factory">当池为空时创建新对象的工厂方法</param>
        /// <param name="resetAction">对象归还时重置其状态的回调（可为 null）</param>
        public Pool(int capacity, Func<T> factory, Action<T> resetAction = null)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "容量必须大于0");

            _maxCapacity = capacity;
            _factory = factory;
            _resetAction = resetAction;
        }

        /// <summary>
        /// 将对象归还池中（如果池未满则存储，否则丢弃并释放资源）
        /// </summary>
        /// <param name="value">要归还的对象</param>
        public void Enter(T value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Pool<T>));

            // 先重置对象状态（如果提供了重置回调）
            _resetAction?.Invoke(value);

            // 尝试增加计数，原子地判断是否超过容量
            int newCount = Interlocked.Increment(ref _count);
            if (newCount <= _maxCapacity)
            {
                _stack.Push(value);
            }
            else
            {
                // 容量已满，回滚计数并丢弃对象
                Interlocked.Decrement(ref _count);
                TryDispose(value);
            }
        }

        /// <summary>
        /// 从池中取出一个对象（如果池为空则返回 false）
        /// </summary>
        /// <param name="result">取出的对象，失败时为 default(T)</param>
        /// <returns>是否成功取出对象</returns>
        public bool TryRelease(out T result)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Pool<T>));

            if (_stack.TryPop(out result))
            {
                Interlocked.Decrement(ref _count);
                return true;
            }

            result = default(T);
            return false;
        }

        /// <summary>
        /// 从池中获取一个对象（如果池为空且提供了工厂，则通过工厂创建；否则失败）
        /// </summary>
        /// <param name="result">获取的对象</param>
        /// <returns>是否成功获取对象</returns>
        public bool TryTake(out T result)
        {
            if (TryRelease(out result))
                return true;

            if (_factory != null)
            {
                result = _factory();
                return true;
            }

            result = default(T);
            return false;
        }

        /// <summary>
        /// 从池中获取一个对象（如果池为空则通过工厂创建，未提供工厂则抛出异常）
        /// </summary>
        /// <returns>获取的对象</returns>
        /// <exception cref="InvalidOperationException">池为空且未提供工厂</exception>
        public T Take()
        {
            if (TryTake(out T result))
                return result;
            throw new InvalidOperationException("池为空且未提供对象工厂。");
        }

        /// <summary>
        /// 归还对象到池中（<see cref="Enter"/> 的别名）
        /// </summary>
        public void Return(T value) => Enter(value);

        /// <summary>
        /// 清空池中所有对象，并释放实现了 <see cref="IDisposable"/> 的对象
        /// </summary>
        public void Clear()
        {
            while (_stack.TryPop(out T item))
            {
                Interlocked.Decrement(ref _count);
                TryDispose(item);
            }
        }

        /// <summary>
        /// 释放池资源，清空所有对象
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Clear();
        }

        private static void TryDispose(T obj)
        {
            if (obj is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
