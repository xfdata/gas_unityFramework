using BattleCommon;
using BattleFoundation;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 主城Actor — 静态BattleEntity，挂载血量组件和属性组件。
    /// 
    /// 不需要移动组件，不需要AI。核心职责：
    /// - 持有血量（通过MainCityHealthComponent）
    /// - 持有属性（通过CombatAttributeComponent）
    /// - 承受到达终点的敌人伤害
    /// </summary>
    public class MainCityActor : BattleEntity
    {
        private MainCityConfig _config;

        public MainCityConfig Config => _config;
        public MainCityHealthComponent Health => Get<MainCityHealthComponent>();
        public CombatAttributeComponent Attributes => Get<CombatAttributeComponent>();

        /// <summary>
        /// 初始化主城
        /// </summary>
        public void InitCity(MainCityConfig config, Vector3 position)
        {
            _config = config;
            SetCamp(EEntityCamp.Ally);
            SetEntityType(EEntityType.Structure);

            // 添加/获取组件
            var attributes = Get<CombatAttributeComponent>();
            if (attributes == null)
                attributes = AddComponent<CombatAttributeComponent>();

            var health = Get<MainCityHealthComponent>();
            if (health == null)
                health = AddComponent<MainCityHealthComponent>();

            // 初始化属性
            health.Init(config.MaxHp, config.Defense);

            // 设置位置、GameObject
            Position = position;
            if (config.Prefab != null)
            {
                var go = Object.Instantiate(config.Prefab, position, Quaternion.identity);
                // 主城GameObject不存Transform到Actor，仅作视觉呈现
            }

            base.Initialize();
            base.Start();
        }

        public override void Die()
        {
            if (!IsAlive) return;
            base.Die();
        }
    }
}
