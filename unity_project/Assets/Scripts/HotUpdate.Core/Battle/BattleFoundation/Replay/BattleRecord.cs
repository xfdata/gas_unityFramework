using System;
using System.Collections.Generic;
using Framework;
using UnityEngine;

namespace BattleFoundation
{
    public interface IBattleReplayAdapter
    {
        void CaptureEntity(BattleEntity entity, EntitySnapshot snapshot);
        BattleEntity CreateEntity(EntitySnapshot snapshot, BattleContext context);
        void ApplyEntity(BattleEntity entity, EntitySnapshot snapshot);
        void RemoveEntity(BattleEntity entity, BattleContext context);
    }

    [Serializable]
    public class FrameRecordData
    {
        public int FrameIndex;
        public float Timestamp;
        public List<EntitySnapshot> Entities = new List<EntitySnapshot>();

        public static FrameRecordData Create(
            int frameIndex,
            float timestamp,
            BattleContext context,
            IBattleReplayAdapter adapter = null)
        {
            var data = new FrameRecordData
            {
                FrameIndex = frameIndex,
                Timestamp = timestamp,
            };

            if (context?.EntityManager == null)
                return data;

            var entities = context.EntityManager.All;
            for (int i = 0; i < entities.Count; i++)
            {
                var snapshot = EntitySnapshot.Capture(entities[i]);
                adapter?.CaptureEntity(entities[i], snapshot);
                data.Entities.Add(snapshot);
            }

            return data;
        }
    }

    [Serializable]
    public class EntitySnapshot
    {
        public int EntityId;
        public EEntityCamp Camp;
        public EEntityType EntityType;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float RotX;
        public float RotY;
        public float RotZ;
        public float RotW;
        public bool IsAlive;
        public string SpawnKey;
        public string PayloadJson;
        public AttributeSnapshot FoundationAttributes;

        public Vector3 Position => new Vector3(PosX, PosY, PosZ);
        public Quaternion Rotation => new Quaternion(RotX, RotY, RotZ, RotW);

        public static EntitySnapshot Capture(BattleEntity entity)
        {
            return new EntitySnapshot
            {
                EntityId = entity.Id,
                Camp = entity.Camp,
                EntityType = entity.EntityType,
                PosX = entity.Position.x,
                PosY = entity.Position.y,
                PosZ = entity.Position.z,
                RotX = entity.Rotation.x,
                RotY = entity.Rotation.y,
                RotZ = entity.Rotation.z,
                RotW = entity.Rotation.w,
                IsAlive = entity.IsAlive,
                FoundationAttributes = entity.Get<BattleAttributeSet>()?.Snapshot(),
            };
        }

        public void ApplyBaseState(BattleEntity entity)
        {
            entity.Position = Position;
            entity.Rotation = Rotation;
            entity.IsAlive = IsAlive;
            entity.Get<BattleAttributeSet>()?.RestoreSnapshot(FoundationAttributes);
        }
    }

    [Serializable]
    public class BattleRecord
    {
        public int BattleId;
        public string BattleType;
        public EBattleTickMode TickMode;
        public float FixedDeltaTime;
        public float TimeScale;
        public int RandomSeed;
        public List<FrameRecordData> Frames = new List<FrameRecordData>();
        public EBattleResult FinalResult;
        public float TotalDuration;

        public int FrameCount => Frames?.Count ?? 0;
    }

    public class BattleRecorder : Disposable
    {
        private BattleEngine _engine;
        private BattleRecord _record;

        public bool IsRecording { get; private set; }
        public BattleRecord Record => _record;

        public void Initialize(BattleEngine engine)
        {
            _engine = engine;
        }

        public void StartRecording()
        {
            var settings = _engine?.Settings;
            _record = new BattleRecord
            {
                TickMode = settings?.TickMode ?? EBattleTickMode.RealTime,
                FixedDeltaTime = settings?.FrameSyncStep ?? 0f,
                TimeScale = _engine?.TimeScale ?? 1f,
                RandomSeed = _engine?.RandomSeed ?? 0,
            };
            IsRecording = true;
        }

        public void RecordFrame(FrameRecordData frame)
        {
            using (new AutoProfiler("BattleFoundation.BattleRecorder.RecordFrame"))
            {
                if (IsRecording && frame != null)
                    _record.Frames.Add(frame);
            }
        }

        public void StopRecording(EBattleResult result)
        {
            if (_record == null || !IsRecording) return;

            IsRecording = false;
            _record.TotalDuration = _engine?.ElapsedTime ?? 0f;
            _record.FinalResult = result;
        }

        public BattleRecord GetRecord()
        {
            if (_record != null && IsRecording)
                _record.TotalDuration = _engine?.ElapsedTime ?? _record.TotalDuration;
            return _record;
        }

        protected override void OnDispose()
        {
            StopRecording(EBattleResult.None);
            _record = null;
            _engine = null;
            base.OnDispose();
        }
    }

    public class BattlePlayback : Disposable
    {
        private BattleRecord _record;
        private BattleContext _context;
        private IBattleReplayAdapter _adapter;
        private Action<EBattleResult> _onCompleted;
        private readonly HashSet<int> _frameEntityIds = new HashSet<int>();
        private readonly List<BattleEntity> _removeBuffer = new List<BattleEntity>();
        private int _nextFrameIndex;
        private float _time;

        public bool IsPlaying { get; private set; }

        public void Initialize(
            BattleRecord record,
            BattleContext context,
            IBattleReplayAdapter adapter,
            Action<EBattleResult> onCompleted)
        {
            _record = record;
            _context = context;
            _adapter = adapter;
            _onCompleted = onCompleted;
        }

        public void Start()
        {
            _nextFrameIndex = 0;
            _time = 0f;
            IsPlaying = true;
            ApplyDueFrames();
        }

        public void Update(float deltaTime)
        {
            using (new AutoProfiler("BattleFoundation.BattlePlayback.Update"))
            {
                if (!IsPlaying) return;
                _time += Mathf.Max(0f, deltaTime);
                ApplyDueFrames();
            }
        }

        public void Stop()
        {
            IsPlaying = false;
        }

        private void ApplyDueFrames()
        {
            while (_nextFrameIndex < _record.Frames.Count &&
                   _record.Frames[_nextFrameIndex].Timestamp <= _time)
            {
                ApplyFrame(_record.Frames[_nextFrameIndex]);
                _nextFrameIndex++;
            }

            if (_nextFrameIndex < _record.Frames.Count)
                return;

            IsPlaying = false;
            _onCompleted?.Invoke(_record.FinalResult);
        }

        private void ApplyFrame(FrameRecordData frame)
        {
            _frameEntityIds.Clear();
            if (frame.Entities != null)
            {
                for (int i = 0; i < frame.Entities.Count; i++)
                {
                    var snapshot = frame.Entities[i];
                    _frameEntityIds.Add(snapshot.EntityId);
                    var entity = _context.EntityManager.GetById(snapshot.EntityId);
                    if (entity == null)
                    {
                        entity = _adapter?.CreateEntity(snapshot, _context);
                        if (entity != null && _context.EntityManager.GetById(snapshot.EntityId) == null)
                        {
                            entity.SetId(snapshot.EntityId);
                            entity.SetCamp(snapshot.Camp);
                            entity.SetEntityType(snapshot.EntityType);
                            _context.EntityManager.AddEntity(entity);
                        }
                    }

                    if (entity == null) continue;
                    snapshot.ApplyBaseState(entity);
                    _adapter?.ApplyEntity(entity, snapshot);
                }
            }

            _removeBuffer.Clear();
            var entities = _context.EntityManager.All;
            for (int i = 0; i < entities.Count; i++)
            {
                if (!_frameEntityIds.Contains(entities[i].Id))
                    _removeBuffer.Add(entities[i]);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                if (_adapter != null)
                    _adapter.RemoveEntity(_removeBuffer[i], _context);
                else
                    _context.EntityManager.RemoveEntity(_removeBuffer[i]);
            }
        }

        protected override void OnDispose()
        {
            Stop();
            _record = null;
            _context = null;
            _adapter = null;
            _onCompleted = null;
            _frameEntityIds.Clear();
            _removeBuffer.Clear();
            base.OnDispose();
        }
    }
}
