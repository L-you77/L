using Godot;

/// <summary>
/// 远程敌人（enemyFar）：**不吃重力**，在出生点左右来回浮动，
/// 玩家进入射程且中间没有地形挡住时，先"蓄力"一小会儿（贴图轻微放大做预警），再朝玩家打一发弹体。
///
/// 和近战敌人（<see cref="Enemy"/>）共用 <see cref="EnemyBase"/> 那套：血量、受击闪红、
/// 从上方踩踏消灭、死亡闪红后立刻销毁 —— 所以它一样能被踩死。
///
/// 场景里必须把根节点的 <c>motion_mode</c> 设成 Floating（否则会往下掉）。
/// </summary>
public partial class EnemyFar : EnemyBase
{
	[ExportGroup("移动")]

	/// <summary>左右浮动速度（像素/秒）。</summary>
	[Export] public float MoveSpeed = 16f;

	/// <summary>巡逻半程：以出生点为中心左右各走这么远就掉头。</summary>
	[Export] public float PatrolDistance = 36f;

	/// <summary>上下浮动的幅度（像素），纯视觉，不影响判定。</summary>
	[Export] public float HoverAmplitude = 1.5f;

	/// <summary>上下浮动的频率（次/秒）。</summary>
	[Export] public float HoverFrequency = 0.6f;

	[ExportGroup("远程攻击")]

	/// <summary>水平射程。</summary>
	[Export] public float AttackRange = 72f;

	/// <summary>高度容差：玩家和它在垂直方向差太多就不打（避免朝天/朝地打）。</summary>
	[Export] public float AttackHeightTolerance = 20f;

	/// <summary>两发之间的间隔（秒）。</summary>
	[Export] public float AttackInterval = 1.8f;

	/// <summary>蓄力时间：这段时间内站住不动、贴图鼓一下，玩家可以躲。</summary>
	[Export] public float WindupTime = 0.45f;

	/// <summary>弹体飞行速度（像素/秒）。</summary>
	[Export] public float BulletSpeed = 90f;

	/// <summary>弹体伤害。</summary>
	[Export] public int BulletDamage = 1;

	/// <summary>弹体场景路径。</summary>
	[Export] public string BulletScenePath = "res://node/enemy/enemy_bullet.tscn";

	/// <summary>true = 朝玩家当前位置打（带角度）；false = 只按朝向水平打。</summary>
	[Export] public bool AimAtPlayer = true;

	/// <summary>是否要求视线不被地形挡住（防止隔墙射击）。</summary>
	[Export] public bool RequireLineOfSight = true;

	private const string GroupPlayer = "player";

	private Vector2 _home;
	private int _facing = -1;
	private float _attackCd;
	private float _windup;
	private float _phase;
	private Vector2 _spriteBaseScale = Vector2.One;
	private AnimatedSprite2D _sprite;
	private Player _player;
	private PackedScene _bulletScene;

	public override void _Ready()
	{
		base._Ready();

		_home = GlobalPosition;
		_sprite = Sprite;
		if (_sprite != null)
		{
			_spriteBaseScale = _sprite.Scale;
		}

		// 场景里那个 FarHit 贴图只是"枪口位置"的参考（白底图直接用会是个白方块），统一隐藏
		if (GetNodeOrNull<Sprite2D>("FarHit") is Sprite2D muzzle)
		{
			muzzle.Visible = false;
		}
	}

	protected override void MoveBehaviour(float delta)
	{
		_phase += delta;

		if (_attackCd > 0f)
		{
			_attackCd = Mathf.Max(0f, _attackCd - delta);
		}

		Player target = FindPlayer();

		// --- 蓄力中：站住、贴图鼓一下，时间到就打出去 ---
		if (_windup > 0f)
		{
			_windup -= delta;
			Velocity = Vector2.Zero;
			UpdateHover();

			float pulse = 1f + 0.25f * Mathf.Sin((WindupTime - _windup) * 30f);
			if (_sprite != null)
			{
				_sprite.Scale = _spriteBaseScale * pulse;
			}

			if (_windup <= 0f)
			{
				if (_sprite != null)
				{
					_sprite.Scale = _spriteBaseScale;
				}
				Fire(target);
			}
			return;
		}

		// --- 进入射程且看得见：开始蓄力 ---
		if (target != null && _attackCd <= 0f && InAttackRange(target) && HasLineOfSight(target))
		{
			_windup = WindupTime;
			_facing = target.GlobalPosition.X >= GlobalPosition.X ? 1 : -1;
			ApplyFacing();
			Velocity = Vector2.Zero;
			UpdateHover();
			return;
		}

		Patrol(delta);
	}

	/// <summary>左右来回浮动，到边界或撞墙就掉头。</summary>
	private void Patrol(float delta)
	{
		if (GlobalPosition.X >= _home.X + PatrolDistance)
		{
			_facing = -1;
		}
		else if (GlobalPosition.X <= _home.X - PatrolDistance)
		{
			_facing = 1;
		}

		ApplyFacing();

		Velocity = new Vector2(_facing * MoveSpeed, 0f);
		MoveAndSlide();
		UpdateHover();

		// 撞到墙/地形：掉头，并把巡逻中心挪到当前位置，免得一直贴着墙抖
		if (IsOnWall())
		{
			_facing = -_facing;
			_home = new Vector2(GlobalPosition.X, _home.Y);
		}
	}

	/// <summary>上下浮动只改贴图位置，不影响碰撞体和判定。</summary>
	private void UpdateHover()
	{
		if (_sprite == null || HoverAmplitude <= 0f)
		{
			return;
		}

		_sprite.Position = new Vector2(_sprite.Position.X, Mathf.Sin(_phase * HoverFrequency * Mathf.Tau) * HoverAmplitude);
	}

	private void ApplyFacing()
	{
		if (_sprite != null)
		{
			_sprite.FlipH = _facing < 0;
		}
	}

	private bool InAttackRange(Player target)
	{
		Vector2 delta = target.GlobalPosition - GlobalPosition;
		return Mathf.Abs(delta.X) <= AttackRange && Mathf.Abs(delta.Y) <= AttackHeightTolerance;
	}

	/// <summary>视线检查：中间有地形就不打（避免隔墙射人）。</summary>
	private bool HasLineOfSight(Player target)
	{
		if (!RequireLineOfSight)
		{
			return true;
		}

		PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(
			GlobalPosition, target.GlobalPosition, GameLayers.WorldMask);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;

		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	private void Fire(Player target)
	{
		_attackCd = AttackInterval;

		if (_bulletScene == null)
		{
			_bulletScene = ResourceLoader.Load<PackedScene>(BulletScenePath);
			if (_bulletScene == null)
			{
				GD.PushWarning($"[EnemyFar] 弹体场景加载失败：{BulletScenePath}");
				return;
			}
		}

		if (_bulletScene.Instantiate() is not EnemyBullet bullet)
		{
			return;
		}

		Vector2 direction = new(_facing, 0f);
		if (AimAtPlayer && target != null)
		{
			Vector2 toTarget = target.GlobalPosition - GlobalPosition;
			if (toTarget.LengthSquared() > 0.01f)
			{
				direction = toTarget.Normalized();
			}
		}

		bullet.Direction = direction;
		bullet.Speed = BulletSpeed;
		bullet.Damage = BulletDamage;

		// 挂到关卡上（挂成敌人的子节点会跟着敌人飞，方向就乱了）
		Node parent = GetParent();
		if (parent == null)
		{
			bullet.QueueFree();
			return;
		}

		parent.AddChild(bullet);
		bullet.GlobalPosition = GlobalPosition + direction * 4f;

		GD.Print($"[{Name}] 远程攻击");
	}

	private Player FindPlayer()
	{
		if (_player != null && IsInstanceValid(_player) && _player.IsAlive)
		{
			return _player;
		}

		_player = GetTree().GetFirstNodeInGroup(GroupPlayer) as Player;
		return _player != null && _player.IsAlive ? _player : null;
	}
}
