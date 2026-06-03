using System;
using System.Collections.Generic;

namespace GAS
{
    public interface IGameplayAttributeSetProvider
    {
        AttributeSet AttributeSet { get; }
    }

    public class AttributeSet : IGameplayAttributeOwner
    {
        private const float ChangeEpsilon = 0.0001f;

        private readonly Dictionary<int, float> baseValues = new Dictionary<int, float>();
        private readonly Dictionary<int, List<AttributeModifierEntry>> modifiers =
            new Dictionary<int, List<AttributeModifierEntry>>();
        private readonly Dictionary<AttributeModifierHandle, int> modifierAttributeIds =
            new Dictionary<AttributeModifierHandle, int>();
        private readonly Dictionary<int, float> cachedValues = new Dictionary<int, float>();
        private readonly HashSet<int> dirtyAttributes = new HashSet<int>();

        private int nextModifierId = 1;

        public event Action<int, float, float> OnAttributeBaseValueChanged;

        public event Action<int, float, float> OnAttributeChanged;

        public IReadOnlyDictionary<int, float> BaseValues => baseValues;

        public float GetAttribute(int attributeId)
        {
            if (!dirtyAttributes.Contains(attributeId) &&
                cachedValues.TryGetValue(attributeId, out var cachedValue))
            {
                return cachedValue;
            }

            var value = CalculateCurrentValue(attributeId);
            cachedValues[attributeId] = value;
            dirtyAttributes.Remove(attributeId);
            return value;
        }

        public float GetBaseValue(int attributeId)
        {
            return baseValues.TryGetValue(attributeId, out var value) ? value : 0f;
        }

        public bool HasBaseValue(int attributeId)
        {
            return baseValues.ContainsKey(attributeId);
        }

        public bool HasModifier(AttributeModifierHandle handle)
        {
            return handle.IsValid && modifierAttributeIds.ContainsKey(handle);
        }

        public bool TryGetModifierAttribute(
            AttributeModifierHandle handle,
            out int attributeId)
        {
            return modifierAttributeIds.TryGetValue(handle, out attributeId);
        }

        public int GetModifierCount(int attributeId)
        {
            return modifiers.TryGetValue(attributeId, out var list) ? list.Count : 0;
        }

        public void SetBaseValue(int attributeId, float value)
        {
            SetBaseValue(attributeId, value, true);
        }

        public void SetBaseValues(IEnumerable<AttributeValueState> values, bool notifyChanges = true)
        {
            if (values == null)
                return;

            foreach (var value in values)
            {
                SetBaseValue(value.AttributeId, value.Value, notifyChanges);
            }
        }

        public void SetBaseValue(int attributeId, float value, bool notifyChanges)
        {
            var oldBaseValue = GetBaseValue(attributeId);
            var oldCurrentValue = GetAttribute(attributeId);
            var newBaseValue = PreAttributeBaseChange(attributeId, value);

            baseValues[attributeId] = newBaseValue;
            MarkDirty(attributeId);

            var newCurrentValue = GetAttribute(attributeId);
            PostAttributeBaseChange(attributeId, oldBaseValue, newBaseValue);

            if (notifyChanges)
            {
                NotifyAttributeBaseValueChanged(attributeId, oldBaseValue, newBaseValue);
                NotifyAttributeChanged(attributeId, oldCurrentValue, newCurrentValue);
            }
        }

        public void AddAttributeBaseValue(int attributeId, float delta)
        {
            SetBaseValue(attributeId, GetBaseValue(attributeId) + delta);
        }

        public AttributeModifierHandle AddModifier(
            int attributeId,
            AttributeModifierOp op,
            float value,
            object source)
        {
            var oldValue = GetAttribute(attributeId);
            var handle = new AttributeModifierHandle(nextModifierId++);

            if (!modifiers.TryGetValue(attributeId, out var list))
            {
                list = new List<AttributeModifierEntry>();
                modifiers[attributeId] = list;
            }

            list.Add(new AttributeModifierEntry
            {
                AttributeId = attributeId,
                Handle = handle,
                Op = op,
                Value = value,
                Source = source,
            });
            modifierAttributeIds[handle] = attributeId;

            MarkDirty(attributeId);
            NotifyAttributeChanged(attributeId, oldValue, GetAttribute(attributeId));
            return handle;
        }

        public void RemoveModifier(AttributeModifierHandle handle)
        {
            if (!handle.IsValid)
                return;

            if (!modifierAttributeIds.TryGetValue(handle, out var attributeId))
                return;

            if (!modifiers.TryGetValue(attributeId, out var modifiersForAttribute))
                return;

            var oldValue = GetAttribute(attributeId);

            for (int i = modifiersForAttribute.Count - 1; i >= 0; i--)
            {
                if (modifiersForAttribute[i].Handle != handle)
                    continue;

                modifiersForAttribute.RemoveAt(i);
                modifierAttributeIds.Remove(handle);

                if (modifiersForAttribute.Count == 0)
                {
                    modifiers.Remove(attributeId);
                }

                MarkDirty(attributeId);
                NotifyAttributeChanged(attributeId, oldValue, GetAttribute(attributeId));
                return;
            }

            modifierAttributeIds.Remove(handle);
        }

        public int RemoveModifiersBySource(object source)
        {
            if (source == null)
                return 0;

            var removedCount = 0;
            var attributeIds = ListPool<int>.Get();

            foreach (var kv in modifiers)
            {
                attributeIds.Add(kv.Key);
            }

            for (int i = 0; i < attributeIds.Count; i++)
            {
                removedCount += RemoveModifiersBySource(attributeIds[i], source);
            }

            ListPool<int>.Release(attributeIds);
            return removedCount;
        }

        public int RemoveModifiersBySource(int attributeId, object source)
        {
            if (source == null || !modifiers.TryGetValue(attributeId, out var list))
                return 0;

            var oldValue = GetAttribute(attributeId);
            var removedCount = 0;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!Equals(list[i].Source, source))
                    continue;

                modifierAttributeIds.Remove(list[i].Handle);
                list.RemoveAt(i);
                removedCount++;
            }

            if (removedCount == 0)
                return 0;

            if (list.Count == 0)
            {
                modifiers.Remove(attributeId);
            }

            MarkDirty(attributeId);
            NotifyAttributeChanged(attributeId, oldValue, GetAttribute(attributeId));
            return removedCount;
        }

        public void ClearAllModifiers()
        {
            if (modifiers.Count == 0)
                return;

            var changedAttributes = ListPool<int>.Get();
            foreach (var kv in modifiers)
            {
                changedAttributes.Add(kv.Key);
            }

            var oldValues = ListPool<float>.Get();
            for (int i = 0; i < changedAttributes.Count; i++)
            {
                oldValues.Add(GetAttribute(changedAttributes[i]));
            }

            modifiers.Clear();
            modifierAttributeIds.Clear();

            for (int i = 0; i < changedAttributes.Count; i++)
            {
                var attributeId = changedAttributes[i];
                MarkDirty(attributeId);
                NotifyAttributeChanged(attributeId, oldValues[i], GetAttribute(attributeId));
            }

            ListPool<float>.Release(oldValues);
            ListPool<int>.Release(changedAttributes);
        }

        public void Clear()
        {
            baseValues.Clear();
            modifiers.Clear();
            modifierAttributeIds.Clear();
            cachedValues.Clear();
            dirtyAttributes.Clear();
            nextModifierId = 1;
        }

        public AttributeSetState CaptureState(bool includeModifiers = false)
        {
            var baseValueStates = new AttributeValueState[baseValues.Count];
            var index = 0;
            foreach (var kv in baseValues)
            {
                baseValueStates[index++] = new AttributeValueState
                {
                    AttributeId = kv.Key,
                    Value = kv.Value,
                };
            }

            return new AttributeSetState
            {
                BaseValues = baseValueStates,
                Modifiers = includeModifiers ? CaptureModifierStates() : Array.Empty<AttributeModifierState>(),
                NextModifierId = nextModifierId,
            };
        }

        public void RestoreState(AttributeSetState state, bool notifyChanges = true)
        {
            Dictionary<int, float> oldBaseValues = null;
            Dictionary<int, float> oldCurrentValues = null;

            if (notifyChanges)
            {
                oldBaseValues = new Dictionary<int, float>();
                oldCurrentValues = new Dictionary<int, float>();
                CaptureBaseValues(oldBaseValues);
                CaptureCurrentValues(oldCurrentValues);
            }

            baseValues.Clear();
            modifiers.Clear();
            modifierAttributeIds.Clear();
            cachedValues.Clear();
            dirtyAttributes.Clear();

            if (state.BaseValues != null)
            {
                for (int i = 0; i < state.BaseValues.Length; i++)
                {
                    var baseValue = state.BaseValues[i];
                    baseValues[baseValue.AttributeId] = baseValue.Value;
                    MarkDirty(baseValue.AttributeId);
                }
            }

            if (state.Modifiers != null)
            {
                for (int i = 0; i < state.Modifiers.Length; i++)
                {
                    var modifier = state.Modifiers[i];
                    if (!modifiers.TryGetValue(modifier.AttributeId, out var list))
                    {
                        list = new List<AttributeModifierEntry>();
                        modifiers[modifier.AttributeId] = list;
                    }

                    list.Add(new AttributeModifierEntry
                    {
                        AttributeId = modifier.AttributeId,
                        Handle = modifier.Handle,
                        Op = modifier.Op,
                        Value = modifier.Value,
                        Source = modifier.Source,
                    });
                    modifierAttributeIds[modifier.Handle] = modifier.AttributeId;
                    MarkDirty(modifier.AttributeId);
                }
            }

            nextModifierId = Math.Max(1, state.NextModifierId);
            EnsureNextModifierIdAfterRestore();

            if (notifyChanges)
            {
                NotifyRestoredBaseChanges(oldBaseValues);
                NotifyRestoredChanges(oldCurrentValues);
            }
        }

        protected virtual float CalculateCurrentValue(int attributeId)
        {
            var baseValue = GetBaseValue(attributeId);
            var additive = 0f;
            var multiplier = 1f;
            var hasOverride = false;
            var overrideValue = 0f;

            if (modifiers.TryGetValue(attributeId, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var modifier = list[i];

                    switch (modifier.Op)
                    {
                        case AttributeModifierOp.Add:
                            additive += modifier.Value;
                            break;

                        case AttributeModifierOp.Multiply:
                            multiplier *= modifier.Value;
                            break;

                        case AttributeModifierOp.Override:
                            hasOverride = true;
                            overrideValue = modifier.Value;
                            break;
                    }
                }
            }

            var value = hasOverride ? overrideValue : (baseValue + additive) * multiplier;
            return ClampAttributeValue(attributeId, value);
        }

        protected virtual float PreAttributeBaseChange(int attributeId, float newValue)
        {
            return newValue;
        }

        protected virtual float PreAttributeChange(int attributeId, float newValue)
        {
            return newValue;
        }

        protected virtual void PostAttributeBaseChange(int attributeId, float oldValue, float newValue)
        {
        }

        protected virtual void PostAttributeChange(int attributeId, float oldValue, float newValue)
        {
        }

        protected virtual float ClampAttributeValue(int attributeId, float value)
        {
            return PreAttributeChange(attributeId, value);
        }

        protected virtual void NotifyAttributeBaseValueChanged(int attributeId, float oldValue, float newValue)
        {
            if (Math.Abs(oldValue - newValue) <= ChangeEpsilon)
                return;

            OnAttributeBaseValueChanged?.Invoke(attributeId, oldValue, newValue);
        }

        protected virtual void NotifyAttributeChanged(int attributeId, float oldValue, float newValue)
        {
            if (Math.Abs(oldValue - newValue) <= ChangeEpsilon)
                return;

            PostAttributeChange(attributeId, oldValue, newValue);
            OnAttributeChanged?.Invoke(attributeId, oldValue, newValue);
        }

        private void MarkDirty(int attributeId)
        {
            dirtyAttributes.Add(attributeId);
        }

        private AttributeModifierState[] CaptureModifierStates()
        {
            var count = 0;
            foreach (var kv in modifiers)
            {
                count += kv.Value.Count;
            }

            if (count == 0)
                return Array.Empty<AttributeModifierState>();

            var states = new AttributeModifierState[count];
            var index = 0;

            foreach (var kv in modifiers)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    var modifier = list[i];
                    states[index++] = new AttributeModifierState
                    {
                        AttributeId = modifier.AttributeId,
                        Handle = modifier.Handle,
                        Op = modifier.Op,
                        Value = modifier.Value,
                        Source = modifier.Source,
                    };
                }
            }

            return states;
        }

        private void CaptureCurrentValues(Dictionary<int, float> values)
        {
            foreach (var kv in baseValues)
            {
                values[kv.Key] = GetAttribute(kv.Key);
            }

            foreach (var kv in modifiers)
            {
                values[kv.Key] = GetAttribute(kv.Key);
            }
        }

        private void CaptureBaseValues(Dictionary<int, float> values)
        {
            foreach (var kv in baseValues)
            {
                values[kv.Key] = kv.Value;
            }
        }

        private void NotifyRestoredBaseChanges(Dictionary<int, float> oldValues)
        {
            var notifiedAttributes = new HashSet<int>();

            foreach (var kv in baseValues)
            {
                oldValues.TryGetValue(kv.Key, out var oldValue);
                NotifyAttributeBaseValueChanged(kv.Key, oldValue, kv.Value);
                notifiedAttributes.Add(kv.Key);
            }

            foreach (var kv in oldValues)
            {
                if (!notifiedAttributes.Contains(kv.Key))
                {
                    NotifyAttributeBaseValueChanged(kv.Key, kv.Value, GetBaseValue(kv.Key));
                }
            }
        }

        private void NotifyRestoredChanges(Dictionary<int, float> oldValues)
        {
            var notifiedAttributes = new HashSet<int>();

            foreach (var kv in baseValues)
            {
                NotifyRestoredChange(kv.Key, oldValues);
                notifiedAttributes.Add(kv.Key);
            }

            foreach (var kv in modifiers)
            {
                if (notifiedAttributes.Contains(kv.Key))
                    continue;

                NotifyRestoredChange(kv.Key, oldValues);
                notifiedAttributes.Add(kv.Key);
            }

            foreach (var kv in oldValues)
            {
                if (!notifiedAttributes.Contains(kv.Key))
                {
                    NotifyAttributeChanged(kv.Key, kv.Value, GetAttribute(kv.Key));
                }
            }
        }

        private void NotifyRestoredChange(int attributeId, Dictionary<int, float> oldValues)
        {
            oldValues.TryGetValue(attributeId, out var oldValue);
            NotifyAttributeChanged(attributeId, oldValue, GetAttribute(attributeId));
        }

        private void EnsureNextModifierIdAfterRestore()
        {
            foreach (var kv in modifiers)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Handle.Id >= nextModifierId)
                    {
                        nextModifierId = list[i].Handle.Id + 1;
                    }
                }
            }
        }

        private struct AttributeModifierEntry
        {
            public int AttributeId;
            public AttributeModifierHandle Handle;
            public AttributeModifierOp Op;
            public float Value;
            public object Source;
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }

    [Serializable]
    public struct AttributeValueState
    {
        public int AttributeId;
        public float Value;
    }

    [Serializable]
    public struct AttributeModifierState
    {
        public int AttributeId;
        public AttributeModifierHandle Handle;
        public AttributeModifierOp Op;
        public float Value;
        public object Source;
    }

    [Serializable]
    public struct AttributeSetState
    {
        public AttributeValueState[] BaseValues;
        public AttributeModifierState[] Modifiers;
        public int NextModifierId;

        public bool HasState =>
            NextModifierId > 0 ||
            BaseValues != null && BaseValues.Length > 0 ||
            Modifiers != null && Modifiers.Length > 0;
    }
}
