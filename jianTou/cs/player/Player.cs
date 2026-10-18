using Godot;

/// <summary>
/// 玩家（箭头）。
///
/// 玩法：横版跳跃平台，没有冲刺。
///   贴图：按当前速度的 X / Y 方向切 8 张箭头图（含 4 个斜向），静止时播 idle 动画（JiaZai 序列帧）。
///   手感：加速度/摩擦、土狼时间（Coyote Time）、跳跃缓冲（Jump Buffer）、松键小跳。
///   战斗：血量 + 受击无敌帧与闪烁 + 击退；从上方踩到敌人会反弹并把敌人消灭。
///   死亡：掉出地图或血量归零，默认整关重开。
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

	/// <summary>玩家节点所在的分组，UI 靠它找到玩家（不用在编辑器里连 NodePath）。</summary>
	public const string GroupName = "player";

	/// <summary>已收集的金币数。只存在内存里，重开关卡会归零。</summary>
	public int Coins { get; private set; }

	[Signal] public delegate void HealthChangedEventHandler(int health, int maxHealth);
	[Signal] public delegate void CoinChangedEventHandler(int coins);
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

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_bodyShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		_hurtBox = GetNodeOrNull<HurtBox>("HurtBox");
		_stompBox = GetNodeOrNull<HitBox>("HitBox");

		ApplyCollisionLayers();
		AddToGroup(GroupName);

		Health = MaxHealth;
		_spawnPosition = GlobalPosition;
		_invincibleTimer = 0f;

		if (_stompBox != null)
		{
			_stompBox.HitLanded += OnStompLanded;
		}

		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
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

		Vector2 velocity = Velocity;
		bool onFloor = IsOnFloor();

		float inputX = Input.GetAxis("Left", "Right");
		bool jumpPressed = Input.IsActionJustPressed("Up");
		bool jumpHeld = Input.IsActionPressed("Up");

		// --- 计时器：土狼时间 / 跳跃缓冲 ---
		_coyoteTimer = onFloor ? CoyoteTime : Mathf.Max(0f, _coyoteTimer - dt);
		_jumpBufferTimer = jumpPressed ? JumpBufferTime : Mathf.Max(0f, _jumpBufferTimer - dt);

		// --- 水平移动 ---
		if (Mathf.Abs(inputX) > 0.01f)
		{
			float accel = onFloor ? GroundAcceleration : AirAcceleration;
			velocity.X = Mathf.MoveToward(velocity.X, inputX * MoveSpeed, accel * dt);
		}
		else
		{
			float friction = onFloor ? GroundFriction : AirFriction;
			velocity.X = Mathf.MoveToward(velocity.X, 0f, friction * dt);
		}

		// --- 重力 ---
		if (!onFloor)
		{
			velocity.Y = Mathf.Min(velocity.Y + Gravity * dt, MaxFallSpeed);
		}

		// --- 起跳 ---
		if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
		{
			velocity.Y = -JumpForce;
			_jumpNotCut = true;
			_jumpBufferTimer = 0f;
			_coyoteTimer = 0f;
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
