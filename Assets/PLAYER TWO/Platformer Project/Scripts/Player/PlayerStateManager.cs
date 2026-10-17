using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
    /// <summary>
    /// 玩家状态管理器 (Player State Manager)。
    /// 负责管理玩家可用的状态（如移动、跳跃、攻击、旋转等），
    /// 并通过 EntityStateManager 基类实现状态机的统一管理。
    /// 
    /// 注意：
    /// - 必须挂载在 Player 对象上，因为它需要管理 Player 的状态。
    /// - 内部通过 states 数组存储状态类名，并在运行时转换为实际的状态对象。
    /// </summary>
    [RequireComponent(typeof(Player))] // 强制要求该组件所在的物体必须挂有 Player 组件
    [AddComponentMenu("PLAYER TWO/Platformer Project/Player/Player State Manager")] // Unity 编辑器菜单入口
    public class PlayerStateManager : EntityStateManager<Player>
    {
        /// <summary>
        /// 玩家状态类的字符串数组。
        /// 使用 ClassTypeName 特性，让 Unity Inspector 面板中可以通过下拉/输入选择继承自 PlayerState 的类。
        /// 
        /// 示例：
        /// states = { "IdlePlayerState", "RunPlayerState", "JumpPlayerState", "SpinPlayerState" }
        /// </summary>
        [ClassTypeName(typeof(PlayerState))]
        public string[] states;

        /// <summary>
        /// 重写基类方法，获取该玩家的状态列表。
        /// 会将 states 中的字符串类名数组转换为真正的状态对象列表。
        /// </summary>
        /// <returns>返回一个包含所有状态的 List<EntityState<Player>>。</returns>
        protected override List<EntityState<Player>> GetStateList()
        {
            // 调用 PlayerState 的辅助方法，把字符串数组转换为状态对象集合
            // 例如： "RunPlayerState" → new RunPlayerState()
            return PlayerState.CreateListFromStringArray(states);
        }
    }
}



