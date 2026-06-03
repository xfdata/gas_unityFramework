using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace BattleSkillSimulation
{
    public sealed class BattleSkillSimulationWorldBuilder
    {
        public struct BuildRequest
        {
            public Transform PlayerRoot;
            public Transform NpcRoot;
            public bool CreatePrimitiveActors;
            public float PlayerMaxHP;
            public float PlayerAttack;
            public float PlayerMoveSpeed;
            public float NpcMaxHP;
            public float NpcDefense;
            public float SkillRange;
            public float SkillRadius;
            public float SkillAttackFactor;
            public float SkillCooldown;
        }

        public BattleSkillSimulationWorld Build(BuildRequest request)
        {
            var definitions = BuildDefinitions(request);
            var services = new RuntimeAbilityServices(definitions.Catalog);
            var engine = new SimulationBattleEngine();
            engine.Initialize();

            var player = CreateActor(
                "Player",
                EEntityCamp.Ally,
                EEntityType.Hero,
                ResolveActorRoot(request.PlayerRoot, "Player", new Vector3(-2f, 0f, 0f), Color.cyan, request.CreatePrimitiveActors),
                services);

            var npc = CreateActor(
                "NPC",
                EEntityCamp.Enemy,
                EEntityType.Monster,
                ResolveActorRoot(request.NpcRoot, "NPC", new Vector3(2f, 0f, 0f), Color.red, request.CreatePrimitiveActors),
                services);

            ConfigureStats(
                player,
                request.PlayerMaxHP,
                request.PlayerAttack * Mathf.Max(0f, request.SkillAttackFactor),
                0f,
                request.PlayerMoveSpeed,
                request.SkillRange);

            ConfigureStats(npc, request.NpcMaxHP, 0f, request.NpcDefense, 0f, 0f);

            player.Get<CombatAbilityComponent>()?.SetInitialAbilities(new[] { definitions.SkillAbility });
            player.Get<BattleSkillSimulationMoveComponent>()?.SetMoveSpeed(request.PlayerMoveSpeed);

            var actorSystem = engine.Context.GetSystem<CombatActorSystem>();
            actorSystem.AddActor(player);
            actorSystem.AddActor(npc);

            engine.StartBattle();

            return new BattleSkillSimulationWorld(
                engine,
                player,
                npc,
                definitions.Catalog,
                definitions.SkillAbility,
                definitions.DamageEffect,
                definitions.DamageExecution,
                request.SkillCooldown);
        }

        private static RuntimeDefinitions BuildDefinitions(BuildRequest request)
        {
            var catalog = ScriptableObject.CreateInstance<GameplayDefinitionCatalog>();
            catalog.name = "RuntimeBattleSkillSimulationCatalog";

            var damageExecution = ScriptableObject.CreateInstance<CombatDamageExecution>();
            damageExecution.name = "RuntimeSkillDamageExecution";

            var damageEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            damageEffect.name = "RuntimeSkillDamageEffect";
            damageEffect.EffectId = 9001;
            damageEffect.EffectTag = CombatGameplayTags.Cue_Hit;
            damageEffect.DurationPolicy = GameplayEffectDurationPolicy.Instant;
            damageEffect.Executions.Add(damageExecution);

            var skillAbility = ScriptableObject.CreateInstance<MeleeAttackAbilityDefinition>();
            skillAbility.name = "RuntimeMeleeSkill";
            skillAbility.AbilityId = CombatAbilityIds.Skill;
            skillAbility.AbilityTag = CombatGameplayTags.Ability_Skill;
            skillAbility.HitDefinition = new MeleeHitDefinition
            {
                MeleeDefinitionId = 9001,
                Range = request.SkillRange,
                Radius = request.SkillRadius,
                MaxTargets = 1,
            };
            skillAbility.DamageEffect = damageEffect;

            catalog.RegisterEffect(damageEffect);
            catalog.RegisterAbility(skillAbility);

            return new RuntimeDefinitions
            {
                Catalog = catalog,
                SkillAbility = skillAbility,
                DamageEffect = damageEffect,
                DamageExecution = damageExecution,
            };
        }

        private static SimulationActor CreateActor(
            string actorName,
            EEntityCamp camp,
            EEntityType type,
            ActorRoot actorRoot,
            ICombatAbilityServices services)
        {
            var actor = new SimulationActor
            {
                Name = actorName,
                Transform = actorRoot.Transform,
                GameObject = actorRoot.DestroyWithActor ? actorRoot.Transform.gameObject : null,
                AbilityServices = services,
            };

            actor.SetCamp(camp);
            actor.SetEntityType(type);
            actor.AddComponent<CombatAttributeComponent>();
            actor.AddComponent<CombatHealthComponent>();
            actor.AddComponent<CombatAbilityComponent>();
            actor.AddComponent<BattleSkillSimulationMoveComponent>();
            return actor;
        }

        private static ActorRoot ResolveActorRoot(
            Transform suppliedRoot,
            string actorName,
            Vector3 position,
            Color color,
            bool createPrimitiveActors)
        {
            if (suppliedRoot != null)
            {
                return new ActorRoot
                {
                    Transform = suppliedRoot,
                    DestroyWithActor = false,
                };
            }

            if (!createPrimitiveActors)
            {
                var empty = new GameObject(actorName);
                empty.transform.position = position;
                return new ActorRoot
                {
                    Transform = empty.transform,
                    DestroyWithActor = true,
                };
            }

            var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            primitive.name = actorName;
            primitive.transform.position = position;

            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;

            return new ActorRoot
            {
                Transform = primitive.transform,
                DestroyWithActor = true,
            };
        }

        private static void ConfigureStats(
            CombatActor actor,
            float maxHP,
            float attack,
            float defense,
            float moveSpeed,
            float attackRange)
        {
            var attributes = actor.Get<CombatAttributeComponent>();
            attributes.MaxHP = maxHP;
            attributes.HP = maxHP;
            attributes.Attack = attack;
            attributes.Defense = defense;
            attributes.MoveSpeed = moveSpeed;
            attributes.AttackRange = attackRange;
            attributes.AttackInterval = 1f;
            attributes.CritRate = 0f;
            attributes.CritDamage = 1.5f;
            attributes.DamageReduce = 0f;
        }

        private struct RuntimeDefinitions
        {
            public GameplayDefinitionCatalog Catalog;
            public MeleeAttackAbilityDefinition SkillAbility;
            public GameplayEffectDefinition DamageEffect;
            public CombatDamageExecution DamageExecution;
        }

        private struct ActorRoot
        {
            public Transform Transform;
            public bool DestroyWithActor;
        }

        private sealed class RuntimeAbilityServices : ICombatAbilityServices
        {
            public RuntimeAbilityServices(GameplayDefinitionCatalog catalog)
            {
                AbilityCatalog = catalog;
            }

            public GameplayDefinitionCatalog AbilityCatalog { get; }
            public IGameplayCueManager GameplayCueManager => null;
            public ProjectileRuntime ProjectileRuntime => null;
        }
    }
}
