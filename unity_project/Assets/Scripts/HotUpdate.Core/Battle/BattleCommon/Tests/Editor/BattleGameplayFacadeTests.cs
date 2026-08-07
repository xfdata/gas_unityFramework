using System;
using System.Collections.Generic;
using BattleFoundation;
using GAS;
using NUnit.Framework;
using UnityEngine;

namespace BattleCommon.Tests
{
    [TestFixture]
    public sealed class BattleGameplayFacadeTests
    {
        [Test]
        public void TryCast_GrantedSkill_ActivatesThroughBusinessFacade()
        {
            var engine = new FacadeTestEngine();
            var ability = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            ability.AbilityId = 101;
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterSkill(501, ability);

            try
            {
                engine.Initialize();
                var source = CreateActor(1, ability, gameplayCatalog: gameplayCatalog);
                var target = CreateActor(2, gameplayCatalog: gameplayCatalog);
                engine.ActorSystem.Spawn(source);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();

                var result = source.Gameplay.Skills.TryCast(501, target);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(BattleCastFailureReason.None, result.Failure);
                Assert.AreEqual(501, result.SkillId);
            }
            finally
            {
                engine.Dispose();
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void CombatSkillBehavior_UsesBusinessSkillIdInsteadOfAbilityId()
        {
            var engine = new FacadeTestEngine();
            var ability = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            ability.AbilityId = 105;
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterSkill(505, ability);

            try
            {
                engine.Initialize();
                var source = CreateActor(1, ability, gameplayCatalog: gameplayCatalog);
                var target = CreateActor(2, gameplayCatalog: gameplayCatalog);
                engine.ActorSystem.Spawn(source);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();

                var businessSkill = new CombatSkillBehavior(505);
                businessSkill.Setup(source, new CombatAIProfile());
                businessSkill.Enter(target);
                Assert.AreEqual(CombatAIBehaviorState.Cooldown, businessSkill.State);
                businessSkill.Dispose();

                var rawAbilityId = new CombatSkillBehavior(105);
                rawAbilityId.Setup(source, new CombatAIProfile());
                rawAbilityId.Enter(target);
                Assert.AreEqual(CombatAIBehaviorState.Inactive, rawAbilityId.State);
                rawAbilityId.Dispose();
            }
            finally
            {
                engine.Dispose();
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void ActorConfigurator_InjectsCatalogAndRejectsUnGrantedBusinessSkills()
        {
            var engine = new FacadeTestEngine();
            var ability = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            ability.AbilityId = 106;
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterSkill(506, ability);
            CombatActor configuredActor = null;
            CombatActor invalidActor = null;

            try
            {
                engine.Initialize();
                configuredActor = CreateActor(1);
                var validationErrors = new List<string>();
                Assert.IsTrue(BattleGameplayActorConfigurator.ConfigureBeforeInitialize(
                    configuredActor,
                    gameplayCatalog,
                    new[] { ability },
                    new[] { 506 },
                    validationErrors));
                Assert.AreSame(gameplayCatalog, configuredActor.GameplayCatalog);
                Assert.IsEmpty(validationErrors);

                var target = CreateActor(3);
                Assert.IsTrue(BattleGameplayActorConfigurator.ConfigureBeforeInitialize(
                    target,
                    gameplayCatalog,
                    null,
                    null,
                    validationErrors));
                engine.ActorSystem.Spawn(configuredActor);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();
                Assert.IsTrue(configuredActor.Gameplay.Skills.TryCast(506, target).Success);

                invalidActor = CreateActor(2);
                Assert.IsFalse(BattleGameplayActorConfigurator.ConfigureBeforeInitialize(
                    invalidActor,
                    gameplayCatalog,
                    null,
                    new[] { 506 },
                    validationErrors));
                Assert.IsNull(invalidActor.GameplayCatalog);
                Assert.IsNotEmpty(validationErrors);
            }
            finally
            {
                engine.Dispose();
                invalidActor?.Dispose();
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void UnitGameplayConfig_ResolvesBusinessSkillIdsBeforeSpawn()
        {
            var engine = new FacadeTestEngine();
            var ability = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            ability.AbilityId = 107;
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterSkill(507, ability);
            var unitConfig = ScriptableObject.CreateInstance<BattleUnitGameplayConfig>();
            unitConfig.SetGameplayCatalog(gameplayCatalog);
            unitConfig.RegisterGrantedSkill(507);
            unitConfig.SetBasicAttackSkill(507);
            unitConfig.RegisterAiSkill(507);
            CombatActor invalidActor = null;
            BattleUnitGameplayConfig invalidConfig = null;

            try
            {
                engine.Initialize();
                var source = CreateActor(1);
                var attack = source.AddComponent<CombatAttackComponent>();
                var target = CreateActor(2);
                var validationErrors = new List<string>();
                Assert.IsTrue(BattleGameplayActorConfigurator.ConfigureBeforeInitialize(source, unitConfig, validationErrors));
                Assert.AreEqual(507, attack.BasicAttackSkillId);
                Assert.IsEmpty(validationErrors);

                engine.ActorSystem.Spawn(source);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();
                Assert.IsTrue(source.Gameplay.Skills.TryCast(507, target).Success);

                invalidConfig = ScriptableObject.CreateInstance<BattleUnitGameplayConfig>();
                invalidConfig.SetGameplayCatalog(gameplayCatalog);
                invalidConfig.RegisterAiSkill(507);
                invalidActor = CreateActor(3);
                Assert.IsFalse(BattleGameplayActorConfigurator.ConfigureBeforeInitialize(
                    invalidActor,
                    invalidConfig,
                    validationErrors));
                Assert.IsNull(invalidActor.GameplayCatalog);
            }
            finally
            {
                engine.Dispose();
                invalidActor?.Dispose();
                UnityEngine.Object.DestroyImmediate(invalidConfig);
                UnityEngine.Object.DestroyImmediate(unitConfig);
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void UnitGameplayConfig_ReportsValidationErrorsBeforeBattleStarts()
        {
            var ability = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            ability.AbilityId = 108;
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterSkill(508, ability);
            var validConfig = ScriptableObject.CreateInstance<BattleUnitGameplayConfig>();
            validConfig.SetGameplayCatalog(gameplayCatalog);
            validConfig.RegisterGrantedSkill(508);
            validConfig.SetBasicAttackSkill(508);
            validConfig.RegisterAiSkill(508);
            var invalidConfig = ScriptableObject.CreateInstance<BattleUnitGameplayConfig>();
            invalidConfig.SetGameplayCatalog(gameplayCatalog);
            invalidConfig.RegisterAiSkill(508);

            try
            {
                var validationErrors = new List<string>();
                validConfig.GetValidationErrors(validationErrors);
                Assert.IsTrue(validConfig.IsValid);
                Assert.IsEmpty(validationErrors);

                invalidConfig.GetValidationErrors(validationErrors);
                Assert.IsFalse(invalidConfig.IsValid);
                Assert.IsNotEmpty(validationErrors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidConfig);
                UnityEngine.Object.DestroyImmediate(validConfig);
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void TryAttack_BusinessSkillId_ActivatesThroughFacade()
        {
            var engine = new FacadeTestEngine();
            var ability = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            ability.AbilityId = 102;
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterSkill(502, ability, isBasicAttack: true);

            try
            {
                engine.Initialize();
                var source = CreateActor(1, ability, gameplayCatalog: gameplayCatalog);
                var attributes = source.AddComponent<CombatAttributeComponent>();
                attributes.HP = 10f;
                attributes.MaxHP = 10f;
                attributes.AttackRange = 5f;
                attributes.AttackInterval = 0f;
                source.AddComponent<CombatHealthComponent>();
                var attack = source.AddComponent<CombatAttackComponent>();
                attack.BasicAttackSkillId = 502;

                var target = CreateActor(2, gameplayCatalog: gameplayCatalog);
                engine.ActorSystem.Spawn(source);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();

                Assert.IsTrue(attack.TryAttack(target));
            }
            finally
            {
                engine.Dispose();
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void TryCast_UngrantedSkill_ReturnsSkillNotFound()
        {
            var engine = new FacadeTestEngine();
            try
            {
                engine.Initialize();
                var source = CreateActor(1);
                engine.ActorSystem.Spawn(source);
                engine.StartBattle();

                var result = source.Gameplay.Skills.TryCast(404, null);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(BattleCastFailureReason.SkillNotFound, result.Failure);
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Test]
        public void AttributesAndStates_ExposeBusinessProjectionsWithoutStateComponent()
        {
            var engine = new FacadeTestEngine();
            try
            {
                engine.Initialize();
                var actor = CreateActor(1);
                var attributes = actor.AddComponent<CombatAttributeComponent>();
                engine.ActorSystem.Spawn(actor);
                engine.StartBattle();

                var changes = new List<BattleAttributeChangedEvent>();
                Action<BattleAttributeChangedEvent> onChanged = evt => changes.Add(evt);
                actor.Gameplay.Attributes.Changed += onChanged;

                attributes.HP = 100f;
                attributes.Attack = 25f;

                Assert.AreEqual(100f, actor.Gameplay.Attributes.Get(BattleAttribute.Health));
                Assert.AreEqual(25f, actor.Gameplay.Attributes.Get(BattleAttribute.Attack));
                Assert.AreEqual(2, changes.Count);
                Assert.AreEqual(BattleAttribute.Health, changes[0].Attribute);
                Assert.AreEqual(100f, changes[0].Current);
                Assert.IsTrue(actor.Gameplay.States.CanMove());
                Assert.IsTrue(actor.Gameplay.States.CanBeTargeted());

                var abilities = actor.Get<CombatAbilityComponent>();
                abilities.AddTag(CombatGameplayTags.State_Invincible);
                Assert.IsTrue(actor.Gameplay.States.Has(BattleState.Invincible));

                abilities.AddTag(CombatGameplayTags.State_Dead);
                Assert.IsTrue(actor.Gameplay.States.Has(BattleState.Dead));
                Assert.IsFalse(actor.Gameplay.States.CanMove());
                Assert.IsFalse(actor.Gameplay.States.CanBeTargeted());
                actor.Gameplay.Attributes.Changed -= onChanged;
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Test]
        public void ControlStates_GateBusinessActionsAndTargeting()
        {
            var engine = new FacadeTestEngine();
            var ability = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            ability.AbilityId = 103;
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterSkill(503, ability);

            try
            {
                engine.Initialize();
                var source = CreateActor(1, ability, gameplayCatalog: gameplayCatalog);
                var target = CreateActor(2, gameplayCatalog: gameplayCatalog);
                engine.ActorSystem.Spawn(source);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();

                var sourceAbilities = source.Get<CombatAbilityComponent>();
                sourceAbilities.AddTag(CombatGameplayTags.State_Stunned);
                Assert.IsFalse(source.Gameplay.States.CanMove());
                Assert.IsFalse(source.Gameplay.States.CanAttack());
                Assert.IsFalse(source.Gameplay.States.CanCastSkill());
                Assert.AreEqual(BattleCastFailureReason.Stunned,
                    source.Gameplay.Skills.TryCast(503, target).Failure);

                sourceAbilities.RemoveTag(CombatGameplayTags.State_Stunned);
                sourceAbilities.AddTag(CombatGameplayTags.State_Rooted);
                Assert.IsFalse(source.Gameplay.States.CanMove());
                Assert.IsTrue(source.Gameplay.States.CanAttack());
                Assert.IsTrue(source.Gameplay.States.CanCastSkill());

                sourceAbilities.RemoveTag(CombatGameplayTags.State_Rooted);
                sourceAbilities.AddTag(CombatGameplayTags.State_Silenced);
                Assert.IsTrue(source.Gameplay.States.CanMove());
                Assert.IsTrue(source.Gameplay.States.CanAttack());
                Assert.IsFalse(source.Gameplay.States.CanCastSkill());
                Assert.AreEqual(BattleCastFailureReason.Silenced,
                    source.Gameplay.Skills.TryCast(503, target).Failure);

                sourceAbilities.RemoveTag(CombatGameplayTags.State_Silenced);
                target.Get<CombatAbilityComponent>().AddTag(CombatGameplayTags.State_Untargetable);
                Assert.IsFalse(target.Gameplay.States.CanBeTargeted());
                Assert.IsFalse(target.IsValidTarget);
                Assert.AreEqual(BattleCastFailureReason.TargetInvalid,
                    source.Gameplay.Skills.TryCast(503, target).Failure);
            }
            finally
            {
                engine.Dispose();
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void RootedState_StopsAnExistingMovement()
        {
            var engine = new FacadeTestEngine();
            try
            {
                engine.Initialize();
                var actor = CreateActor(1);
                var movement = actor.AddComponent<CombatMovementComponent>();
                var motor = new TestMovementMotor();
                movement.SetMotor(motor);
                engine.ActorSystem.Spawn(actor);
                engine.StartBattle();

                movement.MoveTo(Vector3.right);
                Assert.IsTrue(motor.IsMoving);

                actor.Get<CombatAbilityComponent>().AddTag(CombatGameplayTags.State_Rooted);
                movement.Update(0f);

                Assert.IsFalse(motor.IsMoving);
                Assert.AreEqual(1, motor.StopCount);
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Test]
        public void Effects_ApplyBusinessEffectId_ChangesTheTargetAttribute()
        {
            var engine = new FacadeTestEngine();
            var effect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            effect.EffectId = 301;
            effect.DurationPolicy = GameplayEffectDurationPolicy.Instant;
            effect.Modifiers.Add(new GameplayEffectDefinition.Modifier
            {
                AttributeId = CombatAttributeIds.HP,
                Op = AttributeModifierOp.Add,
                Value = -25f,
            });
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterEffect(701, effect);

            try
            {
                engine.Initialize();
                var source = CreateActor(1, gameplayCatalog: gameplayCatalog);
                var target = CreateActor(2, gameplayCatalog: gameplayCatalog);
                var targetAttributes = target.AddComponent<CombatAttributeComponent>();
                targetAttributes.HP = 100f;
                engine.ActorSystem.Spawn(source);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();

                var result = source.Gameplay.Effects.Apply(701, target);

                Assert.IsTrue(result.Success);
                Assert.IsTrue(result.IsInstant);
                Assert.AreEqual(75f, target.Gameplay.Attributes.Get(BattleAttribute.Health));
            }
            finally
            {
                engine.Dispose();
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(effect);
            }
        }

        [Test]
        public void Buffs_ApplyAndRemove_ProjectsTheActiveGameplayEffect()
        {
            var engine = new FacadeTestEngine();
            var catalog = ScriptableObject.CreateInstance<GameplayDefinitionCatalog>();
            var effect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            effect.EffectId = 201;
            effect.DurationPolicy = GameplayEffectDurationPolicy.Duration;
            effect.Duration = 10f;
            effect.StackPolicy = GameplayEffectStackPolicy.StackByTarget;
            effect.MaxStack = 2;
            catalog.RegisterEffect(effect);
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterBuff(601, effect, isDebuff: true, canDispel: false);

            try
            {
                engine.Initialize();
                var source = CreateActor(1, services: new TestAbilityServices(catalog), gameplayCatalog: gameplayCatalog);
                var target = CreateActor(2, services: new TestAbilityServices(catalog), gameplayCatalog: gameplayCatalog);
                engine.ActorSystem.Spawn(source);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();

                var buffEvents = new List<BattleBuffChangedEvent>();
                Action<BattleBuffChangedEvent> onBuffChanged = evt => buffEvents.Add(evt);
                target.Gameplay.Buffs.BuffChanged += onBuffChanged;

                var apply = target.Gameplay.Buffs.Apply(601, source);

                Assert.IsTrue(apply.Success);
                Assert.IsFalse(apply.IsInstant);
                Assert.IsTrue(apply.BuffHandle.IsValid);
                Assert.IsTrue(target.Gameplay.Buffs.TryGetBuff(apply.BuffHandle, out var view));
                Assert.AreEqual(601, view.BuffId);
                Assert.AreEqual(1, view.StackCount);
                Assert.IsTrue(view.IsDebuff);
                Assert.IsFalse(view.CanDispel);
                Assert.AreEqual(1, buffEvents.Count);
                Assert.AreEqual(BattleBuffChangeType.Applied, buffEvents[0].ChangeType);
                Assert.AreEqual(601, buffEvents[0].Buff.BuffId);
                Assert.AreEqual(apply.BuffHandle.RuntimeEffectId, buffEvents[0].Buff.Handle.RuntimeEffectId);
                var stacked = target.Gameplay.Buffs.Apply(601, source);
                Assert.IsTrue(stacked.Success);
                Assert.AreEqual(apply.BuffHandle.RuntimeEffectId, stacked.BuffHandle.RuntimeEffectId);
                Assert.AreEqual(2, buffEvents.Count);
                Assert.AreEqual(BattleBuffChangeType.StackChanged, buffEvents[1].ChangeType);
                Assert.AreEqual(2, buffEvents[1].Buff.StackCount);
                Assert.IsFalse(target.Gameplay.Buffs.Dispel(apply.BuffHandle));
                Assert.IsTrue(target.Gameplay.Buffs.TryGetBuff(apply.BuffHandle, out _));
                Assert.IsTrue(target.Gameplay.Buffs.Remove(apply.BuffHandle));
                Assert.IsFalse(target.Gameplay.Buffs.TryGetBuff(apply.BuffHandle, out _));
                Assert.AreEqual(3, buffEvents.Count);
                Assert.AreEqual(BattleBuffChangeType.Removed, buffEvents[2].ChangeType);

                gameplayCatalog.Buffs[0].CanDispel = true;
                var dispellable = target.Gameplay.Buffs.Apply(601, source);
                Assert.IsTrue(dispellable.Success);
                Assert.IsTrue(target.Gameplay.Buffs.Dispel(dispellable.BuffHandle));
                Assert.IsFalse(target.Gameplay.Buffs.TryGetBuff(dispellable.BuffHandle, out _));
                Assert.AreEqual(5, buffEvents.Count);
                Assert.AreEqual(BattleBuffChangeType.Removed, buffEvents[4].ChangeType);
                target.Gameplay.Buffs.BuffChanged -= onBuffChanged;
            }
            finally
            {
                engine.Dispose();
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(effect);
            }
        }

        [Test]
        public void StunBuff_GrantsStateGatesActionsAndCleansUpOnExpiryOrDispel()
        {
            var engine = new FacadeTestEngine();
            var ability = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            ability.AbilityId = 104;
            var effect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            effect.EffectId = 202;
            effect.DurationPolicy = GameplayEffectDurationPolicy.Duration;
            effect.Duration = 1f;
            effect.GrantedTags.AddTag(CombatGameplayTags.State_Stunned);
            var gameplayCatalog = ScriptableObject.CreateInstance<BattleGameplayCatalog>();
            gameplayCatalog.RegisterSkill(504, ability);
            gameplayCatalog.RegisterBuff(602, effect, isDebuff: true, canDispel: true);

            try
            {
                engine.Initialize();
                var source = CreateActor(1, gameplayCatalog: gameplayCatalog);
                var target = CreateActor(2, ability, gameplayCatalog: gameplayCatalog);
                engine.ActorSystem.Spawn(source);
                engine.ActorSystem.Spawn(target);
                engine.StartBattle();

                var buffEvents = new List<BattleBuffChangedEvent>();
                Action<BattleBuffChangedEvent> onBuffChanged = evt => buffEvents.Add(evt);
                target.Gameplay.Buffs.BuffChanged += onBuffChanged;

                var applied = target.Gameplay.Buffs.Apply(602, source);

                Assert.IsTrue(applied.Success);
                Assert.IsTrue(target.Gameplay.States.Has(BattleState.Stunned));
                Assert.IsFalse(target.Gameplay.States.CanMove());
                Assert.IsFalse(target.Gameplay.States.CanAttack());
                Assert.IsFalse(target.Gameplay.States.CanCastSkill());
                Assert.AreEqual(BattleCastFailureReason.Stunned,
                    target.Gameplay.Skills.TryCast(504, source).Failure);
                Assert.AreEqual(BattleBuffChangeType.Applied, buffEvents[0].ChangeType);

                engine.TickFixed(1f);

                Assert.IsFalse(target.Gameplay.States.Has(BattleState.Stunned));
                Assert.IsTrue(target.Gameplay.States.CanMove());
                Assert.IsTrue(target.Gameplay.States.CanAttack());
                Assert.IsTrue(target.Gameplay.States.CanCastSkill());
                Assert.AreEqual(BattleBuffChangeType.Removed, buffEvents[1].ChangeType);

                var reapplied = target.Gameplay.Buffs.Apply(602, source);
                Assert.IsTrue(reapplied.Success);
                Assert.IsTrue(target.Gameplay.States.Has(BattleState.Stunned));
                Assert.IsTrue(target.Gameplay.Buffs.Dispel(reapplied.BuffHandle));
                Assert.IsFalse(target.Gameplay.States.Has(BattleState.Stunned));
                Assert.IsTrue(target.Gameplay.States.CanCastSkill());
                Assert.AreEqual(BattleBuffChangeType.Removed, buffEvents[3].ChangeType);
                target.Gameplay.Buffs.BuffChanged -= onBuffChanged;
            }
            finally
            {
                engine.Dispose();
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        private static CombatActor CreateActor(
            int id,
            GameplayAbilityDefinition initialAbility = null,
            ICombatAbilityServices services = null,
            IBattleGameplayCatalog gameplayCatalog = null)
        {
            var actor = new CombatActor();
            actor.SetId(id);
            actor.AbilityServices = services;
            actor.GameplayCatalog = gameplayCatalog;
            var abilities = actor.AddComponent<CombatAbilityComponent>();
            if (initialAbility != null)
                abilities.SetInitialAbilities(new[] { initialAbility });
            return actor;
        }

        private sealed class TestAbilityServices : ICombatAbilityServices
        {
            public GameplayDefinitionCatalog AbilityCatalog { get; }
            public ProjectileRuntime ProjectileRuntime => null;

            public TestAbilityServices(GameplayDefinitionCatalog abilityCatalog)
            {
                AbilityCatalog = abilityCatalog;
            }
        }

        private sealed class TestMovementMotor : IMovementMotor
        {
            public bool IsMoving { get; private set; }
            public bool HasArrived => false;
            public float RemainingDistance => IsMoving ? 1f : 0f;
            public int StopCount { get; private set; }

            public void MoveTo(Vector3 destination, float speed)
            {
                IsMoving = true;
            }

            public void Stop()
            {
                StopCount++;
                IsMoving = false;
            }

            public void Teleport(Vector3 position)
            {
                IsMoving = false;
            }
        }

        private sealed class FacadeTestEngine : BattleEngine
        {
            public CombatActorSystem ActorSystem { get; private set; }

            protected override void OnInitialize()
            {
                ActorSystem = Context.AddSystem(new CombatActorSystem());
            }
        }
    }
}
