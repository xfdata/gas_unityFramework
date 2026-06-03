using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Framework
{
    public class ObjectPool<T> where T : class, new()
    {
        private readonly ConcurrentBag<T> _items; //线程安全队列
        private readonly int _capacity;
        private T _fastItem;
        private int _count = 0;
#if DEBUG
        private readonly object lockObject = new();
        private readonly Dictionary<T,int> _cache = new();
#endif
        
        public static ObjectPool<T> Default { get; } = new ObjectPool<T>();
        
        public Func<T> CreateAction { get; set; }
        public Action<T> DisposeAction { get; set; }
        public Action<T> ResetAction { get; set; }
        
        public ObjectPool(int capacity = 16)
        {
            _capacity = capacity;
            _items = new ConcurrentBag<T>();
        }

        public T Get()
        {
            var item = _fastItem;
            if (item == null || Interlocked.CompareExchange(ref _fastItem, null, item) != item)
            {
                if (_items.TryTake(out item))
                {
                    Interlocked.Decrement(ref _count);
                }
                else
                {
                    if (CreateAction != null)
                    {
                        item = CreateAction();
                    }
                    else
                    {
                        item = new T();
                    }
                }
            }
#if DEBUG
            lock(lockObject){
                _cache.Remove(item, out int value);
            }
#endif
            
            return item;
        }

        public void Return(T obj)
        {
        #if DEBUG
            lock(lockObject){
                if(!_cache.TryAdd(obj,1)){
                    throw new Exception("ObjectPool Repeat return obj");
                }
            }
        #endif
            if (_fastItem != null || Interlocked.CompareExchange(ref _fastItem, obj, null) != null)
            {
                if (Interlocked.Increment(ref _count) <= _capacity)
                {
                    _items.Add(obj);
                    
                    if (ResetAction != null)
                    {
                        ResetAction(obj);
                    }
                    
                    return;
                }
                Interlocked.Decrement(ref _count);
            }
            else
            {
                // If _fastItem is null and not set, directly call ResetAction
                if (ResetAction != null)
                {
                    ResetAction(obj);
                }
            }
        }

        public int Count => _count;
        
        public void Clear()
        {
        #if DEBUG
            _cache.Clear();
        #endif
            if (DisposeAction != null)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_items.TryTake(out var item))
                    {
                        DisposeAction(item);
                    }
                }
            }
            _items.Clear();
        }
        
        public struct AutoReturnPoolObject : IDisposable
        {
            private ObjectPool<T> _pool;
            private T _obj;

            public AutoReturnPoolObject(ObjectPool<T> pool)
            {
                _pool = pool;
                _obj = _pool.Get();
            }

            public T Obj => _obj;
            
            public void Dispose()
            {
                _pool.Return(_obj);
            }
        }
    }

    public static class ObjectPoolExtendsion
    {
        public static ObjectPool<T>.AutoReturnPoolObject CreateAutoReturnPoolObject<T>(this ObjectPool<T> pool) where T : class, new()
        {
            return new ObjectPool<T>.AutoReturnPoolObject(pool);
        }
    }
}