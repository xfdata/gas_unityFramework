using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 玩家移动系统 — 处理玩家输入并驱动CombatMovementComponent。
    /// 
    /// 支持两种输入模式：
    /// 1. ClickToMove：点击地面/NavMesh → MoveTo(target)
    /// 2. Joystick：摇杆方向 → 持续移动
    /// 
    /// 性能：单实例IBattleSystem，统一Update
    /// </summary>
    public class TDPlayerMovementSystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;

        /// <summary>
        /// 玩家Actor引用（由外部设置）
        /// </summary>
        public TDPlayerActor Player { get; set; }

        /// <summary>
        /// 移动模式
        /// </summary>
        public ETDPlayerMoveMode MoveMode { get; set; } = ETDPlayerMoveMode.ClickToMove;

        // ClickToMove 状态
        private Vector3 _clickDestination;
        private bool _hasClickDestination;

        // Joystick 状态
        private Vector2 _joystickInput;
        private bool _hasJoystickInput;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;
        }

        public void Start() { }

        /// <summary>
        /// 设置点击移动目标
        /// </summary>
        public void SetClickDestination(Vector3 worldPosition)
        {
            if (Player == null || !Player.IsAlive) return;
            _clickDestination = worldPosition;
            _hasClickDestination = true;
        }

        /// <summary>
        /// 设置摇杆输入（-1到1的归一化值）
        /// </summary>
        public void SetJoystickInput(Vector2 input)
        {
            _joystickInput = Vector2.ClampMagnitude(input, 1f);
            _hasJoystickInput = _joystickInput.sqrMagnitude > 0.001f;
        }

        public void Update(float deltaTime)
        {
            if (Player == null || !Player.IsAlive || deltaTime <= 0f) return;

            switch (MoveMode)
            {
                case ETDPlayerMoveMode.ClickToMove:
                    UpdateClickToMove();
                    break;
                case ETDPlayerMoveMode.Joystick:
                    UpdateJoystickMove(deltaTime);
                    break;
            }
        }

        public void LateUpdate(float deltaTime) { }

        private void UpdateClickToMove()
        {
            if (!_hasClickDestination) return;

            // 检查是否到达目的地
            float distSqr = (Player.Position - _clickDestination).sqrMagnitude;
            if (distSqr <= 0.1f)
            {
                Player.StopMove();
                _hasClickDestination = false;
                return;
            }

            Player.MoveTo(_clickDestination);
        }

        private void UpdateJoystickMove(float deltaTime)
        {
            if (!_hasJoystickInput)
            {
                Player.StopMove();
                return;
            }

            // 摇杆方向转世界坐标偏移
            var attributes = Player.Get<BattleCommon.CombatAttributeComponent>();
            float speed = attributes?.MoveSpeed ?? 5f;
            Vector3 moveDir = new Vector3(_joystickInput.x, 0f, _joystickInput.y).normalized;
            Vector3 destination = Player.Position + moveDir * speed * deltaTime;

            Player.MoveTo(destination);
        }

        public void Dispose()
        {
            Player?.StopMove();
            Player = null;
            _context = null;
            _entityManager = null;
        }
    }

    /// <summary>
    /// 玩家移动模式
    /// </summary>
    public enum ETDPlayerMoveMode
    {
        ClickToMove,
        Joystick,
    }
}
