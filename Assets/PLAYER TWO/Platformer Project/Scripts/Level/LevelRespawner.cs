using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.PlatformerProject
{
	[AddComponentMenu("PLAYER TWO/Platformer Project/Level/Level Respawner")]
	public class LevelRespawner : Singleton<LevelRespawner>
	{
		/// <summary>
		/// 在复活流程结束后调用的事件。
		/// </summary>
		public UnityEvent OnRespawn;

		/// <summary>
		/// 在游戏结束（Game Over）流程结束后调用的事件。
		/// </summary>
		public UnityEvent OnGameOver;

		// 复活淡出延迟时间
		public float respawnFadeOutDelay = 1f;
		// 复活淡入延迟时间
		public float respawnFadeInDelay = 0.5f;
		// 游戏结束淡出延迟时间
		public float gameOverFadeOutDelay = 5f;
		// 重启淡出延迟时间
		public float restartFadeOutDelay = 0.5f;

		// 存储场景中的所有玩家摄像机列表
		protected List<PlayerCamera> m_cameras;

		// 快捷访问当前关卡实例
		protected Level m_level => Level.instance;
		// 快捷访问当前关卡分数管理实例
		protected LevelScore m_score => LevelScore.instance;
		// 快捷访问暂停管理实例
		protected LevelPauser m_pauser => LevelPauser.instance;
		// 快捷访问游戏管理实例
		protected Game m_game => Game.instance;
		// 快捷访问画面淡入淡出管理实例
		protected Fader m_fader => Fader.instance;

		/// <summary>
		/// 复活的协程流程。
		/// 如果 consumeRetries 为真，则扣除一次重试机会。
		/// 重置玩家状态，重置分数，重置摄像机，触发复活事件，播放淡入动画，恢复玩家控制和允许暂停。
		/// </summary>
		protected virtual IEnumerator RespawnRoutine(bool consumeRetries)
		{
			// 根据调用方传入的标记，决定本次复活是否扣除一次重试机会。
			if (consumeRetries)
			{
				m_game.retries--;
			}

			// 执行玩家自身的复活逻辑，恢复其位置和状态。
			m_level.player.Respawn();
			// 清空当前金币计数。
			m_score.coins = 0;
			// 重置所有玩家摄像机，使视角与复活后的玩家同步。
			ResetCameras();
			// 通知监听者处理复活后的逻辑；事件为空时不调用。
			OnRespawn?.Invoke();//无方法

			// 等待配置的淡入延迟，再开始显示复活后的画面。
			yield return new WaitForSeconds(respawnFadeInDelay);

			// 播放淡入动画，完成后再允许暂停并恢复玩家输入。
			m_fader.FadeIn(() =>
			{
				m_pauser.canPause = true;
				m_level.player.inputs.enabled = true;
			});
		}

		/// <summary>
		/// 游戏结束的协程流程。
		/// 停止计时，等待游戏结束淡出延迟，重新加载场景，触发游戏结束事件。
		/// </summary>
		protected virtual IEnumerator GameOverRoutine()
		{
			m_score.stopTime = true;
			yield return new WaitForSeconds(gameOverFadeOutDelay);
			GameLoader.instance.Reload();
			OnGameOver?.Invoke();
		}

		/// <summary>
		/// 重新开始关卡的协程流程。
		/// 取消暂停，禁止暂停，禁用玩家输入，等待重启淡出延迟，重新加载当前场景。
		/// </summary>
		protected virtual IEnumerator RestartRoutine()
		{
			m_pauser.Pause(false);//意思是取消暂停
			m_pauser.canPause = false;
			m_level.player.inputs.enabled = false;//禁止玩家的输入
			yield return new WaitForSeconds(restartFadeOutDelay);//延时
			GameLoader.instance.Reload();//加载场景
		}

		/// <summary>
		/// 复活或游戏结束的统一流程。
		/// 如果消耗重试次数且重试次数为0，则进入游戏结束流程。
		/// 否则先等待复活淡出延迟，然后播放淡出动画，之后进入复活流程。
		/// </summary>
		protected virtual IEnumerator Routine(bool consumeRetries)
		{
			// 先解除暂停，确保后续协程中的等待和过渡动画能够正常进行。
			m_pauser.Pause(false);
			// 过渡期间禁止再次暂停，并禁用玩家输入，避免操作干扰复活流程。
			m_pauser.canPause = false;
			m_level.player.inputs.enabled = false;
			// 本次复活需要消耗重试次数且已无剩余次数时，进入游戏结束流程。
			if (consumeRetries && m_game.retries == 0)
			{
				StartCoroutine(GameOverRoutine());
				// 结束当前协程，不再执行下面的淡出和复活逻辑。
				yield break;
			}

			// 等待配置的复活淡出延迟，再开始画面过渡。
			yield return new WaitForSeconds(respawnFadeOutDelay);
			
			// 淡出完成后启动复活协程，并传入是否消耗重试次数的标记。
			m_fader.FadeOut(() => StartCoroutine(RespawnRoutine(consumeRetries)));
		}

		/// <summary>
		/// 重置所有摄像机状态，调用每个摄像机的 Reset 方法。
		/// </summary>
		protected virtual void ResetCameras()
		{
			foreach (var camera in m_cameras)
			{
				camera.Reset();
			}
		}

		/// <summary>
		/// 根据重试次数调用复活流程或游戏结束流程。
		/// 参数 consumeRetries 指示是否扣除重试次数。
		/// </summary>
		public virtual void Respawn(bool consumeRetries)//true
		{
			StopAllCoroutines();
			StartCoroutine(Routine(consumeRetries));//consumeRetries=true
		}

		/// <summary>
		/// UI按钮重开的执行的功能，重新开始当前关卡，重新加载当前场景。
		/// </summary>
		public virtual void Restart()
		{
			StopAllCoroutines();//停止所有正在运行的协程，确保之前的任何复活或游戏结束流程都被中断
			StartCoroutine(RestartRoutine());
		}

		/// <summary>
		/// 初始化方法，查找场景中所有玩家摄像机，监听玩家死亡事件自动触发复活。
		/// </summary>
		protected virtual void Start()
		{
			m_cameras = new List<PlayerCamera>(FindObjectsOfType<PlayerCamera>());
			m_level.player.playerEvents.OnDie.AddListener(() => Respawn(true));
		}
	}
}
