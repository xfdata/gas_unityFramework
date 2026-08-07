using System;
using System.Collections.Generic;
using BattleCommon.Replay;
using BattleFoundation;
using NUnit.Framework;

namespace BattleCommon.Tests
{
    [TestFixture]
    public class CombatReplayAdapterTests
    {
        [Test]
        public void Replay_DynamicActor_IsSpawnedAndDisposedThroughCombatActorSystem()
        {
            var factory = new TestReplayEntityFactory();
            var engine = new ReplayTestEngine(factory);
            engine.Initialize();

            int spawnedCount = 0;
            int despawnedCount = 0;
            DeathReason despawnReason = DeathReason.Killed;
            Action<ActorSpawnedEvent> onSpawned = _ => spawnedCount++;
            Action<ActorDiedEvent> onDied = evt =>
            {
                despawnedCount++;
                despawnReason = evt.Reason;
            };
            engine.Context.EventBus.On(CombatActorEventIds.ActorSpawned, onSpawned);
            engine.Context.EventBus.On(CombatActorEventIds.ActorDied, onDied);

            try
            {
                Assert.IsTrue(engine.StartReplay(CreateDynamicActorRecord()));
                Assert.AreSame(factory.CreatedActor, engine.Context.EntityManager.GetById(42));
                Assert.AreEqual(1, spawnedCount);

                engine.UpdateFromUnity(0.1f);

                Assert.IsNull(engine.Context.EntityManager.GetById(42));
                Assert.IsTrue(factory.CreatedActor.IsDisposed);
                Assert.AreEqual(1, despawnedCount);
                Assert.AreEqual(DeathReason.SceneCleanup, despawnReason);
            }
            finally
            {
                engine.Context.EventBus.Off(CombatActorEventIds.ActorSpawned, onSpawned);
                engine.Context.EventBus.Off(CombatActorEventIds.ActorDied, onDied);
                engine.Dispose();
            }
        }

        private static BattleRecord CreateDynamicActorRecord()
        {
            return new BattleRecord
            {
                Frames = new List<FrameRecordData>
                {
                    new FrameRecordData
                    {
                        FrameIndex = 0,
                        Timestamp = 0f,
                        Entities = new List<EntitySnapshot>
                        {
                            new EntitySnapshot
                            {
                                EntityId = 42,
                                Camp = EEntityCamp.Enemy,
                                EntityType = EEntityType.Monster,
                                IsAlive = true,
                            },
                        },
                    },
                    new FrameRecordData
                    {
                        FrameIndex = 1,
                        Timestamp = 0.1f,
                        Entities = new List<EntitySnapshot>(),
                    },
                },
            };
        }

        private sealed class ReplayTestEngine : BattleEngine
        {
            private readonly ICombatReplayEntityFactory entityFactory;

            public ReplayTestEngine(ICombatReplayEntityFactory entityFactory)
            {
                this.entityFactory = entityFactory;
            }

            protected override void OnInitialize()
            {
                Context.AddSystem(new CombatActorSystem());
                SetReplayAdapter(new CombatReplayAdapter(entityFactory));
            }
        }

        private sealed class TestReplayEntityFactory : ICombatReplayEntityFactory
        {
            public CombatActor CreatedActor { get; private set; }

            public void Capture(CombatActor actor, EntitySnapshot snapshot)
            {
            }

            public CombatActor Create(EntitySnapshot snapshot, BattleContext context)
            {
                CreatedActor = new CombatActor();
                return CreatedActor;
            }

            public void Apply(CombatActor actor, EntitySnapshot snapshot)
            {
            }
        }
    }
}
