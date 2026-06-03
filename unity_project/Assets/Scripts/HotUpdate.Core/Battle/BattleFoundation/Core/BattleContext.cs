namespace BattleFoundation
{
    public class BattleContext : Disposable, IBattleContext
    {
        public BattleEngine Engine { get; protected set; }
        public EntityManager EntityManager { get; protected set; }
        public BattleEventBus EventBus { get; protected set; }
        public BattleSystemManager SystemManager { get; protected set; }
        public BattleRandom Random { get; protected set; }

        public virtual void Initialize(BattleEngine engine)
        {
            Initialize(engine, engine?.Settings);
        }

        public virtual void Initialize(BattleEngine engine, BattleRuntimeSettings settings)
        {
            Engine = engine;
            EntityManager = new EntityManager(this);
            EventBus = new BattleEventBus();
            SystemManager = new BattleSystemManager();
            Random = new BattleRandom(settings?.RandomSeed ?? 1);
        }

        public virtual T AddSystem<T>(T system) where T : IBattleSystem
        {
            if (system == null) return system;

            SystemManager.EnsureCanRegister(system);
            system.Initialize(this);
            SystemManager.Register(system);

            return system;
        }

        public virtual T GetSystem<T>() where T : class, IBattleSystem
        {
            return SystemManager.Get<T>();
        }

        public virtual void Start()
        {
            SystemManager?.Start();
        }

        public virtual void Update(float deltaTime)
        {
            SystemManager?.Update(deltaTime);
        }

        public virtual void LateUpdate(float deltaTime)
        {
            SystemManager?.LateUpdate(deltaTime);
        }

        protected override void OnDispose()
        {
            DisposeSystems();
            EntityManager?.Dispose();
            EntityManager = null;
            EventBus?.Dispose();
            EventBus = null;
            SystemManager = null;
            Random = null;
            Engine = null;
            base.OnDispose();
        }

        protected virtual void DisposeSystems()
        {
            SystemManager?.Dispose();
        }
    }
}
