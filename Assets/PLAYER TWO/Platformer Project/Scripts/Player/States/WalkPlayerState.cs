using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
    /// <summary>
    /// 玩家行走状态
    /// - 玩家在地面上有输入时进入
    /// - 支持行走、减速、刹车、跳跃、下落、旋转、拾取/投掷、冲刺等操作
    /// </summary>
    [AddComponentMenu("PLAYER TWO/Platformer Project/Player/States/Walk Player State")]
    public class WalkPlayerState : PlayerState
    {
        /// <summary>
        /// 进入行走状态
        /// </summary>
        protected override void OnEnter(Player player) { }

        /// <summary>
        /// 离开行走状态
        /// </summary>
        protected override void OnExit(Player player) { }

        /// <summary>
        /// 每帧更新行走状态逻辑
        /// </summary>
        protected override void OnStep(Player player)
        {
            // 重力处理
            player.Gravity();

            // 保持贴地
            player.SnapToGround();

            // 跳跃处理
            player.Jump();

            // 下落处理
            player.Fall();

            // 空中旋转处理
            player.Spin();

            // 拾取或投掷物体处理
            player.PickAndThrow();

            // 冲刺处理
            player.Dash();

            // 常规坡面力处理
            player.RegularSlopeFactor();

            // 获取玩家输入方向（相机方向）
            var inputDirection = player.inputs.GetMovementCameraDirection();

            if (inputDirection.sqrMagnitude > 0)
            {
                // 输入方向与当前水平速度的点乘，用于判断刹车阈值
                var dot = Vector3.Dot(inputDirection, player.lateralVelocity);
                //刹车阈值 (brakeThreshold) 是一个判断玩家是否急转向的临界值。
                //顺向移动 (dot 高) → 玩家想继续当前方向或微调 → 平滑加速
                //逆向/急转 (dot 低) → 玩家想急停或急转 → 播放刹车动画,可能有滑行效果
                if (dot >= player.stats.current.brakeThreshold)
                {
                    // 超过刹车阈值 → 正常加速与面向方向
                    player.Accelerate(inputDirection);
                    player.FaceDirectionSmooth(player.lateralVelocity);
                }
                else
                {
                    // 低于刹车阈值 → 进入刹车状态
                    player.states.Change<BrakePlayerState>();
                }
            }
            else
            {
                // 没有输入 → 使用摩擦力减速
                player.Friction();

                // 当水平速度为零 → 切换到闲置状态
                if (player.lateralVelocity.sqrMagnitude <= 0)
                {
                    player.states.Change<IdlePlayerState>();
                }
            }

            // 玩家按下蹲或爬行 → 切换到蹲伏状态
            if (player.inputs.GetCrouchAndCraw())
            {
                player.states.Change<CrouchPlayerState>();
            }
        }

        /// <summary>
        /// 碰撞处理
        /// - 行走状态下推动可移动刚体
        /// </summary>
        public override void OnContact(Player player, Collider other)
        {
            player.PushRigidbody(other);
        }
    }
}
