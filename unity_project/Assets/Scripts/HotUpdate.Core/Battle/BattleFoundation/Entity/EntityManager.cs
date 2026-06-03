using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleFoundation
{
    public class EntityManager : Disposable
    {
        private IBattleContext _context;
        private Dictionary<int, BattleEntity> _idToEntity = new Dictionary<int, BattleEntity>();
        private Dictionary<EEntityCamp, List<BattleEntity>> _campToEntities = new Dictionary<EEntityCamp, List<BattleEntity>>();
        private List<BattleEntity> _allEntities = new List<BattleEntity>();
        private int _nextId = 1;

        public IReadOnlyList<BattleEntity> All => _allEntities;
        public int Count => _allEntities.Count;

        public EntityManager(IBattleContext context)
        {
            _context = context;
            foreach (EEntityCamp camp in Enum.GetValues(typeof(EEntityCamp)))
                _campToEntities[camp] = new List<BattleEntity>();
        }

        public int GenerateId()
        {
            while (_idToEntity.ContainsKey(_nextId))
                _nextId++;
            return _nextId++;
        }

        public void AddEntity(BattleEntity entity)
        {
            if (entity == null) return;

            if (entity.Id == 0)
                entity.SetId(GenerateId());

            if (_idToEntity.ContainsKey(entity.Id))
            {
                Debug.LogWarning($"[EntityManager] Entity with Id={entity.Id} already exists, skipping.");
                return;
            }

            entity.Engine = _context.Engine;
            _idToEntity[entity.Id] = entity;
            if (!_campToEntities.TryGetValue(entity.Camp, out var campList))
            {
                campList = new List<BattleEntity>();
                _campToEntities[entity.Camp] = campList;
            }
            campList.Add(entity);
            _allEntities.Add(entity);

            _context.EventBus.Emit(BattleEventIds.EntityCreated, entity);
        }

        public void AddEntityFromPool(BattleEntity entity)
        {
            AddEntity(entity);
        }

        public void RemoveEntity(BattleEntity entity)
        {
            if (entity == null) return;

            if (!_idToEntity.TryGetValue(entity.Id, out var existing) || existing != entity)
                return;

            _idToEntity.Remove(entity.Id);
            if (_campToEntities.TryGetValue(entity.Camp, out var campList))
                campList.Remove(entity);
            _allEntities.Remove(entity);

            _context.EventBus.Emit(BattleEventIds.EntityRemoved, entity);
        }

        public void RemoveEntityFromPool(BattleEntity entity)
        {
            RemoveEntity(entity);
        }

        public BattleEntity GetById(int id)
        {
            _idToEntity.TryGetValue(id, out var entity);
            return entity;
        }

        public IReadOnlyList<BattleEntity> GetByCamp(EEntityCamp camp)
        {
            if (_campToEntities.TryGetValue(camp, out var list))
                return list;
            return Array.Empty<BattleEntity>();
        }

        public List<BattleEntity> GetAliveByCamp(EEntityCamp camp)
        {
            var result = new List<BattleEntity>();
            if (!_campToEntities.TryGetValue(camp, out var list))
                return result;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsAlive)
                    result.Add(list[i]);
            }
            return result;
        }

        public int AliveCountByCamp(EEntityCamp camp)
        {
            int count = 0;
            if (!_campToEntities.TryGetValue(camp, out var list))
                return 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsAlive)
                    count++;
            }
            return count;
        }

        public List<BattleEntity> FindInRange(Vector3 center, float radius, EEntityCamp camp)
        {
            var result = new List<BattleEntity>();
            if (!_campToEntities.TryGetValue(camp, out var list))
                return result;

            float radiusSqr = radius * radius;
            for (int i = 0; i < list.Count; i++)
            {
                var entity = list[i];
                if (!entity.IsAlive) continue;

                float distSqr = (entity.Position - center).sqrMagnitude;
                if (distSqr <= radiusSqr)
                    result.Add(entity);
            }
            return result;
        }

        public BattleEntity FindNearest(Vector3 center, EEntityCamp camp)
        {
            BattleEntity nearest = null;
            float nearestDistSqr = float.MaxValue;

            if (!_campToEntities.TryGetValue(camp, out var list))
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                var entity = list[i];
                if (!entity.IsAlive) continue;

                float distSqr = (entity.Position - center).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestDistSqr = distSqr;
                    nearest = entity;
                }
            }
            return nearest;
        }

        public void Clear()
        {
            foreach (var list in _campToEntities.Values)
                list.Clear();

            _allEntities.Clear();
            _idToEntity.Clear();
        }

        protected override void OnDispose()
        {
            base.OnDispose();

            for (int i = _allEntities.Count - 1; i >= 0; i--)
                _allEntities[i].Dispose();

            Clear();
            _context = null;
        }
    }
}
