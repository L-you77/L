using System.Collections.Generic;
using Godot;

/// <summary>
/// 玩家（箭头）。
///
/// 玩法：横版跳跃平台，没有冲刺。
///   贴图：按当前速度的 X / Y 方向切 8 张箭头图（含 4 个斜向），静止时播 idle 动画（JiaZai 序列帧）。
///   手感：加速度/摩擦、土狼时间（Coyote Time）、跳跃缓冲（Jump Buffer）、松键小跳。
///   战斗：血量 + 受击无敌帧与闪烁 + 击退；从上方踩到敌人会反弹并消灭它；
///         J 近战（NearHit，冷却 3 秒）、U 远程（FarHit，冷却 5 秒）。
///   梯子：站进 tiZi 区域后不吃重力，按 上/下 攀爬；**连按两次跳跃键可以跳离梯子**。
///   死亡：掉出地图或血量归零，等 UI 的重来按钮。
/// </summary>
public partial class Player : CharacterBody2D, IDamageable
{
	[ExportGroup("移动")]

	/// <summary>最大水平速度（像素/秒）。</summary>
	[Export] public float MoveSpeed = 90f;

	/// <summary>地面加速度。</summary>
	[Export] public float GroundAcceleration = 900f;

	/// <summary>空中加速度。</summary>
	[Export] public float AirAcceleration = 600f;

	/// <summary>地面松开按键后的减速。</summary>
	[Export] public float GroundFriction = 1200f;

	/// <summary>空中松开按键后的减速（很小，保留一点惯性）。</summary>
	[Export] public float AirFriction = 300f;

	[ExportGroup("跳跃")]

	/// <summary>起跳初速度。跳起高度 ≈ JumpForce² / (2 × Gravity)，配 Gravity=500 时 150 约跳 22 像素、250 约跳 62 像素。</summary>
	[Export] public float JumpForce = 150f;

	/// <summary>重力加速度。</summary>
	[Export] public float Gravity = 500f;

	/// <summary>最大下落速度，防止穿模。</summary>
	[Export] public float MaxFallSpeed = 320f;

	/// <summary>上升途中松开跳跃键时，纵向速度乘以这个系数（做小跳）。</summary>
	[Export] public float JumpCutMultiplier = 0.45f;

	/// <summary>土狼时间：刚离开地面仍可起跳的宽限。</summary>
	[Export] public float CoyoteTime = 0.1f;

	/// <summary>跳跃缓冲：落地前按下的跳跃会被记住。</summary>
	[Export] public float JumpBufferTime = 0.12f;

	[ExportGroup("战斗")]

	/// <summary>最大血量。</summary>
	[Export] public int MaxHealth = 3;

	/// <summary>受击后的无敌时长。</summary>
	[Export] public float InvincibleTime = 1.0f;

	/// <summary>
	/// 踩中敌人后的额外无敌时长。默认 0：踩踏和被敌人扎到会在同一帧一起结算（掉血也照算）。
	/// 想让"踩敌人"变成安全操作，就调成 0.2~0.3。
	/// </summary>
	[Export] public float StompInvincibleTime = 0f;

	/// <summary>踩中敌人后的反弹速度。</summary>
	[Export] public float StompBounce = 210f;

	/// <summary>受击击退（X 为水平速度，Y 为向上的速度，注意是负数）。</summary>
	[Export] public Vector2 DamageKnockback = new Vector2(90f, -140f);

	[ExportGroup("近战攻击")]

	/// <summary>近战冷却（秒）。</summary>
	[Export] public float NearHitCooldown = 3f;

	/// <summary>近战判定窗口（秒），只要这一小段时间里碰到敌人就算打中。</summary>
	[Export] public float NearHitDuration = 0.3f;

	/// <summary>近战伤害。</summary>
	[Export] public int NearHitDamage = 1;

	/// <summary>近战判定盒相对角色中心往前挪的距离（像素）。</summary>
	[Export] public float NearHitReach = 5f;

	[ExportGroup("远程攻击")]

	/// <summary>远程冷却（秒）。</summary>
	[Export] public float FarHitCooldown = 5f;

	/// <summary>
	/// 弹体速度 = 玩家当前水平移动速度 × 这个倍率。
	/// 1.5 表示"子弹比角色快 50%"（默认值），改它就能统一调整手感。
	/// </summary>
	[Export] public float FarHitSpeedFactor = 1.5f;

	/// <summary>弹体伤害。</summary>
	[Export] public int FarHitDamage = 1;

	/// <summary>弹体场景路径。</summary>
	[Export] public string FarHitScenePath = "res://node/organ/far_hit.tscn";

	[ExportGroup("强化")]

	/// <summary>
	/// 开局是否已经拥有近战/远程武器。
	/// 教学关卡用 player2.tscn 把它关掉，玩家必须先点自己三选一、拿到武器才能攻击。
	/// </summary>
	[Export] public bool UnlockAttacksAtStart = true;

	/// <summary>每次"加速"强化给的速度提升比例（0.15 = +15%）。左右各自独立累计。</summary>
	[Export] public float SpeedUpPerLevel = 0.15f;

	/// <summary>每次"跳跃"强化给的高度提升比例（0.12 = +12%）。</summary>
	[Export] public float JumpUpPerLevel = 0.12f;

	/// <summary>每次"攻速"强化让攻击间隔缩短的比例（0.15 = -15%）。</summary>
	[Export] public float CooldownDownPerLevel = 0.15f;

	/// <summary>每次"伤害"强化增加的伤害点数。</summary>
	[Export] public int DamageUpPerLevel = 1;

	[ExportGroup("梯子")]

	/// <summary>爬梯速度（像素/秒）。</summary>
	/// <summary>梯子上连续按两次跳跃键、两次之间最长的间隔（秒）。</summary>
	[Export] public float LadderJumpWindow = 0.35f;

	/// <summary>从梯子上跳起后，多久之内不再重新抓住梯子（否则会被判定回梯子上、原地卡住）。</summary>
	[Export] public float LadderJumpLockTime = 0.3f;

	[Export] public float LadderClimbSpeed = 40f;

	[ExportGroup("跑酷（无限模式）")]

	/// <summary>自动向右跑（无限模式用，不需要按方向键）。</summary>
	[Export] public bool AutoRun = false;

	/// <summary>允许按 ↓ 蹲下（身体判定高度减半）。其它模式关掉，免得和梯子抢 ↓。</summary>
	[Export] public bool EnableCrouch = false;

	/// <summary>蹲下时身体判定高度相对站立的比例。</summary>
	[Export] public float CrouchHeightScale = 0.5f;

	/// <summary>按 ↓ 时往下瞬移一小段（跑酷用）。</summary>
	[Export] public bool EnableDownTeleport = false;

	/// <summary>瞬移距离（像素）。</summary>
	[Export] public float DownTeleportDistance = 24f;

	/// <summary>瞬移冷却（秒）。</summary>
	[Export] public float DownTeleportCooldown = 0.35f;

	[ExportGroup("相机")]
	/// <summary>镜头是否限制在关卡 TileMapLayer 范围内（视野不会露出地图外面）。</summary>
	[Export] public bool LimitCameraToTileMap = true;

	/// <summary>镜头范围额外放宽多少像素（0 = 严格贴合地形边界）。</summary>
	[Export] public int CameraLimitPadding = 0;

	/// <summary>关卡里地形层的节点名（相机范围按它的实际使用区域算）。</summary>
	private const string TileMapLayerName = "TileMapLayer";

	[ExportGroup("死亡与重生")]

	/// <summary>
	/// 纵坐标超过这个值算摔死（掉出可玩区域）。
	/// 当前关卡：主地面 y=240，坑在 x=512~536，下方地下层地板 y=416~448（最底边 448）。
	/// 地下层是要玩的区域，所以死亡线放在整张地图外（470）——正常游玩不会再触发，
	/// 只作为"以后在地图上挖了洞"的兜底。
	/// </summary>
	[Export] public float FallDeathY = 470f;

	/// <summary>死亡后到重开的等待时间。</summary>
	[Export] public float RespawnDelay = 0.6f;

	/// <summary>true = 死亡后整关重开；false = 原地满血复活（WaitForRestart 关掉时才生效）。</summary>
	[Export] public bool ReloadSceneOnDeath = true;

	/// <summary>
	/// 死亡后一直保持死亡状态，等 UI 的重来按钮调用 <see cref="Restart"/>。
	/// 关掉则回到旧的"等 RespawnDelay 后自动重开/复活"。UI 上有重来按钮时要保持 true，否则会自己重开、按钮来不及点。
	/// </summary>
	[Export] public bool WaitForRestart = true;

	[ExportGroup("受击表现")]

	/// <summary>受击闪红的停留时长。</summary>
	[Export] public float HurtFlashTime = 0.12f;

	/// <summary>受击闪红颜色。</summary>
	[Export] public Color HurtColor = new Color(1f, 0.35f, 0.35f);

	public int Health { get; private set; }
	public bool IsAlive => Health > 0;
	public bool IsInvincible => _invincibleTimer > 0f;

	/// <summary>向右移动速度倍率（"向右加速"强化累加）。</summary>
	public float RightSpeedMultiplier { get; private set; } = 1f;

	/// <summary>向左移动速度倍率（"向左加速"强化累加）。</summary>
	public float LeftSpeedMultiplier { get; private set; } = 1f;

	/// <summary>向下爬梯速度倍率（"向下加速"强化累加）。</summary>
	public float DownSpeedMultiplier { get; private set; } = 1f;

	/// <summary>跳跃初速度倍率（"跳得更高"强化累加）。</summary>
	public float JumpMultiplier { get; private set; } = 1f;

	/// <summary>额外的空中跳跃次数（"空中跳跃"强化累加）。</summary>
	public int AirJumpCount { get; private set; }

	/// <summary>本次离地后还剩几次空中跳（落地或在梯子上时补满）。</summary>
	public int AirJumpsLeft { get; private set; }

	/// <summary>是否已经拿到近战武器（没有时按 J 无效）。</summary>
	public bool HasNearHit { get; private set; }

	/// <summary>是否已经拿到远程武器（没有时按 U 无效）。</summary>
	public bool HasFarHit { get; private set; }

	/// <summary>玩家节点所在的分组，UI 靠它找到玩家（不用在编辑器里连 NodePath）。</summary>
	public const string GroupName = "player";

	/// <summary>已收集的金币数。只存在内存里，重开关卡会归零。</summary>
	public int Coins { get; private set; }

	/// <summary>
	/// 还能"点击自己"弹出几次三选一强化。
	/// 每个 coinSpe 给 1 次（见 <see cref="AddUpgradeCharge"/>），教学关卡靠它控制强化次数。
	/// </summary>
	public int UpgradeCharges { get; private set; }

	[Signal] public delegate void HealthChangedEventHandler(int health, int maxHealth);
	[Signal] public delegate void CoinChangedEventHandler(int coins);
	[Signal] public delegate void UpgradeChargeChangedEventHandler(int charges);
	[Signal] public delegate void DiedEventHandler();

	private static readonly StringName AnimIdle = "idle";
	private static readonly StringName AnimUp = "up";
	private static readonly StringName AnimDown = "down";
	private static readonly StringName AnimLeft = "left";
	private static readonly StringName AnimRight = "right";
	private static readonly StringName AnimLeftUp = "left_up";
	private static readonly StringName AnimRightUp = "right_up";
	private static readonly StringName AnimLeftDown = "left_down";
	private static readonly StringName AnimRightDown = "right_down";

	private AnimatedSprite2D _sprite;
	private CollisionShape2D _bodyShape;

	// 蹲下：站立/蹲下两套形状来回换（不直接改共享资源，先 Duplicate 一份）
	private RectangleShape2D _standShape;
	private RectangleShape2D _crouchShape;
	private Vector2 _standShapePos;
	private Vector2 _spriteBaseScale = Vector2.One;
	private bool _crouching;
	private float _downTeleportCd;
	private HurtBox _hurtBox;
	private HitBox _stompBox;

	private Vector2 _spawnPosition;
	private float _coyoteTimer;
	private float _jumpBufferTimer;
	private float _invincibleTimer;
	private float _hurtFlashTimer;
	private float _deathTimer;
	private bool _jumpNotCut;   // 本次起跳是否还没被“松键”截断
	private bool _isDead;

	// 战斗
	private int _facing = 1;        // 1 = 朝右，-1 = 朝左
	private float _nearHitCd;
	private float _nearHitWindow;
	private float _farHitCd;
	private Node2D _nearHitPivot;
	private Sprite2D _nearHitSprite;
	private AnimationPlayer _nearHitAnim;
	private HitBox _nearHitBox;
	private CollisionShape2D _nearHitShape;
	private PackedScene _farHitScene;

	// 梯子
	private readonly List<TiZi> _ladders = new();

	/// <summary>梯子上连按跳：距离上一次按跳还剩多久算"连上"。</summary>
	private float _ladderTapTimer;

	/// <summary>梯子上连按跳：已经按了几下。</summary>
	private int _ladderTaps;

	/// <summary>&gt; 0 时暂时不算站在梯子上（从梯子上跳起来后的短暂锁定）。</summary>
	private float _ladderIgnoreTimer;

	/// <summary>是否站在梯子上（跳离梯子的短暂锁定期间算"不在"）。</summary>
	public bool IsOnLadder => _ladders.Count > 0 && _ladderIgnoreTimer <= 0f;

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_bodyShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		_hurtBox = GetNodeOrNull<HurtBox>("HurtBox");
		_stompBox = GetNodeOrNull<HitBox>("HitBox");

		ApplyCollisionLayers();
		PrepareCrouch();
		AddToGroup(GroupName);

		Health = MaxHealth;
		_spawnPosition = GlobalPosition;

		// 快速通关成就的计时起点（每次进关卡都会重来一次）
		GameState.Instance?.MarkLevelStart();
		_invincibleTimer = 0f;

		HasNearHit = UnlockAttacksAtStart;
		HasFarHit = UnlockAttacksAtStart;

		if (_stompBox != null)
		{
			_stompBox.HitLanded += OnStompLanded;
		}

		SetUpAttacks();

		// 镜头不越过关卡地形边界
		ApplyCameraLimits();

		// 商城里买的永久强化，在这里一次性打给玩家（没有商城进度时什么也不做）
		GameState.Instance?.ApplyPermanent(this);

		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	/// <summary>
	/// 把 Camera2D 的移动范围限制在关卡 TileMapLayer 用到的区域上：
	/// 镜头推到地图边缘就停住，不会把地图外面的空白露出来。
	///
	/// 判定方式：玩家所在关卡（父节点）下**名字叫 TileMapLayer** 的节点即为地图范围，
	/// 所以每个有地形层的关卡都自动生效，不需要逐关配置。
	/// </summary>
	private void ApplyCameraLimits()
	{
		if (!LimitCameraToTileMap)
		{
			return;
		}

		if (GetNodeOrNull<Camera2D>("Camera2D") is not Camera2D camera)
		{
			return;
		}

		if (GetParent()?.GetNodeOrNull<TileMapLayer>(TileMapLayerName) is not TileMapLayer map)
		{
			return;
		}

		Rect2I used = map.GetUsedRect();
		if (used.Size.X <= 0 || used.Size.Y <= 0)
		{
			return;
		}

		Vector2I tile = map.TileSet?.TileSize ?? new Vector2I(8, 8);
		int originX = Mathf.RoundToInt(map.GlobalPosition.X);
		int originY = Mathf.RoundToInt(map.GlobalPosition.Y);

		camera.LimitLeft = originX + used.Position.X * tile.X - CameraLimitPadding;
		camera.LimitTop = originY + used.Position.Y * tile.Y - CameraLimitPadding;
		camera.LimitRight = originX + (used.Position.X + used.Size.X) * tile.X + CameraLimitPadding;
		camera.LimitBottom = originY + (used.Position.Y + used.Size.Y) * tile.Y + CameraLimitPadding;

		GD.Print($"[Player] 相机限制到 TileMapLayer：({camera.LimitLeft}, {camera.LimitTop}) ~ ({camera.LimitRight}, {camera.LimitBottom})");
	}

	/// <summary>
	/// 碰撞层兜底：场景里也已经设置好，这里再写一遍是为了避免编辑器里被误改后出现“莫名不受伤”的排查痛苦。
	/// 想只靠编辑器管理的话，把这次调用注释掉即可。
	/// </summary>
	private void ApplyCollisionLayers()
	{
		CollisionLayer = GameLayers.PlayerBody;
		CollisionMask = GameLayers.WorldMask;

		if (_hurtBox != null)
		{
			_hurtBox.CollisionLayer = GameLayers.PlayerHurt;
			_hurtBox.CollisionMask = GameLayers.EnemyAttack;
		}

		if (_stompBox != null)
		{
			_stompBox.CollisionLayer = GameLayers.PlayerAttack;
			_stompBox.CollisionMask = GameLayers.EnemyHurt;
			_stompBox.IsStomp = true;
		}
	}

	/// <summary>
	/// 按 ↓：向下瞬移 <see cref="DownTeleportDistance"/> 像素（跑酷模式）。
	/// 用 TestMove 先探一下，下面有地面/平台就不瞬移 —— 所以站在地上按 ↓ 只会蹲下，不会掉进地里。
	/// </summary>
	private void TryDownTeleport(float dt)
	{
		_downTeleportCd = Mathf.Max(0f, _downTeleportCd - dt);

		if (!EnableDownTeleport || _downTeleportCd > 0f || !Input.IsActionJustPressed("Down"))
		{
			return;
		}

		Vector2 motion = new(0f, DownTeleportDistance);
		if (TestMove(GlobalTransform, motion))
		{
			return;
		}

		GlobalPosition += motion;
		_downTeleportCd = DownTeleportCooldown;
		GD.Print("[Player] 向下瞬移");
	}

	/// <summary>蹲下用：按 <see cref="CrouchHeightScale"/> 复制一份矮一点的形状（不动原资源）。</summary>
	private void PrepareCrouch()
	{
		if (!EnableCrouch || _bodyShape?.Shape is not RectangleShape2D stand)
		{
			return;
		}

		_standShape = stand;
		_standShapePos = _bodyShape.Position;
		_spriteBaseScale = _sprite?.Scale ?? Vector2.One;

		_crouchShape = (RectangleShape2D)stand.Duplicate();
		_crouchShape.Size = new Vector2(stand.Size.X, Mathf.Max(1f, stand.Size.Y * CrouchHeightScale));
	}

	/// <summary>按住 ↓ 且站在地面上时蹲下：判定盒从顶部压矮，脚底位置不变。</summary>
	private void UpdateCrouch(bool canCrouch)
	{
		if (_crouchShape == null || _bodyShape == null)
		{
			return;
		}

		bool want = canCrouch && Input.IsActionPressed("Down");
		if (want == _crouching)
		{
			return;
		}

		_crouching = want;

		_bodyShape.Shape = want ? _crouchShape : _standShape;

		// 形状是从中心缩的，往下挪半个高度差，脚底才不会浮起来
		float drop = (_standShape.Size.Y - _crouchShape.Size.Y) * 0.5f;
		_bodyShape.Position = _standShapePos + new Vector2(0f, want ? drop : 0f);

		if (_sprite != null)
		{
			_sprite.Scale = want ? new Vector2(_spriteBaseScale.X, _spriteBaseScale.Y * 0.6f) : _spriteBaseScale;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (_invincibleTimer > 0f)
		{
			_invincibleTimer = Mathf.Max(0f, _invincibleTimer - dt);
		}
		if (_hurtFlashTimer > 0f)
		{
			_hurtFlashTimer = Mathf.Max(0f, _hurtFlashTimer - dt);
		}
		UpdateSpriteTint();

		// --- 攻击冷却 / 近战判定窗口 ---
		_nearHitCd = Mathf.Max(0f, _nearHitCd - dt);
		_farHitCd = Mathf.Max(0f, _farHitCd - dt);
		if (_nearHitWindow > 0f)
		{
			_nearHitWindow -= dt;
			if (_nearHitWindow <= 0f)
			{
				_nearHitWindow = 0f;
				EndNearHit();
			}
		}

		if (_isDead)
		{
			// 死亡后定格当前贴图；倒计时结束后只处理一次（等重来按钮或自动重开）
			Velocity = Vector2.Zero;
			if (_deathTimer > 0f)
			{
				_deathTimer -= dt;
				if (_deathTimer <= 0f)
				{
					_deathTimer = 0f;
					FinishDeath();
				}
			}
			return;
		}

		// --- 攻击输入 ---
		if (Input.IsActionJustPressed("NearHit"))
		{
			TryNearHit();
		}
		if (Input.IsActionJustPressed("FarHit"))
		{
			TryFarHit();
		}

		Vector2 velocity = Velocity;
		bool onFloor = IsOnFloor();

		float inputX = AutoRun ? 1f : Input.GetAxis("Left", "Right");

		// 朝向：按输入方向立刻翻转，站着不动时保留上一次朝向（攻击要靠它决定往哪打）
		if (inputX > 0.01f)
		{
			_facing = 1;
		}
		else if (inputX < -0.01f)
		{
			_facing = -1;
		}

		bool jumpPressed = Input.IsActionJustPressed("Up");
		bool jumpHeld = Input.IsActionPressed("Up");

		// --- 计时器：土狼时间 / 跳跃缓冲 ---
		_coyoteTimer = onFloor ? CoyoteTime : Mathf.Max(0f, _coyoteTimer - dt);
		_jumpBufferTimer = jumpPressed ? JumpBufferTime : Mathf.Max(0f, _jumpBufferTimer - dt);

		// --- 水平移动（左右速度倍率各自独立，来自"向右/向左加速"强化） ---
		if (Mathf.Abs(inputX) > 0.01f)
		{
			float accel = onFloor ? GroundAcceleration : AirAcceleration;
			float speedMultiplier = inputX > 0f ? RightSpeedMultiplier : LeftSpeedMultiplier;
			velocity.X = Mathf.MoveToward(velocity.X, inputX * MoveSpeed * speedMultiplier, accel * dt);
		}
		else
		{
			float friction = onFloor ? GroundFriction : AirFriction;
			velocity.X = Mathf.MoveToward(velocity.X, 0f, friction * dt);
		}

		// --- 纵向：梯子上不吃重力，按 上/下 攀爬；否则正常重力 ---
		_ladderIgnoreTimer = Mathf.Max(0f, _ladderIgnoreTimer - dt);
		bool onLadder = IsOnLadder;
		if (onLadder)
		{
			// GetAxis("Up", "Down")：上 = -1，下 = +1；不按键就停在梯子上
			float climbInput = Input.GetAxis("Up", "Down");
			// "向下加速"强化只加快往下爬，往上爬速度不变
			float climbSpeed = LadderClimbSpeed * (climbInput > 0f ? DownSpeedMultiplier : 1f);
			velocity.Y = climbInput * climbSpeed;
		}
		else if (!onFloor)
		{
			velocity.Y = Mathf.Min(velocity.Y + Gravity * dt, MaxFallSpeed);
		}

		// --- 蹲下（跑酷模式）：身体判定高度减半，用来钻过矮杆 ---
		UpdateCrouch(onFloor && !onLadder);

		// --- 落地 / 在梯子上：把空中跳补满 ---
		if (onFloor || onLadder)
		{
			AirJumpsLeft = AirJumpCount;
		}

		// --- 梯子上连按两次跳跃键：跳离梯子 ---
		// 梯子上 ↑ 同时是"往上爬"，所以单按只爬；在 LadderJumpWindow 内再按一次才算起跳。
		if (onLadder)
		{
			if (jumpPressed)
			{
				_ladderTaps = _ladderTapTimer > 0f ? _ladderTaps + 1 : 1;
				_ladderTapTimer = LadderJumpWindow;
			}
			else
			{
				_ladderTapTimer = Mathf.Max(0f, _ladderTapTimer - dt);
			}

			if (_ladderTaps >= 2)
			{
				_ladderTaps = 0;
				_ladderTapTimer = 0f;

				// 跳起来后短暂"不算在梯子上"，否则下一帧又被梯子抓住、原地卡住
				_ladderIgnoreTimer = LadderJumpLockTime;

				velocity.Y = -JumpForce * JumpMultiplier;
				_jumpNotCut = true;
				_jumpBufferTimer = 0f;
				onLadder = false;

				GD.Print("[Player] 梯子上起跳");
			}
		}
		else
		{
			_ladderTaps = 0;
			_ladderTapTimer = 0f;
		}

		// --- 起跳（在梯子上 Up 是往上爬，不触发跳跃） ---
		if (!onLadder && _jumpBufferTimer > 0f && _coyoteTimer > 0f)
		{
			velocity.Y = -JumpForce * JumpMultiplier;
			_jumpNotCut = true;
			_jumpBufferTimer = 0f;
			_coyoteTimer = 0f;
		}
		else if (!onLadder && _jumpBufferTimer > 0f && AirJumpsLeft > 0)
		{
			// 空中跳（"空中跳跃"强化）：重力已经拉下来的速度直接重置成起跳速度
			AirJumpsLeft--;
			velocity.Y = -JumpForce * JumpMultiplier;
			_jumpNotCut = true;
			_jumpBufferTimer = 0f;
		}

		// --- 松键小跳 ---
		if (_jumpNotCut && velocity.Y < 0f && !jumpHeld)
		{
			velocity.Y *= JumpCutMultiplier;
			_jumpNotCut = false;
		}
		if (velocity.Y >= 0f)
		{
			_jumpNotCut = false;
		}

		// --- 按 ↓ 往下瞬移（跑酷模式） ---
		TryDownTeleport(dt);

		Velocity = velocity;
		MoveAndSlide();

		UpdateAnimation(Velocity);

		// --- 摔出地图 ---
		if (GlobalPosition.Y > FallDeathY)
		{
			Die();
		}
	}

	/// <summary>受到伤害（由 HurtBox 自动调用，也可被陷阱等直接调用）。</summary>
	public void TakeDamage(int amount, HitBox source)
	{
		if (!IsAlive || IsInvincible || amount <= 0)
		{
			return;
		}

		Health = Mathf.Max(0, Health - amount);
		_invincibleTimer = InvincibleTime;
		_hurtFlashTimer = HurtFlashTime;
		_jumpNotCut = false;

		// 从伤害源的反方向被弹开；取不到来源时默认往左弹
		float dir = -1f;
		if (source?.OwnerBody is Node2D attacker)
		{
			float offset = GlobalPosition.X - attacker.GlobalPosition.X;
			dir = offset >= 0f ? 1f : -1f;
		}
		Velocity = new Vector2(dir * DamageKnockback.X, DamageKnockback.Y);

		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		GD.Print($"[Player] 受到 {amount} 点伤害，剩余血量 {Health}/{MaxHealth}");

		if (Health <= 0)
		{
			Die();
		}
	}

	/// <summary>治疗（留给后续道具/存档用）。</summary>
	public void Heal(int amount)
	{
		if (!IsAlive || amount <= 0)
		{
			return;
		}

		Health = Mathf.Min(MaxHealth, Health + amount);
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	/// <summary>加金币（由 Coin 碰到玩家时调用）。</summary>
	public void AddCoin(int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		Coins += amount;
		EmitSignal(SignalName.CoinChanged, Coins);
		GD.Print($"[Player] 金币 +{amount}，当前 {Coins}");
	}

	/// <summary>捡到 coinSpe：增加一次强化机会。</summary>
	public void AddUpgradeCharge(int amount = 1)
	{
		if (amount <= 0)
		{
			return;
		}

		UpgradeCharges += amount;
		EmitSignal(SignalName.UpgradeChargeChanged, UpgradeCharges);
		GD.Print($"[Player] 强化次数 +{amount}，当前 {UpgradeCharges}");
	}

	/// <summary>消耗一次强化机会（弹出三选一时调用）。没次数时返回 false。</summary>
	public bool TryConsumeUpgradeCharge()
	{
		if (UpgradeCharges <= 0)
		{
			return false;
		}

		UpgradeCharges--;
		EmitSignal(SignalName.UpgradeChargeChanged, UpgradeCharges);
		return true;
	}

	// ------------------------------------------------------------------
	// 攻击
	// ------------------------------------------------------------------

	/// <summary>把战斗用的节点接好（近战贴图/动画、近战判定盒、弹体场景）。</summary>
	private void SetUpAttacks()
	{
		// 近战：NearHitPivot 负责左右镜像（Scale.X = ±1），里面是贴图 + 挥砍动画
		_nearHitPivot = GetNodeOrNull<Node2D>("NearHitPivot");
		_nearHitSprite = GetNodeOrNull<Sprite2D>("NearHitPivot/NearHit");
		_nearHitAnim = GetNodeOrNull<AnimationPlayer>("NearHitPivot/NearHit/AnimationPlayer");

		if (_nearHitSprite != null)
		{
			_nearHitSprite.Visible = false;
		}
		if (_nearHitAnim != null)
		{
			_nearHitAnim.AnimationFinished += OnNearHitAnimationFinished;
		}

		// 近战判定盒：平时关掉形状，出手那一下才打开
		_nearHitBox = GetNodeOrNull<HitBox>("NearHitBox");
		_nearHitShape = GetNodeOrNull<CollisionShape2D>("NearHitBox/CollisionShape2D");
		if (_nearHitBox != null)
		{
			_nearHitBox.Damage = NearHitDamage;
			_nearHitBox.IsStomp = false;
		}
		if (_nearHitShape != null)
		{
			_nearHitShape.Disabled = true;
		}

		// player.tscn 里的 FarHit 贴图只是摆位置用的参考，真正的弹体由 FarHitScenePath 生成
		if (GetNodeOrNull<Sprite2D>("FarHit") is Sprite2D farHitReference)
		{
			farHitReference.Visible = false;
		}
	}

	/// <summary>近战攻击（J）。冷却中或还没拿到武器时调用会被忽略。</summary>
	public void TryNearHit()
	{
		if (!IsAlive || !HasNearHit || _nearHitCd > 0f)
		{
			return;
		}

		_nearHitCd = NearHitCooldown;
		_nearHitWindow = NearHitDuration;

		// 贴图往朝向镜像
		if (_nearHitPivot != null)
		{
			_nearHitPivot.Scale = new Vector2(_facing, 1f);
		}
		if (_nearHitSprite != null)
		{
			_nearHitSprite.Visible = true;
		}
		if (_nearHitAnim != null)
		{
			_nearHitAnim.Play("nearHit");
		}

		// 判定盒挪到身前并打开
		if (_nearHitBox != null)
		{
			_nearHitBox.Position = new Vector2(_facing * NearHitReach, 0f);
			_nearHitBox.Damage = NearHitDamage;
		}
		if (_nearHitShape != null)
		{
			_nearHitShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
		}

		GD.Print("[Player] 近战攻击");
	}

	/// <summary>近战判定窗口结束：关掉判定盒（贴图等动画播完自己收）。</summary>
	private void EndNearHit()
	{
		if (_nearHitShape != null)
		{
			_nearHitShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}
	}

	private void OnNearHitAnimationFinished(StringName animationName)
	{
		if (_nearHitSprite != null)
		{
			_nearHitSprite.Visible = false;
		}
	}

	/// <summary>远程攻击（U）：朝面朝方向打出一发弹体。冷却中或还没拿到武器时调用会被忽略。</summary>
	public void TryFarHit()
	{
		if (!IsAlive || !HasFarHit || _farHitCd > 0f)
		{
			return;
		}

		if (_farHitScene == null)
		{
			_farHitScene = ResourceLoader.Load<PackedScene>(FarHitScenePath);
			if (_farHitScene == null)
			{
				GD.PushWarning($"[Player] 远程弹体场景加载失败：{FarHitScenePath}");
				return;
			}
		}

		if (_farHitScene.Instantiate() is not FarHitBullet bullet)
		{
			return;
		}

		Node parent = GetParentOrNull<Node>();
		if (parent == null)
		{
			return;
		}

		// 这些要在 AddChild（触发 _Ready）之前设好
		bullet.Direction = _facing;

		// 弹速跟着角色走：角色当前朝这个方向的最大移动速度 × 倍率（默认比角色快 50%）。
		// 用 Mathf.Max 是为了让"刚起步还没加速到满速"时子弹也不会慢得离谱。
		float moveSpeed = MoveSpeed * (_facing > 0 ? RightSpeedMultiplier : LeftSpeedMultiplier);
		bullet.Speed = Mathf.Max(moveSpeed, Mathf.Abs(Velocity.X)) * FarHitSpeedFactor;

		bullet.Damage = FarHitDamage;

		// 挂到关卡上，别挂成玩家的子节点，否则弹体会跟着玩家跑
		parent.AddChild(bullet);
		bullet.GlobalPosition = GlobalPosition + new Vector2(_facing * 4f, 0f);

		_farHitCd = FarHitCooldown;
		GD.Print("[Player] 远程攻击");
	}

	/// <summary>攻击冷却的下限（秒），防止"攻速"强化叠到 0 帧连发。</summary>
	private const float MinAttackCooldown = 0.2f;

	/// <summary>
	/// 应用一次强化（由三选一界面调用）。
	/// </summary>
	public void ApplyUpgrade(UpgradeEffect effect)
	{
		switch (effect)
		{
			case UpgradeEffect.SpeedRight:
				RightSpeedMultiplier += SpeedUpPerLevel;
				break;

			case UpgradeEffect.SpeedLeft:
				LeftSpeedMultiplier += SpeedUpPerLevel;
				break;

			case UpgradeEffect.JumpHeight:
				JumpMultiplier += JumpUpPerLevel;
				break;

			case UpgradeEffect.DownSpeed:
				DownSpeedMultiplier += SpeedUpPerLevel;
				break;

			case UpgradeEffect.AirJump:
				AirJumpCount++;
				AirJumpsLeft = AirJumpCount;
				break;

			case UpgradeEffect.MaxHealth:
				MaxHealth += 1;
				Health = Mathf.Min(MaxHealth, Health + 1);
				EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
				break;

			case UpgradeEffect.UnlockNearHit:
				HasNearHit = true;
				break;

			case UpgradeEffect.NearHitDamage:
				NearHitDamage += DamageUpPerLevel;
				break;

			case UpgradeEffect.NearHitSpeed:
				NearHitCooldown = Mathf.Max(MinAttackCooldown, NearHitCooldown * (1f - CooldownDownPerLevel));
				break;

			case UpgradeEffect.UnlockFarHit:
				HasFarHit = true;
				break;

			case UpgradeEffect.FarHitDamage:
				FarHitDamage += DamageUpPerLevel;
				break;

			case UpgradeEffect.FarHitSpeed:
				FarHitCooldown = Mathf.Max(MinAttackCooldown, FarHitCooldown * (1f - CooldownDownPerLevel));
				break;
		}

		GD.Print($"[Player] 强化生效：{effect}");
	}

	// ------------------------------------------------------------------
	// 梯子
	// ------------------------------------------------------------------

	/// <summary>进入梯子区域（由 TiZi 调用）。</summary>
	public void EnterLadder(TiZi ladder)
	{
		if (ladder == null || _ladders.Contains(ladder))
		{
			return;
		}

		_ladders.Add(ladder);
	}

	/// <summary>离开梯子区域（由 TiZi 调用）。</summary>
	public void ExitLadder(TiZi ladder)
	{
		_ladders.Remove(ladder);
	}

	private void Die()
	{
		if (_isDead)
		{
			return;
		}

		_isDead = true;
		Health = 0;
		Velocity = Vector2.Zero;
		_deathTimer = RespawnDelay;

		if (_bodyShape != null)
		{
			_bodyShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}
		if (_hurtBox != null)
		{
			_hurtBox.SetDeferred(Area2D.PropertyName.Monitorable, false);
		}
		if (_stompBox != null)
		{
			_stompBox.SetDeferred(Area2D.PropertyName.Monitoring, false);
		}

		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		EmitSignal(SignalName.Died);
		GD.Print("[Player] 死亡");
	}

	/// <summary>死亡倒计时结束：等 UI 来点重来，或按设置自动重开/复活。</summary>
	private void FinishDeath()
	{
		if (WaitForRestart)
		{
			return; // 保持死亡状态，等 UI 的重来按钮
		}

		if (!ReloadSceneOnDeath)
		{
			Respawn();
			return;
		}

		Restart();
	}

	/// <summary>
	/// 重开本关（UI 的"重来"按钮调用）。重开失败时兜底原地复活，避免卡在死亡状态。
	/// </summary>
	public void Restart()
	{
		GetTree().Paused = false;

		Error error = GetTree().ReloadCurrentScene();
		if (error != Error.Ok)
		{
			GD.PushWarning($"[Player] 重开关卡失败（{error}），改为原地复活");
			Respawn();
		}
	}

	/// <summary>原地复活（ReloadSceneOnDeath 关掉时走这条）。</summary>
	private void Respawn()
	{
		_isDead = false;
		Health = MaxHealth;
		GlobalPosition = _spawnPosition;
		Velocity = Vector2.Zero;
		_invincibleTimer = InvincibleTime;
		_coyoteTimer = 0f;
		_jumpBufferTimer = 0f;

		if (_bodyShape != null)
		{
			_bodyShape.Disabled = false;
		}
		if (_hurtBox != null)
		{
			_hurtBox.Monitorable = true;
		}
		if (_stompBox != null)
		{
			_stompBox.Monitoring = true;
		}

		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	/// <summary>踩踏成功：反弹 + 短无敌。</summary>
	private void OnStompLanded(HurtBox target)
	{
		if (_isDead)
		{
			return;
		}

		Velocity = new Vector2(Velocity.X, -StompBounce);
		_invincibleTimer = Mathf.Max(_invincibleTimer, StompInvincibleTime);
		_jumpNotCut = false;
	}

	/// <summary>按速度方向挑选 8 方向箭头贴图；完全静止时播 idle（JiaZai 序列帧）。</summary>
	private void UpdateAnimation(Vector2 velocity)
	{
		if (_sprite == null)
		{
			return;
		}

		int dirX = 0;
		if (velocity.X > 6f)
		{
			dirX = 1;
		}
		else if (velocity.X < -6f)
		{
			dirX = -1;
		}

		// 只有离地时才看纵向，地面上的微小抖动不算方向
		int dirY = 0;
		if (!IsOnFloor())
		{
			if (velocity.Y < -8f)
			{
				dirY = -1;
			}
			else if (velocity.Y > 8f)
			{
				dirY = 1;
			}
		}

		StringName anim = (dirX, dirY) switch
		{
			(0, 0) => AnimIdle,
			(0, -1) => AnimUp,
			(0, 1) => AnimDown,
			(1, 0) => AnimRight,
			(-1, 0) => AnimLeft,
			(1, -1) => AnimRightUp,
			(-1, -1) => AnimLeftUp,
			(1, 1) => AnimRightDown,
			_ => AnimLeftDown,
		};

		if (_sprite.Animation != anim)
		{
			_sprite.Play(anim);
		}
	}

	/// <summary>
	/// 整体染色：刚受伤闪红 → 之后的无敌时间半透明闪烁 → 其余恢复白色。
	/// 染的是玩家**根节点**（Modulate 会向下叠加），这样箭头贴图和 Watcher 里的白块/眼睛会一起变色。
	/// 只染 AnimatedSprite2D 的话，眼睛出现后白块会把闪红盖住，看起来像"没闪红"。
	/// </summary>
	private void UpdateSpriteTint()
	{
		if (_hurtFlashTimer > 0f)
		{
			Modulate = HurtColor;
		}
		else if (_invincibleTimer > 0f)
		{
			float alpha = Mathf.Sin(_invincibleTimer * 40f) > 0f ? 0.3f : 1f;
			Modulate = new Color(1f, 1f, 1f, alpha);
		}
		else if (Modulate != Colors.White)
		{
			Modulate = Colors.White;
		}
	}
}
