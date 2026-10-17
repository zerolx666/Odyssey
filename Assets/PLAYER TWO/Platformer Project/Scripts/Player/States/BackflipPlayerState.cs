using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
    // 将该类添加到 Unity 的组件菜单中，方便在 Inspector 里挂载
    [AddComponentMenu("PLAYER TWO/Platformer Project/Player/States/Backflip Player State")]
    public class BackflipPlayerState : PlayerState
    {
        /// <summary>
        /// 当玩家进入后空翻状态时调用
        /// </summary>
        protected override void OnEnter(Player player)
        {
            // 设置玩家可用的跳跃次数为 1（避免后空翻过程中还能多次跳跃）
            player.SetJumps(1);

            // 触发玩家的跳跃事件（用于播放动画、音效等）
            player.playerEvents.OnJump.Invoke();

            // 如果配置了“锁定移动方向”，则锁定输入方向
            // 也就是说在后空翻过程中，玩家无法改变移动方向
            if (player.stats.current.backflipLockMovement)
            {
                player.inputs.LockMovementDirection();
            }
        }

        /// <summary>
        /// 当玩家离开后空翻状态时调用（此处为空，不需要额外逻辑）
        /// </summary>
        protected override void OnExit(Player player) { }

        /// <summary>
        /// 每帧更新时调用（后空翻逻辑）
        /// </summary>
        protected override void OnStep(Player player)
        {
            // 应用自定义的重力（比普通跳跃可能更大或更小，用于模拟后空翻弧线）
            player.Gravity(player.stats.current.backflipGravity);

            // 处理后空翻中的速度变化（特殊加速度逻辑）
            player.BackflipAcceleration();

            // 如果已经落地
            if (player.isGrounded)
            {
                // 落地后将水平速度清零
                player.lateralVelocity = Vector3.zero;

                // 状态切换为 Idle（待机状态）
                player.states.Change<IdlePlayerState>();
            }
            // 如果还在空中，并且已经开始下落（y 方向速度小于 0）
            else if (player.verticalVelocity.y < 0)
            {
                // 玩家可以在后空翻下落过程中触发其他技能：
                player.Spin();        // 旋转攻击
                player.AirDive();     // 空中下压攻击
                player.StompAttack(); // 踩踏攻击
                player.Glide();       // 滑翔
            }
        }

        /// <summary>
        /// 当后空翻状态下玩家与其他碰撞体接触时调用
        /// </summary>
        public override void OnContact(Player player, Collider other)
        {
            // 推动可推动的刚体（例如木箱、物体）
            player.PushRigidbody(other);

            // 允许在墙面进行摩擦滑落
            player.WallDrag(other);

            // 允许抓住杆子（类似马里奥64里的抓杆机制）
            player.GrabPole(other);
        }
    }
}