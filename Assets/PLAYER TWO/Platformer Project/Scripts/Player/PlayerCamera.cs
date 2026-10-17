using UnityEngine;
using Cinemachine;

namespace PLAYERTWO.PlatformerProject
{
	// 要求该组件必须挂在 CinemachineVirtualCamera 上
	[RequireComponent(typeof(CinemachineVirtualCamera))]
	[AddComponentMenu("PLAYER TWO/Platformer Project/Player/Player Camera")]
	public class PlayerCamera : MonoBehaviour
	{
		[Header("Camera Settings")] // 相机设置
		public Player player;              // 跟随的玩家对象
		public float maxDistance = 15f;    // 相机与目标的最大距离
		public float initialAngle = 20f;   // 初始俯仰角（相机上下角度）
		public float heightOffset = 1f;    // 相机相对玩家的垂直偏移量

		[Header("Following Settings")] // 跟随设置
		public float verticalUpDeadZone = 0.15f;      // 在地面时，相机向上跟随的死区
		public float verticalDownDeadZone = 0.15f;    // 在地面时，相机向下跟随的死区
		public float verticalAirUpDeadZone = 4f;      // 在空中时，相机向上跟随的死区
		public float verticalAirDownDeadZone = 0;     // 在空中时，相机向下跟随的死区
		public float maxVerticalSpeed = 10f;          // 相机在地面时的最大垂直跟随速度
		public float maxAirVerticalSpeed = 100f;      // 相机在空中时的最大垂直跟随速度

		[Header("Orbit Settings")] // 相机环绕设置
		public bool canOrbit = true;                  // 是否允许手动环绕相机
		public bool canOrbitWithVelocity = true;      // 是否允许通过速度带动相机旋转
		public float orbitVelocityMultiplier = 5;     // 速度驱动相机旋转的倍率

		[Range(0, 90)]
		public float verticalMaxRotation = 80;        // 相机俯仰角最大值

		[Range(-90, 0)]
		public float verticalMinRotation = -20;       // 相机俯仰角最小值

		// 内部变量
		protected float m_cameraDistance;             // 当前相机与玩家的距离
		protected float m_cameraTargetYaw;            // 相机目标的水平角度
		protected float m_cameraTargetPitch;          // 相机目标的俯仰角

		protected Vector3 m_cameraTargetPosition;     // 相机目标位置（用于插值过渡）

		protected CinemachineVirtualCamera m_camera;  // 相机对象
		protected Cinemachine3rdPersonFollow m_cameraBody; // 3D 跟随组件
		protected CinemachineBrain m_brain;           // Cinemachine 控制大脑

		protected Transform m_target;                 // 相机跟随的目标点（在玩家上方一点）

		protected string k_targetName = "Player Follower Camera Target"; // 临时目标对象的名称

		/// <summary>
		/// 初始化组件
		/// </summary>
		protected virtual void InitializeComponents()
		{
			if (!player)
			{
				// 如果没有指定 player，则在场景中自动寻找
				player = FindObjectOfType<Player>();
			}

			m_camera = GetComponent<CinemachineVirtualCamera>();
			m_cameraBody = m_camera.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
			m_brain = Camera.main.GetComponent<CinemachineBrain>();
		}

		/// <summary>
		/// 创建相机的跟随目标（跟随点）
		/// </summary>
		protected virtual void InitializeFollower()
		{
			m_target = new GameObject(k_targetName).transform;
			m_target.position = player.transform.position;
		}

		/// <summary>
		/// 初始化相机设置
		/// </summary>
		protected virtual void InitializeCamera()
		{
			m_camera.Follow = m_target.transform; // 相机跟随目标点
			m_camera.LookAt = player.transform;   // 相机始终看向玩家

			Reset();
		}

		/// <summary>
		/// 判断是否处于需要竖直跟随的状态（如游泳、爬墙、挂边等）
		/// </summary>
		protected virtual bool VerticalFollowingStates()
		{
			return player.states.IsCurrentOfType(typeof(SwimPlayerState)) ||
				player.states.IsCurrentOfType(typeof(PoleClimbingPlayerState)) ||
				player.states.IsCurrentOfType(typeof(WallDragPlayerState)) ||
				player.states.IsCurrentOfType(typeof(LedgeHangingPlayerState)) ||
				player.states.IsCurrentOfType(typeof(LedgeClimbingPlayerState)) ||
				player.states.IsCurrentOfType(typeof(RailGrindPlayerState));
		}

		/// <summary>
		/// 重置相机到初始位置与角度
		/// </summary>
		public virtual void Reset()
		{
			m_cameraDistance = maxDistance;
			m_cameraTargetPitch = initialAngle; // 设定初始俯仰角
			m_cameraTargetYaw = player.transform.rotation.eulerAngles.y; // 根据玩家朝向设定相机水平角
			m_cameraTargetPosition = player.unsizedPosition + Vector3.up * heightOffset; // 初始位置
			MoveTarget();
			m_brain.ManualUpdate(); // 强制刷新相机
		}

		/// <summary>
		/// 处理相机的高度偏移，使其根据玩家位置平滑跟随
		/// </summary>
		protected virtual void HandleOffset()
		{
			var target = player.unsizedPosition + Vector3.up * heightOffset;
			var previousPosition = m_cameraTargetPosition;
			var targetHeight = previousPosition.y;

			// 地面跟随
			if (player.isGrounded || VerticalFollowingStates())
			{
				// 当玩家目标高度超过当前相机高度加上向上死区时，触发向上跟随
				if (target.y > previousPosition.y + verticalUpDeadZone)
				{
					// 计算超出死区部分的实际高度差
					var offset = target.y - previousPosition.y - verticalUpDeadZone;
					// 累加高度差，但限制每帧最大移动量（最大垂直速度 * 帧时间），以实现平滑且限速的跟随效果
					targetHeight += Mathf.Min(offset, maxVerticalSpeed * Time.deltaTime);
				}
				else if (target.y < previousPosition.y - verticalDownDeadZone)
				{
					// 玩家下降时，相机缓慢跟随
					var offset = target.y - previousPosition.y + verticalDownDeadZone;
					targetHeight += Mathf.Max(offset, -maxVerticalSpeed * Time.deltaTime);
				}
			}
			// 空中跟随
			else if (target.y > previousPosition.y + verticalAirUpDeadZone)
			{
				var offset = target.y - previousPosition.y - verticalAirUpDeadZone;
				targetHeight += Mathf.Min(offset, maxAirVerticalSpeed * Time.deltaTime);
			}
			else if (target.y < previousPosition.y - verticalAirDownDeadZone)
			{
				var offset = target.y - previousPosition.y + verticalAirDownDeadZone;
				targetHeight += Mathf.Max(offset, -maxAirVerticalSpeed * Time.deltaTime);
			}

			m_cameraTargetPosition = new Vector3(target.x, targetHeight, target.z);
		}

		/// <summary>
		/// 手动环绕相机（通过输入控制旋转）
		/// </summary>
		protected virtual void HandleOrbit()
		{
			
			if (canOrbit)
			{
				var direction = player.inputs.GetLookDirection();

				if (direction.sqrMagnitude > 0)
				{
					var usingMouse = player.inputs.IsLookingWithMouse();
					float deltaTimeMultiplier = usingMouse ? Time.timeScale : Time.deltaTime;

					// 修改相机角度
					m_cameraTargetYaw += direction.x * deltaTimeMultiplier;
					m_cameraTargetPitch -= direction.z * deltaTimeMultiplier;
					m_cameraTargetPitch = ClampAngle(m_cameraTargetPitch, verticalMinRotation, verticalMaxRotation);
				}
			}
		}

		/// <summary>
		/// 基于玩家移动速度自动旋转相机
		/// </summary>
		protected virtual void HandleVelocityOrbit()
		{
			if (canOrbitWithVelocity && player.isGrounded)
			{
				var localVelocity = m_target.InverseTransformVector(player.velocity);
				m_cameraTargetYaw += localVelocity.x * orbitVelocityMultiplier * Time.deltaTime;
			}
		}

		/// <summary>
		/// 移动相机跟随的目标点，使相机逐帧更新位置和角度
		/// </summary>
		protected virtual void MoveTarget()
		{
			m_target.position = m_cameraTargetPosition;
			m_target.rotation = Quaternion.Euler(m_cameraTargetPitch, m_cameraTargetYaw, 0.0f);
			m_cameraBody.CameraDistance = m_cameraDistance;
		}

		/// <summary>
		/// Unity 生命周期：启动时初始化相机
		/// </summary>
		protected virtual void Start()
		{
			InitializeComponents();
			InitializeFollower();
			InitializeCamera();
		}

		/// <summary>
		/// Unity 生命周期：每帧在 LateUpdate 更新相机逻辑
		/// </summary>
		protected virtual void LateUpdate()
		{
			HandleOrbit();        // 输入环绕
			HandleVelocityOrbit(); // 速度驱动环绕
			HandleOffset();       // 高度跟随
			MoveTarget();         // 更新相机目标
		}

		/// <summary>
		/// 限制角度在给定区间内
		/// </summary>
		protected virtual float ClampAngle(float angle, float min, float max)
		{
			if (angle < -360) angle += 360;
			if (angle > 360) angle -= 360;

			return Mathf.Clamp(angle, min, max);
		}
		
		
	}
}
