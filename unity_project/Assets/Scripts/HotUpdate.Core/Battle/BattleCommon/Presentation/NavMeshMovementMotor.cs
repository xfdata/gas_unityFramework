using BattleFoundation;
using UnityEngine;
using UnityEngine.AI;

namespace BattleCommon
{
    /// <summary>
    /// R3-S8: NavMesh 移动马达实现（L3 表现层）。
    ///
    /// 职责：
    /// - 持有 NavMeshAgent（Unity 引擎组件），驱动其沿 NavMesh 移动。
    /// - 实现 IMovementMotor 接口，供 CombatMovementComponent（L2 逻辑层）通过接口调用。
    /// - Teleport 时通过 CombatActor.Position 写逻辑位置（单向，不反读 Transform）。
    ///
    /// 设计原则：
    /// - L2 逻辑层只通过 IMovementMotor 接口调用，不直接依赖 NavMeshAgent。
    /// - NavMeshAgent 的查找通过 IActorViewResources.GameObject.GetComponentInChildren 完成。
    /// - 逻辑位置真相在 CombatActor.Position，NavMeshAgent 的位置是表现投影。
    /// </summary>
    public sealed class NavMeshMovementMotor : IMovementMotor
    {
        private readonly CombatActor _actor;
        private readonly NavMeshAgent _agent;

        public bool IsMoving { get; private set; }
        public bool HasArrived => _agent != null && _agent.enabled && !_agent.pathPending &&
                                  _agent.remainingDistance <= _agent.stoppingDistance;
        public float RemainingDistance => _agent != null && _agent.enabled ? _agent.remainingDistance : 0f;

        public NavMeshMovementMotor(CombatActor actor, NavMeshAgent agent)
        {
            _actor = actor;
            _agent = agent;
        }

        /// <summary>
        /// R3-S8: 通过 IActorViewResources 查找 NavMeshAgent 的工厂构造。
        /// 上层（Samples/工厂）调用此方法创建马达，避免逻辑层直接 new。
        /// </summary>
        public static NavMeshMovementMotor CreateFromViewBinding(CombatActor actor)
        {
            var gameObject = (actor?.ViewBinding as IActorViewResources)?.GameObject;
            var agent = gameObject?.GetComponentInChildren<NavMeshAgent>();
            return agent != null ? new NavMeshMovementMotor(actor, agent) : null;
        }

        public void MoveTo(Vector3 destination, float speed)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            IsMoving = true;
            _agent.isStopped = false;
            _agent.speed = speed;
            _agent.SetDestination(destination);
        }

        public void Stop()
        {
            IsMoving = false;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.isStopped = true;
        }

        public void Teleport(Vector3 position)
        {
            // R3-S8 修复: 先 Warp（更新 NavMeshAgent 内部状态 + Transform），再写逻辑位置。
            // 原实现先写逻辑位置（触发 SyncTransform 设置 Transform），再 Warp（又设置 Transform），
            // 两次写同一 Transform 可能产生抖动。修复后顺序：Warp → 读实际位置 → 写逻辑位置。
            // Warp 可能 snap 到 NavMesh 上的位置，用 snap 后的位置作为逻辑位置（避免不一致）。
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.Warp(position);
                position = _agent.transform.position; // NavMesh snap 后的实际位置
            }

            // 写逻辑位置（触发 SyncTransform，Transform 已被 Warp 设置，值一致不抖动）
            if (_actor != null)
                _actor.Position = new Float3(position.x, position.y, position.z);
        }
    }
}
