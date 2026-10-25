using UnityEngine;
using UnityEngine.Splines;

namespace PLAYERTWO.PlatformerProject
{
	public abstract class EntityBase : MonoBehaviour
	{
		public EntityEvents entityEvents;

		protected Collider[] m_contactBuffer = new Collider[10];    // 碰撞检测缓冲区，用于存储接触的碰撞体
		protected Collider[] m_penetrationBuffer = new Collider[32]; // 碰撞渗透检测缓冲区，用于存储穿透的碰撞体

		protected readonly float m_groundOffset = 0.1f;              // 地面检测偏移
		protected readonly float m_penetrationOffset = -0.1f;        // 渗透检测偏移
		protected readonly float m_slopingGroundAngle = 20f;         // 斜坡角度阈值，判断是否处于斜坡

		public CharacterController controller { get; protected set; } // 角色控制器组件

		public Vector3 velocity { get; set; }                          // 当前速度

		// 当前水平速度（XZ平面速度）
		public Vector3 lateralVelocity
		{
			get { return new Vector3(velocity.x, 0, velocity.z); }
			set { velocity = new Vector3(value.x, velocity.y, value.z); }
		}

		// 当前垂直速度（Y轴速度）
		public Vector3 verticalVelocity
		{
			get { return new Vector3(0, velocity.y, 0); }
			set { velocity = new Vector3(velocity.x, value.y, velocity.z); }
		}

		public Vector3 lastPosition { get; set; }                     // 上一帧的位置

		// 实体当前位置（角色控制器中心加位置）
		public Vector3 position => transform.position + center;

		// 忽略碰撞器缩放的实体位置（用于某些计算）
		public Vector3 unsizedPosition => position - transform.up * height * 0.5f + transform.up * originalHeight * 0.5f;

		// 脚步位置（底部位置，考虑了stepOffset）
		public Vector3 stepPosition => position - transform.up * (height * 0.5f - controller.stepOffset);

		public float positionDelta { get; protected set; }            // 当前位置和上一帧位置的距离

		public float lastGroundTime { get; protected set; }           // 上一次处于地面时间

		public bool isGrounded { get; protected set; } = true;        // 是否在地面上

		public bool onRails { get; set; }                              // 是否处于轨道（Spline轨迹）上

		public float accelerationMultiplier { get; set; } = 1f;       // 加速度倍率

		public float gravityMultiplier { get; set; } = 1f;            // 重力倍率

		public float topSpeedMultiplier { get; set; } = 1f;           // 最高速度倍率

		public float turningDragMultiplier { get; set; } = 1f;        // 转向阻力倍率

		public float decelerationMultiplier { get; set; } = 1f;       // 减速度倍率

		public RaycastHit groundHit;                                   // 当前地面检测的碰撞信息

		public SplineContainer rails { get; protected set; }           // 当前轨道（Spline轨迹）

		public float groundAngle { get; protected set; }               // 当前地面角度

		public Vector3 groundNormal { get; protected set; }            // 当前地面法线

		public Vector3 localSlopeDirection { get; protected set; }     // 当前地面的局部斜坡方向

		public float originalHeight { get; protected set; }            // 初始碰撞器高度

		public float height => controller.height;                      // 碰撞器当前高度

		public float radius => controller.radius;                      // 碰撞器半径

		public Vector3 center => controller.center;                    // 碰撞器中心点

		protected CapsuleCollider m_collider;                          // 自定义胶囊碰撞器（用于自定义碰撞）

		protected BoxCollider m_penetratorCollider;                    // 用于检测碰撞渗透的盒状碰撞器

		protected Rigidbody m_rigidbody;                                // 刚体组件，用于物理模拟
		

		// 判断一个点是否在实体脚步位置下方（用于踩踏检测）
		public virtual bool IsPointUnderStep(Vector3 point) => stepPosition.y > point.y;

		// 判断实体是否在斜坡上
		public virtual bool OnSlopingGround()
		{
		    // 如果实体接触地面并且地面角度大于斜坡的角度阈值
		    if (isGrounded && groundAngle > m_slopingGroundAngle)
		    {
		        // 从当前实体位置沿着下方发射射线进行检测
		        if (Physics.Raycast(transform.position, -transform.up, out var hit, height * 2f,
		            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
		        {
		            // 如果射线命中的法线与世界上方向（Vector3.up）之间的夹角大于斜坡角度阈值，则认为是在斜坡上
		            return Vector3.Angle(hit.normal, Vector3.up) > m_slopingGroundAngle;
		        }
		        else
		            // 如果射线检测没有命中地面，认为是在斜坡上
		            return true;
		    }
		    return false;
		}

		// 调整角色控制器碰撞器高度
		public virtual void ResizeCollider(float height)
		{
		    // 计算新的高度和当前高度的差值
		    var delta = height - this.height;
		    // 修改角色控制器的高度
		    controller.height = height;
		    // 调整角色控制器的中心位置，使其根据高度变化自动平移
		    controller.center += Vector3.up * delta * 0.5f;
		}

		// 胶囊体检测（无返回检测信息，只返回是否检测到碰撞）
		public virtual bool CapsuleCast(Vector3 direction, float distance, int layer = Physics.DefaultRaycastLayers,
		    QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore)
		{
		    // 调用带有返回检测信息的方法，并忽略返回的hit信息
		    return CapsuleCast(direction, distance, out _, layer, queryTriggerInteraction);
		}

		// 胶囊体检测（返回检测信息，检测是否与其他物体发生碰撞）
		public virtual bool CapsuleCast(Vector3 direction, float distance,
		    out RaycastHit hit, int layer = Physics.DefaultRaycastLayers,
		    QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore)
		{
		    // 计算胶囊体的起始位置
		    var origin = position - direction * radius + center;
		    // 计算偏移量，调整胶囊体的上下半部分，使得碰撞器的中心处于正确位置
		    var offset = transform.up * (height * 0.5f - radius);
		    // 计算胶囊体的顶部和底部位置
		    var top = origin + offset;
		    var bottom = origin - offset;

		    // 使用物理引擎进行胶囊体碰撞检测
		    return Physics.CapsuleCast(top, bottom, radius, direction,
		        out hit, distance + radius, layer, queryTriggerInteraction);
		}

		// 球形检测（无返回检测信息，只返回是否检测到碰撞）
		public virtual bool SphereCast(Vector3 direction, float distance, int layer = Physics.DefaultRaycastLayers,
		    QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore)
		{
		    // 调用带有返回检测信息的方法，并忽略返回的hit信息
		    return SphereCast(direction, distance, out _, layer, queryTriggerInteraction);
		}

		// 球形检测（返回检测信息，检测是否与其他物体发生碰撞）
		public virtual bool SphereCast(Vector3 direction, float distance,
		    out RaycastHit hit, int layer = Physics.DefaultRaycastLayers,
		    QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore)
		{
		    // 计算球形检测的有效距离，确保球形的检测范围符合预期
		    var castDistance = Mathf.Abs(distance - radius);

		    // 使用物理引擎进行球形碰撞检测
		    return Physics.SphereCast(position, radius, direction,
		        out hit, castDistance, layer, queryTriggerInteraction);
		}


		// 检测与其他实体的重叠，结果存储到传入的数组
		public virtual int OverlapEntity(Collider[] result, float skinOffset = 0)
		{
			// 计算接触偏移量（包括碰撞器的皮肤宽度和默认的接触偏移量）
			var contactOffset = skinOffset + controller.skinWidth + Physics.defaultContactOffset;
			// 计算重叠半径（包括胶囊碰撞器的半径和接触偏移量）
			var overlapsRadius = radius + contactOffset;
			// 计算碰撞器顶部和底部的偏移位置
			var offset = (height + contactOffset) * 0.5f - overlapsRadius;
			// 计算胶囊碰撞器的顶部位置（是球心位置）
			var top = position + Vector3.up * offset;
			// 计算胶囊碰撞器的底部位置（是球心位置）
			var bottom = position + Vector3.down * offset;
			// 使用Physics.OverlapCapsuleNonAlloc来检测与其他实体的重叠，返回重叠的实体数量
			return Physics.OverlapCapsuleNonAlloc(top, bottom, overlapsRadius, result);
		}


		// 受到伤害（空实现，子类重写）
		public virtual void ApplyDamage(int damage, Vector3 origin) { }
	}

	// 泛型版本实体类，T继承自Entity<T>
	public abstract class Entity<T> : EntityBase where T : Entity<T>
	{
		public EntityStateManager<T> states { get; protected set; }    // 状态管理器

		protected virtual void Awake()
		{
			InitializeController();
			InitializePenetratorCollider();
			InitializeStateManager();
		}

		protected virtual void Update()
		{
			// 检查角色控制器是否启用，或者自定义碰撞器是否存在
			if (controller.enabled || m_collider != null)
			{
				// 处理状态机逻辑：调用当前状态的 Step() 方法
				// 驱动所有状态的帧更新（如移动、跳跃、冲刺等状态的具体逻辑）
				HandleStates();

				// 处理角色移动：使用 CharacterController 或自定义 Transform 移动
				// 将速度向量乘以时间增量，应用到角色位置
				HandleController();

				// 处理轨道（Spline）系统：检测是否在轨道上，并处理进出轨道逻辑
				// 使用球体投射检测轨道碰撞体，决定是否进入/离开轨道模式
				HandleSpline();

				// 处理地面检测：更新角色的接地状态和地面信息
				// 判断是否接地、更新地面法线/坡度、处理落地和离地事件
				HandleGround();

				// 处理接触碰撞：检测与其他物体的重叠并触发接触回调
				// 调用 OnContact 回调，并通知接触对象的 IEntityContact 接口
				HandleContacts();

				// 子类特殊处理（空实现，由子类重写以实现自定义逻辑）
				// 用于派生类在每帧执行其他必要的更新
				OnUpdate();
			}
		}

		//收尾工作、位置/相机最终修正,放LateUpdate里合适
		protected virtual void LateUpdate()
		{
			if (controller.enabled)
			{
				HandlePosition();
				HandlePenetration();
			}
		}
		
		// 初始化角色控制器组件（CharacterController）
		// 负责角色的基本移动、碰撞等物理交互
		protected virtual void InitializeController()
		{
		    // 获取当前物体上的 CharacterController 组件
		    controller = GetComponent<CharacterController>();
		    // 如果没有，就动态添加一个 CharacterController
		    if (!controller)
		    {
		        controller = gameObject.AddComponent<CharacterController>();
		    }

		    // skinWidth 表示碰撞器表面到实际碰撞检测边界的距离（防止卡住用的小偏移）
		    controller.skinWidth = 0.005f;
		    // minMoveDistance 为最小移动距离（设为 0 表示即使移动非常小也会被检测到）
		    controller.minMoveDistance = 0;
		    // 记录角色控制器的初始高度（用于后续复位或高度调整）
		    originalHeight = controller.height;
		}

		// 初始化用于碰撞渗透检测的盒碰撞器（辅助碰撞检测）
		// 这个盒碰撞器的作用是检测角色是否与其它物体相交（“穿模”检测）
		protected virtual void InitializePenetratorCollider()
		{
		    // 计算 XZ 平面的尺寸（直径 = 半径 * 2，减去 skinWidth 避免干扰）
		    var xzSize = radius * 2f - controller.skinWidth;

		    // 动态添加 BoxCollider
		    m_penetratorCollider = gameObject.AddComponent<BoxCollider>();

		    /*
				Slope Limit：爬坡最大角度
				Step Offset：爬梯最大高度
				Skin Width：皮肤厚度
				Min Move Distance：最小移动距离
				Center、Radius、Height：角色用于检测碰撞的胶囊体中心、半径、高
		     */
		    // 设置盒碰撞器尺寸：XZ 平面是计算好的直径，高度减去stepOffset
		    m_penetratorCollider.size = new Vector3(xzSize, height - controller.stepOffset, xzSize);

		    // 设置碰撞器中心位置：在角色中心的基础上向上偏移 stepOffset 一半
		    m_penetratorCollider.center = center + Vector3.up * controller.stepOffset * 0.5f;

		    // 设置为触发器模式（不产生物理碰撞，只检测重叠）
		    m_penetratorCollider.isTrigger = true;
		}

		// 初始化胶囊碰撞器（CapsuleCollider）
		// 用于自定义的碰撞检测逻辑（比如 Overlap 检查），默认禁用
		protected virtual void InitializeCollider()
		{
		    // 动态添加 CapsuleCollider
		    m_collider = gameObject.AddComponent<CapsuleCollider>();

		    // 高度与 CharacterController 保持一致
		    m_collider.height = controller.height;

		    // 半径与 CharacterController 保持一致
		    m_collider.radius = controller.radius;

		    // 中心点与 CharacterController 保持一致
		    m_collider.center = controller.center;

		    // 设置为触发器（不产生物理碰撞，只检测）
		    m_collider.isTrigger = true;

		    // 默认禁用（只在需要的时候启用）
		    m_collider.enabled = false;
		}

		// 初始化刚体组件（Rigidbody）
		// 用于与物理系统交互，这里设置为运动学模式（不受物理力影响）
		protected virtual void InitializeRigidbody()
		{
		    // 动态添加 Rigidbody
		    m_rigidbody = gameObject.AddComponent<Rigidbody>();

		    // 设置为 Kinematic（运动学刚体，不受重力和物理力作用）
		    m_rigidbody.isKinematic = true;
		}


		// 初始化状态管理器
		protected virtual void InitializeStateManager() => states = GetComponent<EntityStateManager<T>>();

		// 处理状态机的步进逻辑
		protected virtual void HandleStates() => states.Step();

		// 处理角色控制器的移动
		protected virtual void HandleController()
		{
			if (controller.enabled)
			{
				controller.Move(velocity * Time.deltaTime);
				return;
			}

			transform.position += velocity * Time.deltaTime;
		}

		// 处理轨道（Spline）上的逻辑
		protected virtual void HandleSpline()
		{
			var distance = (height * 0.5f) + height * 0.5f;

			if (SphereCast(-transform.up, distance, out var hit) &&
				hit.collider.CompareTag(GameTags.InteractiveRail))
			{
				if (!onRails && verticalVelocity.y <= 0)
				{
					EnterRail(hit.collider.GetComponent<SplineContainer>());
				}
			}
			else
			{
				ExitRail();
			}
		}

		// 处理角色与地面的检测与相关逻辑
		protected virtual void HandleGround()
		{
		    // 如果角色在轨道模式（onRails）下，不做地面检测
		    if (onRails) return;

		    // 距离计算：角色半高 + 地面检测的额外偏移量
		    var distance = (height * 0.5f) + m_groundOffset;

		    // 向下发射球体射线检测地面，并且角色的垂直速度 ≤ 0（下落或静止状态）
		    if (SphereCast(Vector3.down, distance, out var hit) && verticalVelocity.y <= 0)
		    {
		        // 如果之前不在地面状态
		        if (!isGrounded)
		        {
		            // 判断是否满足落地条件
		            if (EvaluateLanding(hit))
		            {
		                // 进入落地逻辑
		                EnterGround(hit);
		            }
		            else
		            {
		                // 否则是接近高台边缘的情况
		                HandleHighLedge(hit);
		            }
		        }
		        // 已经在地面状态
		        else if (IsPointUnderStep(hit.point))
		        {
		            // 更新地面信息（比如接触点、法线等）
		            UpdateGround(hit);

		            // 如果地面坡度角度超过允许值
		            if (Vector3.Angle(hit.normal, Vector3.up) >= controller.slopeLimit)
		            {
		                // 处理坡度限制（可能是滑落或停止移动）
		                HandleSlopeLimit(hit);
		            }
		        }
		        else
		        {
		            // 不在台阶下方，则可能是悬空或高台边缘
		            HandleHighLedge(hit);
		        }
		    }
		    else
		    {
		        // 射线未检测到地面，则视为离开地面
		        ExitGround();
		    }
		}

		// 检测与其他物体的接触（碰撞触发）
		protected virtual void HandleContacts()
		{
		    // 检测与其他实体的重叠，并把结果存入 m_contactBuffer
		    var overlaps = OverlapEntity(m_contactBuffer);

		    // 遍历所有重叠碰撞体
		    for (int i = 0; i < overlaps; i++)
		    {
		        // 排除触发器碰撞体和自身碰撞体
		        if (!m_contactBuffer[i].isTrigger && m_contactBuffer[i].transform != transform)
		        {
		            // 调用本对象的接触回调
		            OnContact(m_contactBuffer[i]);

		            // 获取该物体上所有实现 IEntityContact 接口的组件
		            var listeners = m_contactBuffer[i].GetComponents<IEntityContact>();

		            // 依次调用对方的接触回调（实现双向交互）
		            foreach (var contact in listeners)
		            {
		                contact.OnEntityContact((T)this);
		            }

		            // 如果接触物体的底部高于角色的顶部（即碰到头顶的物体）
		            if (m_contactBuffer[i].bounds.min.y > controller.bounds.max.y)
		            {
		                // 限制向上的垂直速度（防止继续向上穿透）
		                verticalVelocity = Vector3.Min(verticalVelocity, Vector3.zero);
		            }
		        }
		    }
		}

		// 记录位置变化，用于计算位移
		protected virtual void HandlePosition()
		{
		    // 计算当前位置与上一帧位置的距离（位移长度）
		    positionDelta = (position - lastPosition).magnitude;

		    // 更新上一帧位置
		    lastPosition = position;
		}

		// 处理碰撞体的穿透修正,碰到别的东西给反推回来
		protected virtual void HandlePenetration()
		{
		    // 获取盒碰撞器在 X/Z 平面的半宽
		    var xzSize = m_penetratorCollider.size.x * 0.5f;
		    // 获取盒碰撞器在 Y 方向的半高（考虑 stepOffset 的一半）
		    var ySize = (height - controller.stepOffset * 0.5f) * 0.5f;
		    // 盒碰撞器的中心点（向上偏移 stepOffset 一半）
		    var origin = position + Vector3.up * controller.stepOffset * 0.5f;
		    // 盒碰撞器的半尺寸
		    var halfExtents = new Vector3(xzSize, ySize, xzSize);

		    // 检测所有与盒碰撞器重叠的物体，忽略触发器
		    var overlaps = Physics.OverlapBoxNonAlloc(
		        origin, 
		        halfExtents, 
		        m_penetrationBuffer,
		        Quaternion.identity, 
		        Physics.DefaultRaycastLayers, 
		        QueryTriggerInteraction.Ignore
		    );

		    // 遍历所有重叠的碰撞体
		    for (int i = 0; i < overlaps; i++)
		    {
		        // 排除触发器、自身，并且只在静止或与平台接触时处理
		        if (!m_penetrationBuffer[i].isTrigger && m_penetrationBuffer[i].transform != transform &&
		            (lateralVelocity.sqrMagnitude == 0 || m_penetrationBuffer[i].CompareTag(GameTags.Platform)))
		        {
		            // ComputePenetration 计算两个碰撞体的分离向量和距离
		            if (Physics.ComputePenetration(
		                m_penetratorCollider, position, Quaternion.identity,
		                m_penetrationBuffer[i], m_penetrationBuffer[i].transform.position,
		                m_penetrationBuffer[i].transform.rotation, 
		                out var direction, out float distance))
		            {
		                // 只保留水平方向的推离（避免角色被垂直方向顶飞）
		                var pushDirection = new Vector3(direction.x, 0, direction.z).normalized;

		                // 直接把角色位置沿推离方向移动（修正穿透）
		                transform.position += pushDirection * distance;
		            }
		        }
		    }
		}


		// 进入地面状态（角色刚刚落地时调用）
		protected virtual void EnterGround(RaycastHit hit)
		{
		    // 只有当前不是地面状态时才执行（防止重复触发）
		    if (!isGrounded)
		    {
		        // 记录当前地面的射线检测信息（位置、法线等）
		        groundHit = hit;
		        // 标记角色已经在地面上
		        isGrounded = true;
		        // 触发“进入地面”的事件（例如播放落地动画、音效）
		        entityEvents.OnGroundEnter?.Invoke();
		    }
		}

		// 离开地面状态（角色刚刚离开地面时调用）
		protected virtual void ExitGround()
		{
		    // 只有当前在地面状态时才执行
		    if (isGrounded)
		    {
		        // 标记角色不在地面
		        isGrounded = false;
		        // 解除与地面的父子关系（如果站在移动平台上，需要解绑）
		        transform.parent = null;
		        // 记录离开地面的时间（可能用于跳跃缓冲或着陆判断）
		        lastGroundTime = Time.time;
		        // 限制垂直速度：如果正在向下运动，不改变；如果有向上的速度，则保留（防止离地瞬间速度异常）
		        verticalVelocity = Vector3.Max(verticalVelocity, Vector3.zero);
		        // 触发“离开地面”的事件（例如播放起跳动画）
		        entityEvents.OnGroundExit?.Invoke();
		    }
		}

		// 进入轨道状态（通常是进入某种固定路径/轨道，例如滑轨、过山车）
		protected virtual void EnterRail(SplineContainer rails)
		{
		    // 只有当前不在轨道状态时才执行
		    if (!onRails)
		    {
		        // 标记角色进入轨道模式
		        onRails = true;
		        // 保存当前轨道数据引用
		        this.rails = rails;
		        // 触发“进入轨道”的事件（例如锁定角色移动方向）
		        entityEvents.OnRailsEnter.Invoke();
		    }
		}

		// 退出轨道状态
		public virtual void ExitRail()
		{
		    // 只有当前在轨道状态时才执行
		    if (onRails)
		    {
		        // 标记角色不在轨道模式
		        onRails = false;
		        // 触发“退出轨道”的事件（恢复自由移动）
		        entityEvents.OnRailsExit.Invoke();
		    }
		}

		// 更新地面相关数据（每帧更新站立地面的法线、坡度等信息）
		protected virtual void UpdateGround(RaycastHit hit)
		{
		    // 只有当前处于地面状态时才执行
		    if (isGrounded)
		    {
		        // 更新地面射线检测信息
		        groundHit = hit;
		        // 记录地面法线（用于计算坡度方向）
		        groundNormal = groundHit.normal;
		        // 计算当前地面的坡度角（与世界Y轴的夹角）
		        groundAngle = Vector3.Angle(Vector3.up, groundHit.normal);
		        // 计算本地的坡度方向（水平投影后的法线方向）
		        localSlopeDirection = new Vector3(groundNormal.x, 0, groundNormal.z).normalized;
		        // 如果地面是平台（tag = Platform），让角色成为平台的子物体，跟随平台移动
		        // 否则取消父子关系
		        transform.parent = hit.collider.CompareTag(GameTags.Platform) ? hit.transform : null;
		    }
		}


		// 评估是否符合着陆条件
		protected virtual bool EvaluateLanding(RaycastHit hit)
		{
			//slopeLimit是坡度的最大限制角度
			return IsPointUnderStep(hit.point) && Vector3.Angle(hit.normal, Vector3.up) < controller.slopeLimit;
		}

		// 处理坡度限制（空实现，子类重写）
		protected virtual void HandleSlopeLimit(RaycastHit hit) { }

		// 处理高地（空实现，子类重写）
		protected virtual void HandleHighLedge(RaycastHit hit) { }

		protected virtual void OnUpdate() { }

		// 处理接触事件
		protected virtual void OnContact(Collider other)
		{
			if (other)
			{
				states.OnContact(other);
			}
		}

		// 根据输入的方向平滑加速移动
		public virtual void Accelerate(Vector3 direction, float turningDrag, float acceleration, float topSpeed)
		{
			// 判断方向是否有效（不为零向量）
			if (direction.sqrMagnitude > 0)
			{
				// 计算当前速度在目标方向上的投影速度（标量）
				var speed = Vector3.Dot(direction, lateralVelocity);
				// 计算当前速度在目标方向上的向量部分
				var velocity = direction * speed;
				// 计算当前速度中垂直于目标方向的部分（转向速度）
				var turningVelocity = lateralVelocity - velocity;
				// 计算转向阻力对应的速度变化量（根据转向阻力系数和时间增量）
				var turningDelta = turningDrag * turningDragMultiplier * Time.deltaTime;
				// 计算最大允许速度（考虑速度倍率）
				var targetTopSpeed = topSpeed * topSpeedMultiplier;

				// 如果当前速度未达最大速度，或当前速度与目标方向相反，则加速
				if (lateralVelocity.magnitude < targetTopSpeed || speed < 0)
				{
					// 增加速度，受加速度倍率和时间影响
					speed += acceleration * accelerationMultiplier * Time.deltaTime;
					// 限制速度在[-最大速度, 最大速度]范围内
					speed = Mathf.Clamp(speed, -targetTopSpeed, targetTopSpeed);
				}

				// 重新计算目标方向速度向量
				velocity = direction * speed;
				// 使用 Vector3.MoveTowards 将转向速度平滑减小到零向量，实现自然的转向过渡
				// 参数说明：
				//   - turningVelocity：当前转向速度向量（垂直于目标方向的速度分量）
				//   - Vector3.zero：目标值（完全停止转向）
				//   - turningDelta：每帧最大移动距离（转向阻力 × 时间增量）
				// 作用：每帧最多减少 turningDelta 的速度量，保证转向不会瞬间停止，形成平滑的转向动画
				// 类似线性插值，但每帧步长固定，不会因时间变化而改变动画速度
				turningVelocity = Vector3.MoveTowards(turningVelocity, Vector3.zero, turningDelta);
				// 更新横向速度为目标方向速度与转向速度之和
				lateralVelocity = velocity + turningVelocity;
			}
		}


		// 平滑减速，速度逐渐趋近于 0（水平速度减速）
		public virtual void Decelerate(float deceleration)
		{
		    // 计算本帧的减速度（decelerationMultiplier 可用于调节全局减速效果）
		    var delta = deceleration * decelerationMultiplier * Time.deltaTime;
		    // 将 lateralVelocity（水平速度向量）逐渐插值到 Vector3.zero（完全停止）
		    // 第三个参数是本帧允许的最大速度变化量
		    lateralVelocity = Vector3.MoveTowards(lateralVelocity, Vector3.zero, delta);
		}


		// 应用重力加速度（仅在空中时生效）
		public virtual void Gravity(float gravity)
		{
		    // 如果没有接触地面（在空中）
		    if (!isGrounded)
		    {
		        // 给垂直速度 verticalVelocity 叠加重力向量（Vector3.down）
		        // gravityMultiplier 用于调整重力强度
		        verticalVelocity += Vector3.down * gravity * gravityMultiplier * Time.deltaTime;
		    }
		}


		// 根据斜坡角度调整速度（模拟上坡减速、下坡加速）
		public virtual void SlopeFactor(float upwardForce, float downwardForce)
		{
		    // 必须接触地面，且当前地面是斜坡，才进行处理
		    if (!isGrounded || !OnSlopingGround()) return;

		    // factor 表示斜坡倾斜程度（法线越接近 Vector3.up，factor 越接近 1）
		    var factor = Vector3.Dot(Vector3.up, groundNormal);
		    // 检查当前水平速度是否沿着斜坡向下
		    var downwards = Vector3.Dot(localSlopeDirection, lateralVelocity) > 0;
		    // 根据方向选择上坡力或下坡力
		    var multiplier = downwards ? downwardForce : upwardForce;
		    // 计算本帧速度调整量
		    var delta = factor * multiplier * Time.deltaTime;

		    // 在 localSlopeDirection 方向上增加或减少速度
		    lateralVelocity += localSlopeDirection * delta;
		}


		// 将角色吸附到地面（防止悬空）
		public virtual void SnapToGround(float force)
		{
		    // 只有接触地面，且垂直速度是向下的（y <= 0）才生效
		    if (isGrounded && (verticalVelocity.y <= 0))
		    {
		        // 将垂直速度设置为一个恒定向下的力（防止离地浮空）
		        verticalVelocity = Vector3.down * force;
		    }
		}


		// 让角色立即朝向某个方向（瞬间转向）
		public virtual void FaceDirection(Vector3 direction)
		{
		    // 如果方向向量有效（不是零向量）
		    if (direction.sqrMagnitude > 0)
		    {
		        // 生成一个面向 direction 方向的旋转（保持世界Y轴为上）
		        var rotation = Quaternion.LookRotation(direction, Vector3.up);

		        // 直接设置物体的旋转
		        transform.rotation = rotation;
		    }
		}


		// 让角色按一定旋转速度朝向某个方向（平滑转向）
		public virtual void FaceDirection(Vector3 direction, float degreesPerSecond)
		{
		    // 必须是有效的方向
		    if (direction != Vector3.zero)
		    {
		        // 当前旋转
		        var rotation = transform.rotation;
		        // 本帧允许的最大旋转角度（受 Time.deltaTime 影响）
		        var rotationDelta = degreesPerSecond * Time.deltaTime;
		        // 目标旋转
		        var target = Quaternion.LookRotation(direction, Vector3.up);
		        // 按最大旋转速度逐渐逼近目标旋转
		        transform.rotation = Quaternion.RotateTowards(rotation, target, rotationDelta);
		    }
		}


		// 判断实体碰撞器是否能放入指定位置（是否与其他碰撞体重叠）
		public virtual bool FitsIntoPosition(Vector3 position)
		{
			var radius = controller.radius - controller.skinWidth;
			var offset = height * 0.5f - radius;
			var top = position + Vector3.up * offset;
			var bottom = position - Vector3.up * offset;

			return !Physics.CheckCapsule(top, bottom, radius,
				Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
		}

		// 启用或禁用自定义碰撞，禁用角色控制器，启用自定义碰撞组件
		public virtual void UseCustomCollision(bool value)
		{
			controller.enabled = !value;

			if (value)
			{
				InitializeCollider();
				InitializeRigidbody();
			}
			else
			{
				Destroy(m_collider);
				Destroy(m_rigidbody);
			}
		}
	}
}
