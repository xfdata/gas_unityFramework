using System;
using System.Collections.Generic;

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

    /// <summary>
    /// 组件接口标记，允许 Get&lt;T&gt;() 接受接口类型而非仅 EntityComponent 子类。
    /// L2 接口（如 ICombatHealthComponent）继承此接口后，可通过 Get&lt;ICombatHealthComponent&gt;() 查询。
    /// </summary>
    public interface IEntityComponent
    {
    }

    public class EntityComponent : Disposable, IEntityComponent
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
        protected int _id;
        protected EEntityCamp _camp;
        protected EEntityType _entityType;

        protected List<EntityComponent> _components = new List<EntityComponent>();
        protected Dictionary<Type, EntityComponent> _componentMap = new Dictionary<Type, EntityComponent>();
        private bool _isInitialized;
        private bool _hasStarted;

        public int Id => _id;
        public EEntityCamp Camp => _camp;
        public EEntityType EntityType => _entityType;
        public virtual bool IsAlive { get; set; } = true;
        public virtual Float3 Position { get; set; }
        public virtual Float4 Rotation { get; set; }
        public BattleEngine Engine { get; set; }
        public bool IsInitialized => _isInitialized;
        public bool HasStarted => _hasStarted;

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

            if (_isInitialized)
                component.Initialize();
            if (_hasStarted)
                component.Start();

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

        public T Get<T>() where T : class, IEntityComponent
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

        public bool Has<T>() where T : class, IEntityComponent
        {
            return Get<T>() != null;
        }

        public virtual void Initialize()
        {
            if (_isInitialized)
                return;

            IsAlive = true;
            for (int i = 0; i < _components.Count; i++)
                _components[i].Initialize();
            _isInitialized = true;
        }

        public virtual void Start()
        {
            if (!_isInitialized || _hasStarted)
                return;

            _hasStarted = true;
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
            Position = Float3.zero;
            Rotation = Float4.identity;
            _isInitialized = false;
            _hasStarted = false;

            for (int i = 0; i < _components.Count; i++)
                _components[i].ActivateForPool(this);
        }

        public virtual void DeactivateForPool()
        {
            IsAlive = false;
            _isInitialized = false;
            _hasStarted = false;
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
            _isInitialized = false;
            _hasStarted = false;
            Engine = null;
        }
    }
}
