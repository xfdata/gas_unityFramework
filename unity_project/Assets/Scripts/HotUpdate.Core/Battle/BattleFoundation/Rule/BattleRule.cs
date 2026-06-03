using System;

namespace BattleFoundation
{
    public enum EBattleResult
    {
        None,
        Win,
        Lose,
        Draw,
        Timeout,
    }

    public abstract class BattleRuleBase : Disposable
    {
        public bool IsTriggered { get; protected set; }
        public EBattleResult Result { get; protected set; }
        public float ElapsedTime { get; protected set; }

        protected BattleEngine Engine { get; private set; }

        public void Initialize(BattleEngine engine)
        {
            Engine = engine;
            IsTriggered = false;
            Result = EBattleResult.None;
            ElapsedTime = 0f;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }

        public void Update(float deltaTime)
        {
            if (IsTriggered) return;
            ElapsedTime += deltaTime;
            OnUpdate(deltaTime);
        }

        protected abstract void OnUpdate(float deltaTime);

        protected void Trigger(EBattleResult result)
        {
            if (IsTriggered) return;
            IsTriggered = true;
            Result = result;
            Engine.Context.EventBus.Emit(BattleEventIds.RuleTriggered, this);
        }

        public EBattleResult GetBattleResult() => Result;

        protected override void OnDispose()
        {
            base.OnDispose();
            Engine = null;
        }
    }

    public abstract class WinLoseConditionBase : BattleRuleBase
    {
        [Serializable]
        public struct Condition
        {
            public EBattleResult Result;
            public string Description;
        }

        protected abstract Condition Evaluate();
    }

    public class TimeoutRule : BattleRuleBase
    {
        private float _timeLimit;

        public TimeoutRule(float timeLimit)
        {
            _timeLimit = timeLimit;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (ElapsedTime >= _timeLimit)
                Trigger(EBattleResult.Timeout);
        }
    }

    public class AllEnemiesDeadRule : BattleRuleBase
    {
        private float _checkInterval = 0.5f;
        private float _checkTimer;

        public AllEnemiesDeadRule() { }

        public AllEnemiesDeadRule(float checkInterval)
        {
            _checkInterval = checkInterval;
        }

        protected override void OnUpdate(float deltaTime)
        {
            _checkTimer += deltaTime;
            if (_checkTimer < _checkInterval) return;
            _checkTimer = 0f;

            var entityManager = Engine?.Context?.EntityManager;
            if (entityManager == null) return;

            int aliveEnemies = entityManager.AliveCountByCamp(EEntityCamp.Enemy);
            int aliveAllies = entityManager.AliveCountByCamp(EEntityCamp.Ally);

            if (aliveEnemies <= 0)
                Trigger(EBattleResult.Win);

            if (aliveAllies <= 0)
                Trigger(EBattleResult.Lose);
        }
    }
}