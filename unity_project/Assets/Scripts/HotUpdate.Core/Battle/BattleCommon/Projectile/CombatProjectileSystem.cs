using BattleFoundation;
using GAS;

namespace BattleCommon
{
    public class CombatProjectileSystem : IBattleSystem
    {
        public ProjectileRuntime Runtime { get; private set; } = new ProjectileRuntime();

        public void Initialize(IBattleContext context) { }
        public void Start() { }
        public void Update(float deltaTime) => Runtime?.Tick(deltaTime);
        public void LateUpdate(float deltaTime) { }
        public void Dispose() => Runtime = null;
    }
}
