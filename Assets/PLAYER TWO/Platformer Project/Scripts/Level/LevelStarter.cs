using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.PlatformerProject
{
    [AddComponentMenu("PLAYER TWO/Platformer Project/Level/Level Starter")]
    public class LevelStarter : Singleton<LevelStarter>
    {
        /// <summary>
        /// 当关卡开始流程完成后触发的事件。
        /// </summary>
        public UnityEvent OnStart;

        /// <summary>
        /// 启用玩家控制的延迟时间（秒）。
        /// </summary>
        public float enablePlayerDelay = 1f;

        // 关卡实例引用
        protected Level m_level => Level.instance;
        // 关卡分数管理实例引用
        protected LevelScore m_score => LevelScore.instance;
        // 关卡暂停管理实例引用
        protected LevelPauser m_pauser => LevelPauser.instance;

        // 画面淡入淡出管理实例引用
        protected Fader m_fader => Fader.instance;

        /// <summary>
        /// 关卡开始的协程流程：
        /// 1. 锁定鼠标光标
        /// 2. 禁用玩家控制和输入
        /// 3. 等待指定的延迟时间
        /// 4. 开始计时（关卡时间计数开始）
        /// 5. 启用玩家控制和输入
        /// 6. 允许关卡暂停
        /// 7. 触发关卡开始事件
        /// </summary>
        protected virtual IEnumerator Routine()
        {
            Game.LockCursor();                              // 锁定鼠标光标（隐藏并锁定在窗口中央）
            m_level.player.controller.enabled = false;    // 禁用玩家控制器，防止操作
            m_level.player.inputs.enabled = false;        // 禁用玩家输入
            yield return new WaitForSeconds(enablePlayerDelay); // 延迟等待，通常用于加载动画或准备阶段
            m_score.stopTime = false;                      // 开始计时，允许时间累加
            m_level.player.controller.enabled = true;     // 启用玩家控制器
            m_level.player.inputs.enabled = true;         // 启用玩家输入
            m_pauser.canPause = true;                       // 允许游戏暂停
            OnStart?.Invoke();                             // 触发关卡开始事件，通知其他系统
        }

        /// <summary>
        /// MonoBehaviour的启动方法，自动开始关卡启动协程。
        /// </summary>
        protected virtual void Start()
        {
            StartCoroutine(Routine());
        }
    }
}