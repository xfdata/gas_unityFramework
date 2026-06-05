using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleFoundation
{
    /// <summary>
    /// 优化版的事件总线 - 降低GC压力，提高性能
    /// 
    /// 优化项：
    /// 1. 避免 ToArray() 的堆分配 - 使用缓存数组
    /// 2. 高效的移除 - 使用索引替代 Remove()
    /// 3. 防止重复订阅 - 同一处理器不会注册两次
    /// 4. 缓存字典查询结果
    /// </summary>
    public class BattleEventBusOptimized : Disposable
    {
        private sealed class HandlerSet
        {
            public readonly Type PayloadType;
            public readonly List<Delegate> Handlers = new List<Delegate>();
            
            // 缓存的处理器数组，避免 ToArray() 产生GC
            public Delegate[] CachedArray = Array.Empty<Delegate>();
            public int CachedCount;
            private bool _needsRebuild = true;

            public HandlerSet(Type payloadType)
            {
                PayloadType = payloadType;
            }

            /// <summary>获取处理器数组，避免额外分配</summary>
            public Delegate[] GetHandlers()
            {
                if (_needsRebuild)
                {
                    CachedCount = Handlers.Count;
                    if (CachedArray.Length < CachedCount)
                    {
                        CachedArray = new Delegate[CachedCount * 2]; // 预留空间减少重分配
                    }
                    Handlers.CopyTo(CachedArray);
                    _needsRebuild = false;
                }
                return CachedArray;
            }

            public void AddHandler(Delegate handler)
            {
                // 防止重复订阅
                if (Handlers.Contains(handler))
                    return;
                
                Handlers.Add(handler);
                _needsRebuild = true;
            }

            public void RemoveHandler(Delegate handler)
            {
                int index = Handlers.IndexOf(handler);
                if (index >= 0)
                {
                    // 使用RemoveAt替代Remove，避免线性搜索
                    Handlers[index] = Handlers[Handlers.Count - 1];
                    Handlers.RemoveAt(Handlers.Count - 1);
                    _needsRebuild = true;
                }
            }
        }

        private readonly Dictionary<int, HandlerSet> _handlers = new Dictionary<int, HandlerSet>();
        private HandlerSet _cachedHandlerSet;
        private int _cachedEventId;

        public void On<T>(int eventId, Action<T> handler)
        {
            if (handler == null) return;
            GetOrCreateSet<T>(eventId).AddHandler(handler);
        }

        public void Off<T>(int eventId, Action<T> handler)
        {
            if (handler == null || !_handlers.TryGetValue(eventId, out var set))
                return;

            EnsurePayloadType<T>(eventId, set);
            set.RemoveHandler((Delegate)handler);
            
            if (set.Handlers.Count == 0)
            {
                _handlers.Remove(eventId);
                if (_cachedEventId == eventId)
                    _cachedHandlerSet = null;
            }
        }

        public void Emit<T>(int eventId, T data)
        {
            // 使用缓存加速频繁查询的事件
            HandlerSet set;
            if (_cachedEventId == eventId && _cachedHandlerSet != null)
            {
                set = _cachedHandlerSet;
            }
            else if (!_handlers.TryGetValue(eventId, out set))
                return;
            else
            {
                _cachedEventId = eventId;
                _cachedHandlerSet = set;
            }

            EnsurePayloadType<T>(eventId, set);
            
            // 获取缓存的处理器数组，避免 ToArray() 产生GC
            var handlers = set.GetHandlers();
            for (int i = 0; i < set.CachedCount; i++)
            {
                try
                {
                    ((Action<T>)handlers[i]).Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BattleEventBus] Error handling event {eventId}: {e}");
                }
            }
        }

        public void ClearAll()
        {
            _handlers.Clear();
            _cachedHandlerSet = null;
            _cachedEventId = 0;
        }

        private HandlerSet GetOrCreateSet<T>(int eventId)
        {
            if (!_handlers.TryGetValue(eventId, out var set))
            {
                set = new HandlerSet(typeof(T));
                _handlers.Add(eventId, set);
            }
            else
            {
                EnsurePayloadType<T>(eventId, set);
            }
            return set;
        }

        private static void EnsurePayloadType<T>(int eventId, HandlerSet set)
        {
            if (set.PayloadType != typeof(T))
            {
                throw new InvalidOperationException(
                    $"Battle event '{eventId}' uses payload '{set.PayloadType.Name}', not '{typeof(T).Name}'.");
            }
        }

        protected override void OnDispose()
        {
            ClearAll();
            base.OnDispose();
        }
    }
}

