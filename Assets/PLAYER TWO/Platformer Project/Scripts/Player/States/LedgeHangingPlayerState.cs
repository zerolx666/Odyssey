using UnityEngine;
using System.Collections;

namespace PLAYERTWO.PlatformerProject
{
    /// <summary>
    /// 玩家抓悬挂状态
    /// - 当玩家挂在悬崖边缘时进入该状态
    /// - 处理悬挂位置、左右移动、跳跃、释放悬挂、爬升等逻辑
    /// </summary>
    [AddComponentMenu("PLAYER TWO/Platformer Project/Player/States/Ledge Hanging Player State")]
    public class LedgeHangingPlayerState : PlayerState
    {
        // 是否保持父对象（用于爬升时防止重置父对象）
        protected bool m_keepParent;

        // 延迟清理父对象的协程引用
        protected Coroutine m_clearParentRoutine;

        // 清理父对象的延迟时间
        protected const float k_clearParentDelay = 0.25f;

        /// <summary>
        /// 进入抓悬挂状态
        /// - 调整玩家皮肤偏移
        /// - 重置跳跃、空中旋转和空中冲刺次数
        /// </summary>
        protected override void OnEnter(Player player)
        {
            if (m_clearParentRoutine != null)
                player.StopCoroutine(m_clearParentRoutine); // 停止可能存在的清理协程

            m_keepParent = false;
            player.skin.position += player.transform.rotation * player.stats.current.ledgeHangingSkinOffset;
            player.ResetJumps();       // 重置跳跃次数
            player.ResetAirSpins();    // 重置空中旋转次数
            player.ResetAirDash();     // 重置空中冲刺次数
        }

        /// <summary>
        /// 离开抓悬挂状态
        /// - 启动延迟协程清理父对象
        /// - 还原皮肤偏移
        /// </summary>
        protected override void OnExit(Player player)
        {
            m_clearParentRoutine = player.StartCoroutine(ClearParentRoutine(player));
            player.skin.position -= player.transform.rotation * player.stats.current.ledgeHangingSkinOffset;
        }

        /// <summary>
        /// 每帧更新抓悬挂状态逻辑
        /// - 检测悬挂顶端和侧面位置
        /// - 处理玩家左右移动、释放悬挂、跳跃、爬升
        /// - 如果悬挂条件失效，切换到下落状态
        /// </summary>
        protected override void OnStep(Player player)
        {
            // 顶端检测参数
            var ledgeTopMaxDistance = player.radius + player.stats.current.ledgeMaxForwardDistance;
            var ledgeTopHeightOffset = player.height * 0.5f + player.stats.current.ledgeMaxDownwardDistance;
            var topOrigin = player.position + Vector3.up * ledgeTopHeightOffset + player.transform.forward * ledgeTopMaxDistance;

            // 侧面检测参数
            var sideOrigin = player.position + Vector3.up * player.height * 0.5f + Vector3.down * player.stats.current.ledgeSideHeightOffset;
            var rayDistance = player.radius + player.stats.current.ledgeSideMaxDistance;
            var rayRadius = player.stats.current.ledgeSideCollisionRadius;

            // 侧面检测和顶端检测
            if (Physics.SphereCast(sideOrigin, rayRadius, player.transform.forward, out var sideHit,
                rayDistance, player.stats.current.ledgeHangingLayers, QueryTriggerInteraction.Ignore) &&
                Physics.Raycast(topOrigin, Vector3.down, out var topHit, player.height,
                player.stats.current.ledgeHangingLayers, QueryTriggerInteraction.Ignore))
            {
                var inputDirection = player.inputs.GetMovementDirection();
                var ledgeSideOrigin = sideOrigin + player.transform.right * Mathf.Sign(inputDirection.x) * player.radius;
                var ledgeHeight = topHit.point.y - player.height * 0.5f;
                var sideForward = -new Vector3(sideHit.normal.x, 0, sideHit.normal.z).normalized;
                var destinationHeight = player.height * 0.5f + Physics.defaultContactOffset;
                var climbDestination = topHit.point + Vector3.up * destinationHeight + player.transform.forward * player.radius;

                // 面向悬挂墙方向
                player.FaceDirection(sideForward);

                // 左右移动逻辑
                if (Physics.Raycast(ledgeSideOrigin, sideForward, rayDistance,
                    player.stats.current.ledgeHangingLayers, QueryTriggerInteraction.Ignore))
                {
                    player.lateralVelocity = player.transform.right * inputDirection.x * player.stats.current.ledgeMovementSpeed;
                }
                else
                {
                    player.lateralVelocity = Vector3.zero;
                }

                // 固定玩家悬挂位置
                player.transform.position = new Vector3(sideHit.point.x, ledgeHeight, sideHit.point.z) - sideForward * player.radius - player.center;

                // 检测释放悬挂（向下）
                if (player.inputs.GetReleaseLedgeDown())
                {
                    player.FaceDirection(-sideForward);
                    player.states.Change<FallPlayerState>();
                }
                // 检测跳跃
                else if (player.inputs.GetJumpDown())
                {
                    player.Jump(player.stats.current.maxJumpHeight);
                    player.states.Change<FallPlayerState>();
                }
                // 检测爬升
                else if (inputDirection.z > 0 && player.stats.current.canClimbLedges &&
                        ((1 << topHit.collider.gameObject.layer) & player.stats.current.ledgeClimbingLayers) != 0 &&
                        player.FitsIntoPosition(climbDestination))
                {
                    m_keepParent = true;
                    player.states.Change<LedgeClimbingPlayerState>();
                    player.playerEvents.OnLedgeClimbing?.Invoke();
                }
            }
            else
            {
                // 悬挂条件不满足 → 下落
                player.states.Change<FallPlayerState>();
            }
        }

        /// <summary>
        /// 碰撞处理逻辑
        /// - 抓悬挂状态通常不处理碰撞
        /// </summary>
        public override void OnContact(Player player, Collider other) { }

        /// <summary>
        /// 延迟清理父对象协程
        /// - 如果爬升保持父对象则跳过清理
        /// </summary>
        protected virtual IEnumerator ClearParentRoutine(Player player)
        {
            if (m_keepParent) yield break;

            yield return new WaitForSeconds(k_clearParentDelay);

            player.transform.parent = null;
        }
    }
}
