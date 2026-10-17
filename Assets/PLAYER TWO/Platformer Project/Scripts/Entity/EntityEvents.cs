using System;
using UnityEngine.Events;

namespace PLAYERTWO.PlatformerProject
{
    /// <summary>
    /// 实体事件类  
    /// 这个类定义了与实体（角色或物体）相关的各种事件，
    /// 通过 UnityEvent 形式暴露给编辑器和代码调用，
    /// 方便在特定条件触发时执行对应的逻辑（例如播放音效、触发动画等）。
    /// </summary>
    [Serializable]
    public class EntityEvents
    {
        /// <summary>
        /// 当实体落地时触发的事件。  
        /// </summary>
        public UnityEvent OnGroundEnter;

        /// <summary>
        /// 当实体离开地面时触发的事件。  
        /// </summary>
        public UnityEvent OnGroundExit;

        /// <summary>
        /// 当实体进入“轨道”（Rails）时触发的事件。  
        /// </summary>
        public UnityEvent OnRailsEnter;

        /// <summary>
        /// 当实体离开“轨道”时触发的事件。  
        /// </summary>
        public UnityEvent OnRailsExit;
    }
}