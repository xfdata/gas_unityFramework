using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleFoundation
{
    public enum EEntityCamp
    {
        None = 0,
        Ally = 1,
        Enemy = 2,
        Neutral = 3,
    }

    public enum EEntityType
    {
        Unknown,
        Hero,
        Monster,
        Boss,
        Summon,
        Structure,
        Projectile,
    }

    public class EntityComponent : Disposable
    {
        public BattleEntity Owner { get; private set; }
        public bool IsActive { get; private set; } = true;

        public virtual void Attach(BattleEntity owner)
        {
            Owner = owner;
            IsActive = true;
        }

        public virtual void Initialize() { }
        public virtual void Start() { }
        public virtual void Update(float deltaTime) { }
        public virtual void LateUpdate(float deltaTime) { }

        public virtual void ActivateForPool(BattleEntity owner)
        {
            Owner = owner;
            IsActive = true;
        }

        public virtual void DeactivateForPool()
        {
            IsActive = false;
            Owner = null;
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            Owner = null;
        }
    }

    public abstract class BattleEntity : Disposable
    {
        [SerializeField]
        protected int _id;

        [SerializeField]
        protected EEntityCamp _camp;

        [SerializeField]
        protected EEntityType _entityType;

        protected List<EntityComponent> _components = new List<EntityComponent>();
        protected Dictionary<Type, EntityComponent> _componentMap = new Dictionary<Type, EntityComponent>();

        public int Id => _id;
        public EEntityCamp Camp => _camp;
        public EEntityType EntityType => _entityType;
        public virtual bool IsAlive { get; set; } = true;
        public virtual Vector3 Position { get; set; }
        public virtual Quaternion Rotation { get; set; }
        public BattleEngine Engine { get; set; }

        public void SetId(int id) => _id = id;
        public void SetCamp(EEntityCamp camp) => _camp = camp;
        public void SetEntityType(EEntityType type) => _entityType = type;

        public IReadOnlyList<EntityComponent> Components => _components;

        public T AddComponent<T>(T component) where T : EntityComponent
        {
            if (component == null) return null;

            var type = component.GetType();
            if (_componentMap.ContainsKey(type))
                return _componentMap[type] as T;

            component.Attach(this);
            _components.Add(component);
            _componentMap[type] = component;

            return component;
        }

        public T AddComponent<T>() where T : EntityComponent, new()
        {
            return AddComponent(new T());
        }

        public bool RemoveComponent<T>() where T : EntityComponent
        {
            EntityComponent comp = null;
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i] is T)
                {
                    comp = _components[i];
                    break;
                }
            }

            if (comp == null) return false;

            comp.Dispose();
            _components.Remove(comp);
            _componentMap.Remove(comp.GetType());
            return true;
        }

        public T Get<T>() where T : EntityComponent
        {
            var type = typeof(T);
            if (_componentMap.TryGetValue(type, out var comp))
                return comp as T;

            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i] is T result)
                    return result;
            }
            return null;
        }

        public bool Has<T>() where T : EntityComponent
        {
            return Get<T>() != null;
        }

        public virtual void Initialize()
        {
            IsAlive = true;
            for (int i = 0; i < _components.Count; i++)
                _components[i].Initialize();
        }

        public virtual void Start()
        {
            for (int i = 0; i < _components.Count; i++)
                _components[i].Start();
        }

        public virtual void Update(float deltaTime)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i].IsActive)
                    _components[i].Update(deltaTime);
            }
        }

        public virtual void LateUpdate(float deltaTime)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i].IsActive)
                    _components[i].LateUpdate(deltaTime);
            }
        }

        public virtual void Die()
        {
            if (!IsAlive) return;
            IsAlive = false;
        }

        public virtual void ActivateForPool(int id, EEntityCamp camp, EEntityType type)
        {
            _id = id;
            _camp = camp;
            _entityType = type;
            IsAlive = true;
            Position = Vector3.zero;
            Rotation = Quaternion.identity;

            for (int i = 0; i < _components.Count; i++)
                _components[i].ActivateForPool(this);
        }

        public virtual void DeactivateForPool()
        {
            IsAlive = false;
            for (int i = 0; i < _components.Count; i++)
                _components[i].DeactivateForPool();
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            for (int i = _components.Count - 1; i >= 0; i--)
                _components[i].Dispose();
            _components.Clear();
            _componentMap.Clear();
            Engine = null;
        }
    }
}
