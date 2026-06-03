using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleFoundation
{
    public enum EAttributeModifierOp
    {
        Add,
        Multiply,
        Override,
    }

    public struct AttributeModifier
    {
        public EAttributeModifierOp Op;
        public float Value;
        public object Source;

        public AttributeModifier(EAttributeModifierOp op, float value, object source = null)
        {
            Op = op;
            Value = value;
            Source = source;
        }
    }

    public class BattleAttribute : Disposable
    {
        private float _baseValue;
        private float _minValue = float.MinValue;
        private float _maxValue = float.MaxValue;
        private List<AttributeModifier> _modifiers = new List<AttributeModifier>();
        private float _cachedValue;
        private bool _dirty = true;

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                if (Mathf.Approximately(_baseValue, value)) return;
                float oldValue = Value;
                _baseValue = value;
                _dirty = true;
                NotifyChanged(oldValue);
            }
        }

        public float Value
        {
            get
            {
                if (_dirty)
                {
                    _cachedValue = CalcFinalValue();
                    _dirty = false;
                }
                return _cachedValue;
            }
        }

        public event Action<BattleAttribute, float, float> OnChanged;

        public BattleAttribute(float baseValue = 0f)
        {
            _baseValue = baseValue;
        }

        public void SetMinMax(float min, float max)
        {
            float oldValue = Value;
            _minValue = min;
            _maxValue = max;
            if (_baseValue < min) _baseValue = min;
            if (_baseValue > max) _baseValue = max;
            _dirty = true;
            NotifyChanged(oldValue);
        }

        public void AddModifier(AttributeModifier modifier)
        {
            float oldValue = Value;
            _modifiers.Add(modifier);
            _dirty = true;
            NotifyChanged(oldValue);
        }

        public bool RemoveModifier(AttributeModifier modifier)
        {
            float oldValue = Value;
            bool removed = _modifiers.Remove(modifier);
            if (removed)
            {
                _dirty = true;
                NotifyChanged(oldValue);
            }
            return removed;
        }

        public int RemoveModifiersFromSource(object source)
        {
            float oldValue = Value;
            int count = _modifiers.RemoveAll(m => m.Source == source);
            if (count > 0)
            {
                _dirty = true;
                NotifyChanged(oldValue);
            }
            return count;
        }

        public void ClearModifiers()
        {
            if (_modifiers.Count == 0) return;
            float oldValue = Value;
            _modifiers.Clear();
            _dirty = true;
            NotifyChanged(oldValue);
        }

        private float CalcFinalValue()
        {
            float additive = 0f;
            float multiplicative = 1f;
            bool hasOverride = false;
            float overrideValue = 0f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                var mod = _modifiers[i];
                switch (mod.Op)
                {
                    case EAttributeModifierOp.Add:
                        additive += mod.Value;
                        break;
                    case EAttributeModifierOp.Multiply:
                        multiplicative *= (1f + mod.Value);
                        break;
                    case EAttributeModifierOp.Override:
                        hasOverride = true;
                        overrideValue = mod.Value;
                        break;
                }
            }

            float result;
            if (hasOverride)
            {
                result = overrideValue + additive;
            }
            else
            {
                result = (_baseValue + additive) * multiplicative;
            }

            return Mathf.Clamp(result, _minValue, _maxValue);
        }

        private void NotifyChanged(float oldValue)
        {
            float newValue = Value;
            if (!Mathf.Approximately(oldValue, newValue))
                OnChanged?.Invoke(this, oldValue, newValue);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _modifiers.Clear();
            OnChanged = null;
        }
    }

    public static class CommonAttributeIds
    {
        public const int HP = 1;
        public const int MaxHP = 2;
        public const int ATK = 3;
        public const int DEF = 4;
        public const int MoveSpeed = 5;
        public const int AttackSpeed = 6;
        public const int AttackRange = 7;
        public const int CritRate = 8;
        public const int CritDamage = 9;
        public const int DamageUp1 = 10;
        public const int DamageUp2 = 11;
        public const int DamageReduce1 = 12;
        public const int DamageReduce2 = 13;
        public const int AbsoluteReduce = 14;
    }

    public class BattleAttributeSet : EntityComponent
    {
        private Dictionary<int, BattleAttribute> _attributes = new Dictionary<int, BattleAttribute>();

        public event Action<int, float, float> OnAttributeChanged;

        public void RegisterAttribute(int attrId, float baseValue = 0f)
        {
            if (_attributes.ContainsKey(attrId)) return;

            var attr = new BattleAttribute(baseValue);
            attr.OnChanged += (a, oldValue, newValue) => OnAttributeChanged?.Invoke(attrId, oldValue, newValue);
            _attributes[attrId] = attr;
        }

        public BattleAttribute GetAttribute(int attrId)
        {
            _attributes.TryGetValue(attrId, out var attr);
            return attr;
        }

        public float GetValue(int attrId)
        {
            return _attributes.TryGetValue(attrId, out var attr) ? attr.Value : 0f;
        }

        public float GetBaseValue(int attrId)
        {
            return _attributes.TryGetValue(attrId, out var attr) ? attr.BaseValue : 0f;
        }

        public void SetBaseValue(int attrId, float value)
        {
            if (_attributes.TryGetValue(attrId, out var attr))
                attr.BaseValue = value;
        }

        public void AddModifier(int attrId, AttributeModifier modifier)
        {
            if (_attributes.TryGetValue(attrId, out var attr))
                attr.AddModifier(modifier);
        }

        public void RemoveModifier(int attrId, AttributeModifier modifier)
        {
            if (_attributes.TryGetValue(attrId, out var attr))
                attr.RemoveModifier(modifier);
        }

        public int RemoveModifiersFromSource(object source)
        {
            int count = 0;
            foreach (var kv in _attributes)
            {
                count += kv.Value.RemoveModifiersFromSource(source);
            }
            return count;
        }

        public void SetMinMax(int attrId, float min, float max)
        {
            if (_attributes.TryGetValue(attrId, out var attr))
                attr.SetMinMax(min, max);
        }

        public IReadOnlyDictionary<int, BattleAttribute> GetAllAttributes()
        {
            return _attributes;
        }

        public AttributeSnapshot Snapshot()
        {
            var snapshot = new AttributeSnapshot();
            foreach (var kv in _attributes)
            {
                snapshot.AttributeIds.Add(kv.Key);
                snapshot.BaseValues.Add(kv.Value.BaseValue);
            }
            return snapshot;
        }

        public void RestoreSnapshot(AttributeSnapshot snapshot)
        {
            if (snapshot == null) return;
            int count = Mathf.Min(snapshot.AttributeIds.Count, snapshot.BaseValues.Count);
            for (int i = 0; i < count; i++)
            {
                if (_attributes.TryGetValue(snapshot.AttributeIds[i], out var attr))
                    attr.BaseValue = snapshot.BaseValues[i];
            }
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            foreach (var kv in _attributes)
                kv.Value.Dispose();
            _attributes.Clear();
            OnAttributeChanged = null;
        }
    }

    [Serializable]
    public class AttributeSnapshot
    {
        public List<int> AttributeIds = new List<int>();
        public List<float> BaseValues = new List<float>();
    }
}
