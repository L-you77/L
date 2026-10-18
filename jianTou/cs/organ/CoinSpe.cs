using Godot;

/// <summary>
/// 强化币（coinSpe）：捡到一个，玩家就能"点击自己"弹出一次三选一强化。
///
/// 和金币（<see cref="Coin"/>）的区别：这个加的是 <see cref="Player.UpgradeCharges"/>，不是金币数。
/// 只用玩家**本体**判定（mask = PlayerBody），不参与伤害层，无敌帧期间照样能捡。
/// </summary>
public partial class CoinSpe : Area2D
{
	/// <summary>这一枚给几次强化机会。</summary>
	[Export] public int Charges = 1;

	/// <summary>上下浮动幅度（像素），0 = 不浮动。</summary>
	[Export] public float BobAmplitude = 1.5f;

	/// <summary>浮动频率（次/秒）。</summary>
	[Export] public float BobSpeed = 1.2f;

	/// <summary>拾取后放大淡出的时长。</summary>
	[Export] public float CollectTime = 0.18f;

	[Signal] public delegate void CollectedEventHandler(int charges);

	private Vector2 _origin;
	private float _time;
	private bool _collected;

	public override void _Ready()
	{
		// 兜底：只检测玩家本体，自己不在任何层上（场景里也设了一遍）
		CollisionLayer = 0;
		CollisionMask = GameLayers.PlayerBody;
		Monitorable = false;
		Monitoring = true;

		_origin = Position;

		if (GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") is AnimatedSprite2D sprite
			&& sprite.SpriteFrames != null
			&& sprite.SpriteFrames.HasAnimation("default"))
		{
			sprite.Play("default");
		}

		BodyEntered += OnBodyEntered;
	}

	public override void _Process(double delta)
	{
		if (_collected || BobAmplitude <= 0f)
		{
			return;
		}

		_time += (float)delta;
		Position = _origin + new Vector2(0f, Mathf.Sin(_time * Mathf.Tau * BobSpeed) * BobAmplitude);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_collected || body is not Player player)
		{
			return;
		}

		_collected = true;

		player.AddUpgradeCharge(Charges);
		EmitSignal(SignalName.Collected, Charges);
		GD.Print($"[CoinSpe] 拾取强化币，次数 +{Charges}");

		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D shape)
		{
			shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}

		PlayPickup();
	}

	/// <summary>放大 + 淡出后销毁。</summary>
	private void PlayPickup()
	{
		if (CollectTime <= 0f)
		{
			QueueFree();
			return;
		}

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(this, "scale", Vector2.One * 1.8f, CollectTime);
		tween.TweenProperty(this, "modulate:a", 0f, CollectTime);
		tween.Chain().TweenCallback(Callable.From(QueueFree));
	}
}
