namespace BattleFoundation
{
    public interface IBattleContext
    {
        BattleEngine Engine { get; }
        EntityManager EntityManager { get; }
        BattleEventBus EventBus { get; }
        BattleSystemManager SystemManager { get; }
        BattleRandom Random { get; }

        T AddSystem<T>(T system) where T : IBattleSystem;
        T GetSystem<T>() where T : class, IBattleSystem;
        void Start();
        void Update(float deltaTime);
        void LateUpdate(float deltaTime);
        void Dispose();
    }

    public interface IBattleSystem
    {
        void Initialize(IBattleContext context);
        void Start();
        void Update(float deltaTime);
        void LateUpdate(float deltaTime);
        void Dispose();
    }
}
