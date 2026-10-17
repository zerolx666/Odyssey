using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	// 该组件依赖于以下几个组件，保证挂载 Player 时会自动附加这些必要组件
	[RequireComponent(typeof(PlayerInputManager))]   // 玩家输入管理器（处理按键、手柄等输入）
	[RequireComponent(typeof(PlayerStatsManager))]   // 玩家数值管理器（存储移动速度、跳跃力等数值配置）
	[RequireComponent(typeof(PlayerStateManager))]   // 玩家状态机管理器（Idle、Run、Jump 等状态）
	[RequireComponent(typeof(Health))]               // 生命值组件（处理血量、受伤、死亡）
	[AddComponentMenu("PLAYER TWO/Platformer Project/Player/Player")] // 添加到 Unity 组件菜单
	public class Player : Entity<Player>              // 继承自通用的 Entity<Player> 基类
	{
		public PlayerEvents playerEvents;  // 玩家事件（受伤、死亡、拾取物品等触发的事件）

		public Transform pickableSlot;     // 玩家手持物品的挂点（物品显示在这里）
		public Transform skin;             // 玩家角色皮肤（外观的 Transform，用于重置姿态）

		// 重生点位置与旋转
		protected Vector3 m_respawnPosition;
		protected Quaternion m_respawnRotation;

		// 皮肤初始位置与旋转（用于恢复外观）
		protected Vector3 m_skinInitialPosition;
		protected Quaternion m_skinInitialRotation;

		/// <summary> 玩家输入管理器实例 </summary>
		public PlayerInputManager inputs { get; protected set; }

		/// <summary> 玩家数值管理器实例 </summary>
		public PlayerStatsManager stats { get; protected set; }

		/// <summary> 生命值实例 </summary>
		public Health health { get; protected set; }

		/// <summary> 玩家是否在水中 </summary>
		public bool onWater { get; protected set; }

		/// <summary> 玩家是否正在持有物品 </summary>
		public bool holding { get; protected set; }

		/// <summary> 玩家已跳跃的次数（用于多段跳） </summary>
		public int jumpCounter { get; protected set; }

		/// <summary> 玩家已进行的空中旋转次数 </summary>
		public int airSpinCounter { get; protected set; }

		/// <summary> 玩家已进行的空中冲刺次数 </summary>
		public int airDashCounter { get; protected set; }

		/// <summary> 上一次冲刺的时间 </summary>
		public float lastDashTime { get; protected set; }

		/// <summary> 玩家最后接触到的墙面的法线（用于墙跳等逻辑） </summary>
		public Vector3 lastWallNormal { get; protected set; }

		/// <summary> 玩家当前攀爬的 Pole（竿子/杆子） </summary>
		public Pole pole { get; protected set; }

		/// <summary> 玩家当前所在的水域碰撞体 </summary>
		public Collider water { get; protected set; }

		/// <summary> 玩家当前持有的可拾取物体实例 </summary>
		public Pickable pickable { get; protected set; }

		/// <summary> 玩家是否存活（血量大于 0） </summary>
		public virtual bool isAlive => !health.isEmpty;

		/// <summary> 玩家是否可以站立（通过 SphereCast 检测头顶是否有障碍物） </summary>
		public virtual bool canStandUp => !SphereCast(Vector3.up, originalHeight);

		// 玩家从水中出来时的微小偏移
		protected const float k_waterExitOffset = 0.25f;

		// 初始化输入
		protected virtual void InitializeInputs() => inputs = GetComponent<PlayerInputManager>();
		// 初始化数值
		protected virtual void InitializeStats() => stats = GetComponent<PlayerStatsManager>();
		// 初始化生命
		protected virtual void InitializeHealth() => health = GetComponent<Health>();
		// 初始化标签（标记为 Player）
		protected virtual void InitializeTag() => tag = GameTags.Player;

		// 初始化重生点
		protected virtual void InitializeRespawn()
		{
			m_respawnPosition = transform.position;
			m_respawnRotation = transform.rotation;
		}

		// 初始化皮肤初始位置与旋转
		protected virtual void InitializeSkin()
		{
			if (skin)
			{
				m_skinInitialPosition = skin.localPosition;
				m_skinInitialRotation = skin.localRotation;
			}
		}

		/// <summary>
		/// 玩家复活：重置生命值、位置、旋转，并切换到 Idle 状态
		/// </summary>
		public virtual void Respawn()
		{
			health.Reset();   // 重置生命
			transform.SetPositionAndRotation(m_respawnPosition, m_respawnRotation); // 回到重生点
			states.Change<IdlePlayerState>(); // 状态机切换为待机
		}

		/// <summary>
		/// 设置下次重生的位置与旋转
		/// </summary>
		public virtual void SetRespawn(Vector3 position, Quaternion rotation)
		{
			m_respawnPosition = position;
			m_respawnRotation = rotation;
		}

		/// <summary>
		/// 对玩家造成伤害（带击退与受伤反应）
		/// </summary>
		/// <param name="amount">要扣除的生命值</param>
		/// <param name="origin">伤害来源位置（用于计算击退方向）</param>
		public override void ApplyDamage(int amount, Vector3 origin)
		{
			if (!health.isEmpty && !health.recovering) // 确保玩家未死亡且不在恢复无敌状态
			{
				health.Damage(amount); // 扣血
				var damageDir = origin - transform.position; // 计算受击方向
				damageDir.y = 0; // 忽略垂直方向
				damageDir = damageDir.normalized;
				FaceDirection(damageDir); // 面向攻击方向

				// 受伤时向后击退
				lateralVelocity = -transform.forward * stats.current.hurtBackwardsForce;

				if (!onWater) // 如果不在水中，则会被击飞向上并进入受伤状态
				{
					verticalVelocity = Vector3.up * stats.current.hurtUpwardForce;
					states.Change<HurtPlayerState>();
				}

				playerEvents.OnHurt?.Invoke(); // 触发受伤事件

				// 如果血量空了 -> 死亡
				if (health.isEmpty)
				{
					Throw(); // 丢掉物品
					playerEvents.OnDie?.Invoke(); // 触发死亡事件
				}
			}
		}

		/// <summary>
		/// 直接杀死玩家（生命设为 0 并触发死亡事件）
		/// </summary>
		public virtual void Die()
		{
			health.Set(0);
			playerEvents.OnDie?.Invoke();
		}

		/// <summary>
		/// 进入水中（切换到游泳状态）
		/// </summary>
		/// <param name="water">水的碰撞体</param>
		public virtual void EnterWater(Collider water)
		{
			if (!onWater && !health.isEmpty)
			{
				Throw();  // 丢掉手上的物品
				onWater = true;
				this.water = water;
				states.Change<SwimPlayerState>(); // 切换到游泳状态
			}
		}

		/// <summary>
		/// 离开水域
		/// </summary>
		public virtual void ExitWater()
		{
			if (onWater)
			{
				onWater = false;
			}
		}

		/// <summary>
		/// 抓取竿子（如果可以攀爬且条件满足）
		/// </summary>
		/// <param name="other">检测到的碰撞体</param>
		public virtual void GrabPole(Collider other)
		{
			if (stats.current.canPoleClimb && velocity.y <= 0
				&& !holding && other.TryGetComponent(out Pole pole))
			{
				this.pole = pole;
				states.Change<PoleClimbingPlayerState>(); // 切换到攀爬竿子状态
			}
		}

		/// <summary>
		/// 着陆判定：调用基类的判定，并且排除 Spring（弹簧）作为有效地面
		/// </summary>
		protected override bool EvaluateLanding(RaycastHit hit)
		{
			return base.EvaluateLanding(hit) && !hit.collider.CompareTag(GameTags.Spring);
		}
		
		/// <summary>
		/// 处理斜坡限制：当斜坡过陡时，推动玩家往斜坡下方滑动
		/// </summary>
		protected override void HandleSlopeLimit(RaycastHit hit)
		{
			if (onWater) return; // 如果在水中则不处理斜坡逻辑

			// 根据法线计算斜坡方向：
			// 1. hit.normal = 碰撞表面法线
			// 2. Vector3.Cross(hit.normal, Vector3.up) = 斜坡的横向向量
			// 3. Cross(hit.normal, 横向向量) = 斜坡下滑的方向
			var slopeDirection = Vector3.Cross(hit.normal, Vector3.Cross(hit.normal, Vector3.up));
			slopeDirection = slopeDirection.normalized;

			// 按照滑动力 stats.current.slideForce 推动角色沿斜坡下滑
			controller.Move(slopeDirection * stats.current.slideForce * Time.deltaTime);
		}

		/// <summary>
		/// 处理过高的台阶：当撞到高边缘时推动玩家离开
		/// </summary>
		protected override void HandleHighLedge(RaycastHit hit)
		{
			if (onWater) return;

			// 计算边缘方向 = 碰撞点 - 玩家位置
			var edgeNormal = hit.point - position;
			// 通过 Cross 计算推动方向，让玩家沿边缘推开
			var edgePushDirection = Vector3.Cross(edgeNormal, Vector3.Cross(edgeNormal, Vector3.up));

			// 施加一个推力（使用 gravity 值作为强度），让玩家远离过高的边缘
			controller.Move(edgePushDirection * stats.current.gravity * Time.deltaTime);
		}

		/// <summary>
		/// 在指定方向上平滑移动玩家（加速度控制）
		/// </summary>
		public virtual void Accelerate(Vector3 direction)
		{
			// 根据是否按下 Run 键、是否在地面，决定不同的转向阻尼与加速度
			// 地面奔跑时使用更小的转向阻尼（快速转向），否则使用标准阻尼
			var turningDrag = isGrounded && inputs.GetRun() ? stats.current.runningTurningDrag : stats.current.turningDrag;
			// 地面奔跑时使用更高的加速度，否则使用标准加速度
			var acceleration = isGrounded && inputs.GetRun() ? stats.current.runningAcceleration : stats.current.acceleration;
			// 在空中时使用 airAcceleration（更低的加速度），地面时使用之前计算的 acceleration
			var finalAcceleration = isGrounded ? acceleration : stats.current.airAcceleration;
			// 是否按 Run 键决定最大速度：奔跑速度更快，否则使用常规速度
			var topSpeed = inputs.GetRun() ? stats.current.runningTopSpeed : stats.current.topSpeed;

			// 调用底层 Accelerate(方向, 转向阻尼, 加速度, 最大速度)
			Accelerate(direction, turningDrag, finalAcceleration, topSpeed);

			// 如果刚松开跑步键，限制最大速度，避免瞬间超速
			if (inputs.GetRunUp())
			{
				lateralVelocity = Vector3.ClampMagnitude(lateralVelocity, topSpeed);
			}
		}

		/// <summary>
		/// 根据相机方向来平滑移动玩家,为了实现跳跃过程中可以转向
		/// </summary>
		public virtual void AccelerateToInputDirection()
		{
			var inputDirection = inputs.GetMovementCameraDirection(); // 输入相对于相机的方向
			Accelerate(inputDirection);
		}

		/// <summary>
		/// 应用标准的斜坡修正因子（在上坡 / 下坡时修改移动表现）
		/// </summary>
		public virtual void RegularSlopeFactor()
		{
			if (stats.current.applySlopeFactor)
				SlopeFactor(stats.current.slopeUpwardForce, stats.current.slopeDownwardForce);
		}

		/// <summary>
		/// 在指定方向上平滑移动玩家（水下的参数）
		/// </summary>
		public virtual void WaterAcceleration(Vector3 direction) =>
			Accelerate(direction, stats.current.waterTurningDrag, stats.current.swimAcceleration, stats.current.swimTopSpeed);

		/// <summary>
		/// 在指定方向上平滑移动玩家（匍匐状态的参数）
		/// </summary>
		public virtual void CrawlingAccelerate(Vector3 direction) =>
			Accelerate(direction, stats.current.crawlingTurningSpeed, stats.current.crawlingAcceleration, stats.current.crawlingTopSpeed);

		/// <summary>
		/// 在空翻动作中平滑移动玩家（后空翻参数）
		/// </summary>
		public virtual void BackflipAcceleration()
		{
			var direction = inputs.GetMovementCameraDirection();
			Accelerate(direction, stats.current.backflipTurningDrag, stats.current.backflipAirAcceleration, stats.current.backflipTopSpeed);
		}

		/// <summary>
		/// 平滑减速（使用 deceleration 参数）
		/// </summary>
		public virtual void Decelerate() => Decelerate(stats.current.deceleration);

		/// <summary>
		/// 平滑减速（使用摩擦力参数）,用于没有输入的情况下
		/// </summary>
		public virtual void Friction()
		{
			if (OnSlopingGround())
				Decelerate(stats.current.slopeFriction); // 在斜坡上使用斜坡摩擦
			else
				Decelerate(stats.current.friction);      // 普通摩擦
		}

		/// <summary>
		/// 施加重力，使玩家下落
		/// </summary>
		public virtual void Gravity()
		{
			if (!isGrounded && verticalVelocity.y > -stats.current.gravityTopSpeed)
			{
				var speed = verticalVelocity.y;
				// 上升时用普通重力，下落时用更强的下落重力
				var force = verticalVelocity.y > 0 ? stats.current.gravity : stats.current.fallGravity;
				speed -= force * gravityMultiplier * Time.deltaTime;

				// 限制最大下落速度
				speed = Mathf.Max(speed, -stats.current.gravityTopSpeed);
				verticalVelocity = new Vector3(0, speed, 0);
			}
		}

		/// <summary>
		/// 通过 snap 力量强制把玩家贴到地面上
		/// </summary>
		public virtual void SnapToGround() => SnapToGround(stats.current.snapForce);

		/// <summary>
		/// 平滑朝向某个方向旋转（陆地旋转速度）
		/// </summary>
		public virtual void FaceDirectionSmooth(Vector3 direction) => FaceDirection(direction, stats.current.rotationSpeed);

		/// <summary>
		/// 平滑朝向某个方向旋转（水中旋转速度）
		/// </summary>
		public virtual void WaterFaceDirection(Vector3 direction) => FaceDirection(direction, stats.current.waterRotationSpeed);

		/// <summary>
		/// 如果玩家不在地面上，切换到下落状态
		/// </summary>
		public virtual void Fall()
		{
			if (!isGrounded)
			{
				states.Change<FallPlayerState>();
			}
		}

		/// <summary>
		/// 执行跳跃逻辑（包括多段跳、土狼跳、持物判定）
		/// </summary>
		public virtual void Jump()
		{
			// 是否可以进行二段 / 多段跳
			var canMultiJump = (jumpCounter > 0) && (jumpCounter < stats.current.multiJumps);
			// 土狼跳判定（离地一小段时间内仍然可以跳）
			var canCoyoteJump = (jumpCounter == 0) && (Time.time < lastGroundTime + stats.current.coyoteJumpThreshold);
			// 是否允许在持物状态下跳跃
			var holdJump = !holding || stats.current.canJumpWhileHolding;

			// 地面 / 轨道 / 多段跳 / 土狼跳条件满足时才允许跳跃
			if ((isGrounded  || canMultiJump || canCoyoteJump) )
			{
				if (inputs.GetJumpDown()) // 按下跳跃键
				{
					Jump(stats.current.maxJumpHeight);
				}
			}

			// 松开跳跃键时，如果还在上升，限制为最小跳跃高度（实现“按得短跳得低”的效果）
			if (inputs.GetJumpUp() && (jumpCounter > 0) && (verticalVelocity.y > stats.current.minJumpHeight))
			{
				verticalVelocity = Vector3.up * stats.current.minJumpHeight;
			}
		}

		/// <summary>
		/// 执行一个标准的向上跳跃
		/// </summary>
		public virtual void Jump(float height)
		{
			jumpCounter++; // 增加跳跃计数
			verticalVelocity = Vector3.up * height; // 设置垂直速度
			states.Change<FallPlayerState>();       // 切换为下落状态（跳起后最终会落下）
			playerEvents.OnJump?.Invoke();          // 触发跳跃事件
		}

		/// <summary>
		/// 执行带方向的跳跃（比如斜向跳）
		/// </summary>
		public virtual void DirectionalJump(Vector3 direction, float height, float distance)
		{
			jumpCounter++;
			verticalVelocity = Vector3.up * height; // 垂直上升
			lateralVelocity = direction * distance; // 水平方向的推动
			playerEvents.OnJump?.Invoke();
		}

		/// <summary>
		/// 重置空中冲刺计数
		/// </summary>
		public virtual void ResetAirDash() => airDashCounter = 0;

		/// <summary>
		/// 重置跳跃计数（回到 0，常用于落地时）
		/// </summary>
		public virtual void ResetJumps() => jumpCounter = 0;

		/// <summary>
		/// 设置跳跃计数为指定值（特殊用途）
		/// </summary>
		public virtual void SetJumps(int amount) => jumpCounter = amount;

		/// <summary>
		/// 重置空中旋转次数
		/// </summary>
		public virtual void ResetAirSpins() => airSpinCounter = 0;

		/// <summary>
		/// 执行旋转动作（Spin）
		/// </summary>
		public virtual void Spin()
		{
			// 空中旋转条件：允许空中旋转 && 未超过上限
			var canAirSpin = (isGrounded || stats.current.canAirSpin) && airSpinCounter < stats.current.allowedAirSpins;

			// 满足旋转条件 + 没有持物 + 按下旋转键
			if (stats.current.canSpin && canAirSpin && !holding && inputs.GetSpinDown())
			{
				if (!isGrounded)
				{
					airSpinCounter++; // 空中旋转次数 +1
				}

				states.Change<SpinPlayerState>(); // 切换到旋转状态
				playerEvents.OnSpin?.Invoke();    // 触发旋转事件
			}
		}

				/// <summary>
		/// 拾取或投掷物体（根据当前状态决定是拾取还是丢出）
		/// </summary>
		public virtual void PickAndThrow()
		{
			// 玩家当前允许拾取并且输入了“拾取/放下”按键
			if (stats.current.canPickUp && inputs.GetPickAndDropDown())
			{
				if (!holding) // 如果当前没有拿着东西
				{
					// 在角色前方做一个 CapsuleCast（胶囊体检测），看是否有可拾取物
					if (CapsuleCast(transform.forward,
						stats.current.pickDistance, out var hit))
					{
						// 如果检测到的物体有 Pickable 组件，则执行拾取
						if (hit.transform.TryGetComponent(out Pickable pickable))
						{
							PickUp(pickable);
						}
					}
				}
				else // 如果当前正在拿着物品，则执行投掷
				{
					Throw();
				}
			}
		}

		/// <summary>
		/// 拾取物品逻辑
		/// </summary>
		public virtual void PickUp(Pickable pickable)
		{
			// 必须没有拿东西，并且玩家在地面上或允许空中拾取
			if (!holding && (isGrounded || stats.current.canPickUpOnAir))
			{
				holding = true; // 标记正在持有物品
				this.pickable = pickable; // 记录被拾取的物品
				pickable.PickUp(pickableSlot); // 把物品附着到拾取点（手、头顶等）
				pickable.onRespawn.AddListener(RemovePickable); // 监听物品的重生事件，如果物品重生就清除引用
				playerEvents.OnPickUp?.Invoke(); // 触发拾取事件
			}
		}

		/// <summary>
		/// 投掷物品逻辑
		/// </summary>
		public virtual void Throw()
		{
			if (holding)
			{
				// 投掷力与玩家的水平移动速度相关
				var force = lateralVelocity.magnitude * stats.current.throwVelocityMultiplier;
				pickable.Release(transform.forward, force); // 按角色前方向丢出
				pickable = null; // 清除物品引用
				holding = false; // 置空持有状态
				playerEvents.OnThrow?.Invoke(); // 触发投掷事件
			}
		}

		/// <summary>
		/// 移除手中持有的物品（例如物品消失或重生时）
		/// </summary>
		public virtual void RemovePickable()
		{
			if (holding)
			{
				pickable = null;
				holding = false;
			}
		}

		/// <summary>
		/// 空中俯冲（下劈攻击）
		/// </summary>
		public virtual void AirDive()
		{
			// 必须允许空中俯冲，且不在地面上，没有拿物品，按下了空中俯冲按键
			if (stats.current.canAirDive && !isGrounded && !holding && inputs.GetAirDiveDown())
			{
				states.Change<AirDivePlayerState>(); // 切换到空中俯冲状态
				playerEvents.OnAirDive?.Invoke();
			}
		}

		/// <summary>
		/// 踩踏攻击（从空中下踩敌人）
		/// </summary>
		public virtual void StompAttack()
		{
			if (!isGrounded && !holding && stats.current.canStompAttack && inputs.GetStompDown())
			{
				states.Change<StompPlayerState>();
			}
		}

		/// <summary>
		/// 抓住悬崖（Ledge Grab）
		/// </summary>
		public virtual void LedgeGrab()
		{
			// 必须允许挂边，角色正在下落，没有拿东西，并且存在悬挂状态类
			// 同时检测到悬崖
			if (stats.current.canLedgeHang && velocity.y < 0 && !holding &&
				states.ContainsStateOfType(typeof(LedgeHangingPlayerState)) &&
				DetectingLedge(stats.current.ledgeMaxForwardDistance, stats.current.ledgeMaxDownwardDistance, out var hit))
			{
				// 排除球体和胶囊体碰撞体（避免挂到错误的物体）
				if (!(hit.collider is CapsuleCollider) && !(hit.collider is SphereCollider))
				{
					// 计算角色挂到悬崖的位置
					var ledgeDistance = radius + stats.current.ledgeMaxForwardDistance;
					var lateralOffset = transform.forward * ledgeDistance;
					var verticalOffset = Vector3.down * height * 0.5f - center;

					velocity = Vector3.zero; // 停止角色运动
					// 如果挂的是平台，角色会成为平台的子物体
					transform.parent = hit.collider.CompareTag(GameTags.Platform) ? hit.transform : null;
					// 定位角色到挂边位置
					transform.position = hit.point - lateralOffset + verticalOffset;

					// 切换状态到挂边
					states.Change<LedgeHangingPlayerState>();
					playerEvents.OnLedgeGrabbed?.Invoke();
				}
			}
		}

		/// <summary>
		/// 后空翻
		/// </summary>
		public virtual void Backflip(float force)
		{
			if (stats.current.canBackflip && !holding)
			{
				verticalVelocity = Vector3.up * stats.current.backflipJumpHeight; // 上跳力
				lateralVelocity = -transform.forward * force; // 向后推力
				states.Change<BackflipPlayerState>();
				playerEvents.OnBackflip.Invoke();
			}
		}

		/// <summary>
		/// 冲刺（包括地面冲刺和空中冲刺）
		/// </summary>
		public virtual void Dash()
		{
			// 是否可以空中冲刺
			var canAirDash = stats.current.canAirDash && !isGrounded &&
				airDashCounter < stats.current.allowedAirDashes;

			// 是否可以地面冲刺（冷却结束）
			var canGroundDash = stats.current.canGroundDash && isGrounded &&
				Time.time - lastDashTime > stats.current.groundDashCoolDown;

			// 如果按下冲刺键，且符合条件
			if (inputs.GetDashDown() && (canAirDash || canGroundDash))
			{
				if (!isGrounded) airDashCounter++; // 空中冲刺计数+1
				lastDashTime = Time.time; // 记录冲刺时间
				states.Change<DashPlayerState>(); // 切换到冲刺状态
			}
		}

		/// <summary>
		/// 滑翔（下落时减缓下落速度）
		/// </summary>
		public virtual void Glide()
		{
			if (!isGrounded && inputs.GetGlide() &&
				verticalVelocity.y <= 0 && stats.current.canGlide)
				states.Change<GlidingPlayerState>();
		}

		/// <summary>
		/// 设置皮肤模型的父物体（比如挂在某个武器或挂点上）
		/// </summary>
		public virtual void SetSkinParent(Transform parent)
		{
			if (skin)
			{
				skin.parent = parent;
			}
		}

		/// <summary>
		/// 重置皮肤的父物体（回到玩家本体，恢复初始位置和旋转）
		/// </summary>
		public virtual void ResetSkinParent()
		{
			if (skin)
			{
				skin.parent = transform;
				skin.localPosition = m_skinInitialPosition;
				skin.localRotation = m_skinInitialRotation;
			}
		}

		/// <summary>
		/// 贴墙下滑（Wall Drag）
		/// </summary>
		public virtual void WallDrag(Collider other)
		{
			if (stats.current.canWallDrag && velocity.y <= 0 &&
				!holding && !other.TryGetComponent<Rigidbody>(out _))
			{
				// 检测前方是否有可滑的墙体
				if (CapsuleCast(transform.forward, 0.25f, out var hit,
					stats.current.wallDragLayers) && !DetectingLedge(0.25f, height, out _))
				{
					// 如果是平台，成为其子物体
					if (hit.collider.CompareTag(GameTags.Platform))
						transform.parent = hit.transform;

					lastWallNormal = hit.normal; // 记录墙面法线
					states.Change<WallDragPlayerState>();
				}
			}
		}

		/// <summary>
		/// 推动刚体（例如推动箱子）
		/// </summary>
		public virtual void PushRigidbody(Collider other)
		{
			// 排除台阶上方的情况，检测目标是否是刚体
			if (!IsPointUnderStep(other.bounds.max) &&
				other.TryGetComponent(out Rigidbody rigidbody))
			{
				// 推动力量与玩家的水平速度相关
				var force = lateralVelocity * stats.current.pushForce;
				// 模拟物理推力（除以质量，乘上deltaTime）
				rigidbody.velocity += force / rigidbody.mass * Time.deltaTime;
			}
		}

		/// <summary>
		/// 检测悬崖是否可抓取
		/// </summary>
		protected virtual bool DetectingLedge(float forwardDistance, float downwardDistance, out RaycastHit ledgeHit)
		{
			var contactOffset = Physics.defaultContactOffset + positionDelta;
			var ledgeMaxDistance = radius + forwardDistance;
			var ledgeHeightOffset = height * 0.5f + contactOffset;
			var upwardOffset = transform.up * ledgeHeightOffset;
			var forwardOffset = transform.forward * ledgeMaxDistance;

			// 如果前方或上方有遮挡，则不能挂边
			if (Physics.Raycast(position + upwardOffset, transform.forward, ledgeMaxDistance,
				Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
				Physics.Raycast(position + forwardOffset * .01f, transform.up, ledgeHeightOffset,
				Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
			{
				ledgeHit = new RaycastHit();
				return false;
			}

			// 向下检测是否有可挂的 ledge
			var origin = position + upwardOffset + forwardOffset;
			var distance = downwardDistance + contactOffset;
			
			return Physics.Raycast(origin, Vector3.down, out ledgeHit, distance,
				stats.current.ledgeHangingLayers, QueryTriggerInteraction.Ignore);
		}

		/// <summary>
		/// 开始轨道滑行（Rail Grind）
		/// </summary>
		public virtual void StartGrind() => states.Change<RailGrindPlayerState>();

		/// <summary>
		/// 初始化
		/// </summary>
		protected override void Awake()
		{
			base.Awake();
			InitializeInputs();
			InitializeStats();
			InitializeHealth();
			InitializeTag();
			InitializeRespawn();

			// 监听落地事件，重置跳跃/空中技能次数
			entityEvents.OnGroundEnter.AddListener(() =>
			{
				ResetJumps();
				ResetAirSpins();
				ResetAirDash();
			});

			// 监听进入轨道事件，重置空中技能并进入滑轨状态
			entityEvents.OnRailsEnter.AddListener(() =>
			{
				ResetJumps();
				ResetAirSpins();
				ResetAirDash();
				StartGrind();
			});
		}

		/// <summary>
		/// 触发检测（玩家停留在触发器内）
		/// 用于检测是否进入水体或离开水体
		/// </summary>
		protected virtual void OnTriggerStay(Collider other)
		{
			if (other.CompareTag(GameTags.VolumeWater))
			{
				// 如果当前不在水中，但进入了水体包围盒
				if (!onWater && other.bounds.Contains(unsizedPosition))
				{
					EnterWater(other);
				}
				// 如果已经在水中，则检测是否离开
				else if (onWater)
				{
					// 计算一个向下偏移点，判断是否离开水面
					var exitPoint = position + Vector3.down * k_waterExitOffset;

					if (!other.bounds.Contains(exitPoint))
					{
						ExitWater();
					}
				}
			}
		}
	}
}
