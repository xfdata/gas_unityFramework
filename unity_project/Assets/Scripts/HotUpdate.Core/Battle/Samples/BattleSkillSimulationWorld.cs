using System;
using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace BattleSkillSimulation
{
    public sealed class BattleSkillSimulationWorld : IDisposable
    {
        private static Vector3 ToVector3(Float3 f) => new Vector3(f.x, f.y, f.z);

        private readonly SimulationBattleEngine _engine;
        private readonly SimulationActor _player;
        private readonly SimulationActor _npc;
        private readonly GameplayDefinitionCatalog _catalog;
        private readonly MeleeAttackAbilityDefinition _skillAbility;
        private readonly GameplayEffectDefinition _damageEffect;
        private readonly CombatDamageExecution _damageExecution;
        private readonly float _skillCooldown;
        private readonly Action<ActorDiedEvent> _onActorDied;

        private float _nextSkillTime;

        public string LastMessage { get; private set; } = "Ready. WASD move, J cast skill.";
        public float PlayerHP => ReadHP(_player);
        public float PlayerMaxHP => ReadMaxHP(_player);
        public float NpcHP => ReadHP(_npc);
        public float NpcMaxHP => ReadMaxHP(_npc);

        internal BattleSkillSimulationWorld(
            SimulationBattleEngine engine,
            SimulationActor player,
            SimulationActor npc,
            GameplayDefinitionCatalog catalog,
            MeleeAttackAbilityDefinition skillAbility,
            GameplayEffectDefinition damageEffect,
            CombatDamageExecution damageExecution,
            float skillCooldown)
        {
            _engine = engine;
            _player = player;
            _npc = npc;
            _catalog = catalog;
            _skillAbility = skillAbility;
            _damageEffect = damageEffect;
            _damageExecution = damageExecution;
            _skillCooldown = skillCooldown;

            // R3-S10 修复: 改为通过 EventBus 订阅 ActorDiedEvent，替代 Obsolete 的 OnDeath C# 事件。
            _onActorDied = OnActorDied;
            _engine.Context.EventBus.On(CombatActorEventIds.ActorDied, _onActorDied);
            _player.Get<BattleSkillSimulationMoveComponent>()?.Face(ToVector3(_npc.Position));
        }

        public void Update(float deltaTime)
        {
            _engine?.UpdateFromUnity(deltaTime);
        }

        public void MovePlayer(Vector3 inputDirection, float deltaTime)
        {
            var movement = _player?.Get<BattleSkillSimulationMoveComponent>();
            if (movement == null)
                return;

            if (inputDirection.sqrMagnitude > 0.001f)
                movement.Move(inputDirection, deltaTime);
            else
                movement.Face(ToVector3(_npc.Position));
        }

        public void CastSkill(float currentTime)
        {
            if (_player == null || _npc == null)
                return;

            if (currentTime < _nextSkillTime)
            {
                LastMessage = "Skill cooling down.";
                return;
            }

            float oldHP = NpcHP;
            _player.Get<BattleSkillSimulationMoveComponent>()?.Face(ToVector3(_npc.Position));

            bool activated = _player.Get<CombatAbilityComponent>()?.TryActivateById(CombatAbilityIds.Skill) ?? false;
            _nextSkillTime = currentTime + _skillCooldown;

            float damage = Mathf.Max(0f, oldHP - NpcHP);
            if (!activated)
                LastMessage = "Skill failed.";
            else if (NpcHP <= 0f)
                LastMessage = "NPC defeated. Press R to reset.";
            else if (damage > 0f)
                LastMessage = $"Hit NPC for {damage:0}.";
            else
                LastMessage = "Skill cast but missed.";
        }

        public void Dispose()
        {
            // R3-S10 修复: 反订阅 EventBus。
            _engine?.Context?.EventBus?.Off(CombatActorEventIds.ActorDied, _onActorDied);
            _engine?.Dispose();
            DestroyRuntimeAsset(_skillAbility);
            DestroyRuntimeAsset(_damageEffect);
            DestroyRuntimeAsset(_damageExecution);
            DestroyRuntimeAsset(_catalog);
        }

        private void OnActorDied(ActorDiedEvent evt)
        {
            // R3-S10 修复: 通过 ActorDiedEvent 接收死亡事件（替代 OnNpcDeath）。
            if (_npc != null && evt.EntityId == _npc.Id)
            {
                LastMessage = "NPC defeated. Press R to reset.";
            }
        }

        private static float ReadHP(CombatActor actor)
        {
            return actor?.Get<CombatAttributeComponent>()?.HP ?? 0f;
        }

        private static float ReadMaxHP(CombatActor actor)
        {
            return actor?.Get<CombatAttributeComponent>()?.MaxHP ?? 0f;
        }

        private static void DestroyRuntimeAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(asset);
            else
                UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}