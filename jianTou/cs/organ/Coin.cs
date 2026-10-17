using Godot;

/// <summary>
/// 金币：玩家身体碰到即收集，计数记在 <see cref="Player.Coins"/> 上。
///
/// 待机表现：上下浮动 + 横向收缩（模拟硬币旋转），每枚的相位随机，摆一排不会同步得像机器。
/// 收集表现：放大 + 淡出后自动销毁。
///
/// 说明：金币只检测玩家本体（<see cref="GameLayers.PlayerBody"/>），自己不在任何碰撞层上，
/// 所以不会挡住玩家、也不会被敌人的 HitBox/HurtBox 误判。
/// </summary>
public partial class Coin : Area2D
{
	[ExportGroup("数值")]

	/// <summary>这一枚值多少。</summary>
	[Export] public int Value = 1;

	[ExportGroup("待机表现")]

	/// <summary>是否上下浮动。</summary>
	[Export] public bool Bob = true;

	/// <summary>浮动幅度（像素）。</summary>
	[Export] public float BobAmplitude = 1.5f;

	/// <summary>浮动频率（次/秒）。</summary>
	[Export] public float BobFrequency = 1.2f;

	/// <summary>是否横向收缩模拟旋转。</summary>
	[Export] public bool Spin = true;

	/// <summary>转一圈的时间（秒）。</summary>
	[Export] public float SpinPeriod = 1.2f;

	[ExportGroup("收集表现")]

	/// <summary>收集后放大淡出的时长。</summary>
	[Export] public float PopTime = 0.18f;

	/// <summary>收集时放大到的倍数。</summary>
	[Export] public float PopScale = 1.6f;

	/// <summary>被收集时发出，供音效/UI 监听。</summary>
	[Signal] public delegate void CollectedEventHandler(int value, Node2D collector);

	private AnimatedSprite2D _sprite;
	private CollisionShape2D _shape;
	private Vector2 _basePosition;
	private Vector2 _spriteBaseScale = Vector2.One;
	private float _phase;
	private bool _collected;

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

		// 只去检测玩家本体；金币自己不需要被任何东西检测
		CollisionLayer = 0;
		CollisionMask = GameLayers.PlayerBody;
		Monitoring = true;
		Monitorable = false;

		BodyEntered += OnBodyEntered;

		_basePosition = Position;
		if (_sprite != null)
		{
			_spriteBaseScale = _sprite.Scale;
			if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation("coin"))
			{
				_sprite.Play("coin");
			}
		}

		// 相位随机，一排金币不会整齐划一地一起浮动
		_phase = (float)GD.RandRange(0.0, Mathf.Tau);
	}

	public override void _Process(double delta)
	{
		if (_collected)
		{
			return;
		}

		_phase += (float)delta;

		if (Bob)
		{
			Position = _basePosition + new Vector2(0f, Mathf.Sin(_phase * BobFrequency * Mathf.Tau) * BobAmplitude);
		}

		if (Spin && _sprite != null && SpinPeriod > 0.01f)
		{
			// |cos| 从 1 收到 0 再回 1，看起来就像硬币在转
			float k = Mathf.Abs(Mathf.Cos(_phase / SpinPeriod * Mathf.Tau));
			_sprite.Scale = new Vector2(_spriteBaseScale.X * Mathf.Max(k, 0.15f), _spriteBaseScale.Y);
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_collected || body is not Player player)
		{
			return;
		}

		_collected = true;
		player.AddCoin(Value);
		EmitSignal(SignalName.Collected, Value, player);
		PlayPop();
	}

	/// <summary>放大淡出，然后销毁。</summary>
	private void PlayPop()
	{
		// 先关掉判定，避免同一帧被重复收集
		if (_shape != null)
		{
			_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}

		Node2D visual = _sprite != null ? _sprite : this;

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(visual, "scale", _spriteBaseScale * PopScale, PopTime);
		tween.TweenProperty(visual, "modulate:a", 0f, PopTime);
		tween.Finished += QueueFree;
	}
}
