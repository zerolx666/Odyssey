using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	[RequireComponent(typeof(Player))]
	[AddComponentMenu("PLAYER TWO/Platformer Project/Player/Player Level Pause")]
	public class PlayerLevelPause : MonoBehaviour
	{
		protected Player m_player;
		protected LevelPauser m_pauser;

		protected virtual void Start()
		{
			m_player = GetComponent<Player>();
			m_pauser = LevelPauser.instance;
		}

		protected virtual void Update()
		{
			// 检查玩家是否按下暂停按键
			if (m_player.inputs.GetPauseDown())
			{
				// 获取当前的暂停状态
				var value = m_pauser.paused;
				// 切换暂停状态（如果暂停则继续，如果继续则暂停）
				m_pauser.Pause(!value);
			}
		}
	}
}
