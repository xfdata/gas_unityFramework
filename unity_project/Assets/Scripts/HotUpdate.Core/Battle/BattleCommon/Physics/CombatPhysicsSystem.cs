using System;
using System.Collections.Generic;
using BattleFoundation;
using UnityEngine;

namespace BattleCommon
{
    public class CombatPhysicsSystem : IBattleSystem
    {
        private IBattleContext _context;
        private readonly Dictionary<int, int> _layerCollisionMap = new Dictionary<int, int>();
        private readonly Collider[] _hits = new Collider[32];

        public event Action<CombatActor, CombatActor, Vector3> OnActorCollision;

        public void Initialize(IBattleContext context) => _context = context;
        public void Start() { }
        public void Update(float deltaTime) { }
        public void LateUpdate(float deltaTime) { }

        public void RegisterLayerCollision(int layerA, int layerB)
        {
            _layerCollisionMap[(layerA << 16) | layerB] = 1;
        }

        public bool CanLayersCollide(int layerA, int layerB)
        {
            return _layerCollisionMap.ContainsKey((layerA << 16) | layerB) ||
                _layerCollisionMap.ContainsKey((layerB << 16) | layerA);
        }

        public int OverlapSphere(Vector3 center, float radius, int layerMask, QueryTriggerInteraction triggerInteraction, Action<Collider[], int> callback)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, _hits, layerMask, triggerInteraction);
            callback?.Invoke(_hits, count);
            return count;
        }

        public void RecordCollision(CombatActor source, CombatActor target, Vector3 point)
        {
            OnActorCollision?.Invoke(source, target, point);
        }

        public static bool RaycastGround(Vector3 origin, out Vector3 hitPoint, float maxDistance = 100f)
        {
            if (Physics.Raycast(origin, Vector3.down, out var hit, maxDistance, LayerMask.GetMask("Default", "Ground")))
            {
                hitPoint = hit.point;
                return true;
            }
            hitPoint = origin;
            return false;
        }

        public void Dispose()
        {
            OnActorCollision = null;
            _layerCollisionMap.Clear();
            _context = null;
        }
    }
}
