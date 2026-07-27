using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// 主城被摧毁规则 — 监听MainCityDestroyed事件，触发战斗失败。
    /// </summary>
    public class MainCityDestroyedRule : BattleRuleBase
    {
        private MainCitySystem _mainCitySystem;
        private bool _subscribed;

        protected override void OnInitialize()
        {
            _mainCitySystem = Engine?.Context?.GetSystem<MainCitySystem>();
        }

        protected override void OnUpdate(float deltaTime)
        {
            // 如果已订阅过，跳过重复订阅
            if (_subscribed) return;

            // 规则Update时订阅事件（确保EventBus已初始化）
            var eventBus = Engine?.Context?.EventBus;
            if (eventBus != null)
            {
                eventBus.On<MainCityDestroyedEvent>(TDEventIds.MainCityDestroyed, OnCityDestroyed);
                _subscribed = true;
            }
        }

        private void OnCityDestroyed(MainCityDestroyedEvent evt)
        {
            if (IsTriggered) return;
            Trigger(EBattleResult.Lose);
        }

        protected override void OnDispose()
        {
            if (_subscribed)
            {
                Engine?.Context?.EventBus?.Off<MainCityDestroyedEvent>(TDEventIds.MainCityDestroyed, OnCityDestroyed);
                _subscribed = false;
            }
            _mainCitySystem = null;
            base.OnDispose();
        }
    }

    /// <summary>
    /// 所有波次清除规则 — 由VictoryCheckSystem触发胜利事件后，此Rule监听并触发战斗胜利。
    /// 
    /// 改为监听 VictoryCheckSystem 发射的事件，而非直接检查波次状态。
    /// 这样解耦了胜利条件检查与Rule触发逻辑。
    /// </summary>
    public class AllWavesClearedRule : BattleRuleBase
    {
        private bool _subscribed;

        public AllWavesClearedRule() { }

        protected override void OnInitialize() { }

        protected override void OnUpdate(float deltaTime)
        {
            if (_subscribed) return;

            var eventBus = Engine?.Context?.EventBus;
            if (eventBus != null)
            {
                // 监听VictoryCheckSystem发射的胜利事件
                eventBus.On<int>(TDEventIds.AllWavesCleared, OnAllWavesCleared);
                _subscribed = true;
            }
        }

        private void OnAllWavesCleared(int dummy)
        {
            if (IsTriggered) return;
            BattleLog.BattleEndWarning($"AllWavesClearedRule.OnAllWavesCleared → Trigger(EBattleResult.Win)! EndBattle will be called.");
            Trigger(EBattleResult.Win);
        }

        protected override void OnDispose()
        {
            if (_subscribed)
            {
                Engine?.Context?.EventBus?.Off<int>(TDEventIds.AllWavesCleared, OnAllWavesCleared);
                _subscribed = false;
            }
            base.OnDispose();
        }
    }
}
