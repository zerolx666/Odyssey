using UnityEngine;
using UnityEngine.InputSystem;

namespace PLAYERTWO.PlatformerProject
{
	/// <summary>
	/// 玩家输入管理器  
	/// 负责缓存并提供各种输入的访问方法（移动、跳跃、冲刺等）。  
	/// 基于 Unity Input System 实现。
	/// </summary>
	[AddComponentMenu("PLAYER TWO/Platformer Project/Player/Player Input Manager")]
	public class PlayerInputManager : MonoBehaviour
	{
		// 输入动作资源（在 Input System 中配置的 InputActionAsset）
		public InputActionAsset actions;

		// 输入动作缓存
		protected InputAction m_movement;
		protected InputAction m_run;
		protected InputAction m_jump;
		protected InputAction m_dive;
		protected InputAction m_spin;
		protected InputAction m_pickAndDrop;
		protected InputAction m_crouch;
		protected InputAction m_airDive;
		protected InputAction m_stomp;
		protected InputAction m_releaseLedge;
		protected InputAction m_pause;
		protected InputAction m_look;
		protected InputAction m_glide;
		protected InputAction m_dash;
		protected InputAction m_grindBrake;

		// 主摄像机引用，用于计算相对移动方向
		protected Camera m_camera;

		// 用于锁定移动方向的时间戳（当前时间小于此值时，禁止移动输入）
		protected float m_movementDirectionUnlockTime;
		// 最近一次按下跳跃的时间，用于跳跃缓冲
		protected float? m_lastJumpTime;

		// 常量：鼠标设备名称
		protected const string k_mouseDeviceName = "Mouse";
		// 常量：跳跃缓冲时长（单位：秒）
		protected const float k_jumpBuffer = 0.15f;

		/// <summary>
		/// 缓存 InputActionAsset 中的各类输入动作，避免每次查询时查找开销。
		/// </summary>
		protected virtual void CacheActions()
		{
			m_movement = actions["Movement"];
			m_run = actions["Run"];
			m_jump = actions["Jump"];
			m_dive = actions["Dive"];
			m_spin = actions["Spin"];
			m_pickAndDrop = actions["PickAndDrop"];
			m_crouch = actions["Crouch"];
			m_airDive = actions["AirDive"];
			m_stomp = actions["Stomp"];
			m_releaseLedge = actions["ReleaseLedge"];
			m_pause = actions["Pause"];
			m_look = actions["Look"];
			m_glide = actions["Glide"];
			m_dash = actions["Dash"];
			m_grindBrake = actions["Grind Brake"];
		}

		/// <summary>
		/// 获取移动方向输入（带十字型死区判断）
		/// 如果在锁定时间内，则返回 Vector3.zero
		/// </summary>
		public virtual Vector3 GetMovementDirection()
		{
			if (Time.time < m_movementDirectionUnlockTime) return Vector3.zero;

			var value = m_movement.ReadValue<Vector2>();
			return GetAxisWithCrossDeadZone(value);
		}

		/// <summary>
		/// 获取观察方向输入（鼠标时直接返回，手柄时使用死区修正）
		/// </summary>
		public virtual Vector3 GetLookDirection()
		{
			var value = m_look.ReadValue<Vector2>();

			if (IsLookingWithMouse())
			{
				return new Vector3(value.x, 0, value.y);
			}

			return GetAxisWithCrossDeadZone(value);
		}

		/// <summary>
		/// 获取相机方向下的移动向量
		/// 将输入方向映射到相机朝向（Y轴旋转）下
		/// </summary>
		public virtual Vector3 GetMovementCameraDirection()
		{
			// 获取原始的移动方向输入（WASD或左摇杆）
			var direction = GetMovementDirection();

			// 仅在有有效输入时才进行旋转计算（避免不必要的计算）
			if (direction.sqrMagnitude > 0)
			{
				// 根据相机的Y轴旋转角度创建旋转四元数
				// 相机的eulerAngles.y代表水平旋转角度（左右转向）
				var rotation = Quaternion.AngleAxis(m_camera.transform.eulerAngles.y, Vector3.up);
				// 应用旋转到移动方向：将相对于玩家前方的输入方向转换为相对于相机前方的方向
				// 例如：玩家按前（Z+），但相机转向东方，则向量会旋转指向东方
				//表示用rotation旋转direction向量
				direction = rotation * direction;
				// 重新归一化方向向量，确保方向长度为1（移动速度由上层控制）
				direction = direction.normalized;
			}

			return direction;
		}

		/// <summary>
		/// 根据十字形死区修正输入值（Input System 默认是圆形死区）
		/// </summary>
		/// <param name="axis">输入轴</param>
		public virtual Vector3 GetAxisWithCrossDeadZone(Vector2 axis)
		{
			var deadzone = InputSystem.settings.defaultDeadzoneMin;
			axis.x = Mathf.Abs(axis.x) > deadzone ? RemapToDeadzone(axis.x, deadzone) : 0;
			axis.y = Mathf.Abs(axis.y) > deadzone ? RemapToDeadzone(axis.y, deadzone) : 0;
			return new Vector3(axis.x, 0, axis.y);
		}

		/// <summary>
		/// 判断是否通过鼠标进行观察输入
		/// </summary>
		public virtual bool IsLookingWithMouse()
		{
			if (m_look.activeControl == null)
			{
				return false;
			}
			return m_look.activeControl.device.name.Equals(k_mouseDeviceName);
		}

		// 以下是输入状态的快捷访问方法
		public virtual bool GetRun() => m_run.IsPressed();
		public virtual bool GetRunUp() => m_run.WasReleasedThisFrame();

		/// <summary>
		/// 判断是否触发跳跃（支持跳跃缓冲）
		/// </summary>
		public virtual bool GetJumpDown()
		{
			if (m_lastJumpTime != null &&
				Time.time - m_lastJumpTime < k_jumpBuffer)
			{
				m_lastJumpTime = null;
				return true;
			}
			return false;
		}

		public virtual bool GetJumpUp() => m_jump.WasReleasedThisFrame();
		public virtual bool GetDive() => m_dive.IsPressed();
		public virtual bool GetSpinDown() => m_spin.WasPressedThisFrame();
		public virtual bool GetPickAndDropDown() => m_pickAndDrop.WasPressedThisFrame();
		public virtual bool GetCrouchAndCraw() => m_crouch.IsPressed();
		public virtual bool GetAirDiveDown() => m_airDive.WasPressedThisFrame();
		public virtual bool GetStompDown() => m_stomp.WasPressedThisFrame();
		public virtual bool GetReleaseLedgeDown() => m_releaseLedge.WasPressedThisFrame();
		public virtual bool GetGlide() => m_glide.IsPressed();
		public virtual bool GetDashDown() => m_dash.WasPressedThisFrame();
		public virtual bool GetGrindBrake() => m_grindBrake.IsPressed();
		public virtual bool GetPauseDown() => m_pause.WasPressedThisFrame();

		/// <summary>
		/// 将输入值按给定死区重新映射到 0-1
		/// </summary>
		//protected float RemapToDeadzone(float value,float deadzone)=>(value - deadzone) / (1-deadzone);
		protected float RemapToDeadzone(float value,float deadzone)=>(value - (value > 0 ? -deadzone : deadzone)) / (1-deadzone);

		/// <summary>
		/// 临时锁定移动方向输入
		/// </summary>
		/// <param name="duration">锁定时长（秒）</param>
		public virtual void LockMovementDirection(float duration = 0.25f)
		{
			m_movementDirectionUnlockTime = Time.time + duration;
		}

		protected virtual void Awake() => CacheActions();

		protected virtual void Start()
		{
			m_camera = Camera.main;
			actions.Enable();
		}
		

		protected virtual void Update()
		{
			// 记录跳跃按下时间，用于实现跳跃缓冲
			if (m_jump.WasPressedThisFrame())
			{
				m_lastJumpTime = Time.time;
			}
		}

		protected virtual void OnEnable() => actions?.Enable();
		protected virtual void OnDisable() => actions?.Disable();
	}
}
