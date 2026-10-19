using Godot;

/// <summary>
/// 生命值 UI（挂在 ui.tscn 的 HealthIcon 上）：
///   · 血量 &gt; 1 → 显示 AnimatedSprite2D 图标，右边数字显示当前血量；
///   · 血量 == 1 → 图标换成闪烁的 warn 感叹号，数字照常显示；
///   · 血量 == 0 → 图标和 warn 都收起（死亡面板接管）。
///
/// 血量只用**数字**表示，不做"一个图标代表一点血"。
/// warn 的缩放取场景里的值，脚本只负责做脉冲，所以你调整缩放/位置不会被覆盖。
/// </summary>
public partial class HealthHud : Control
{
	[ExportGroup("表现")]

	/// <summary>血量图标播放的动画名。</summary>
	[Export] public StringName IconAnimation = "idle";

	/// <summary>warn 闪烁速度。</summary>
	[Export] public float WarnPulseSpeed = 7f;

	private Player _player;
	private AnimatedSprite2D _icon;
	private Sprite2D _warn;
	private Label _label;
	private Label _coinLabel;
	private Vector2 _warnBaseScale = Vector2.One;
	private float _pulseTime;
	private bool _warnShown;

	public override void _Ready()
	{
		_icon = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_warn = GetNodeOrNull<Sprite2D>("Warn");
		_label = GetNodeOrNull<Label>("HealthLabel");
		_coinLabel = GetNodeOrNull<Label>("CoinLabel");

		if (_warn != null)
		{
			_warnBaseScale = _warn.Scale;
			_warn.Visible = false;
		}

		if (_icon != null && _icon.SpriteFrames != null && _icon.SpriteFrames.HasAnimation(IconAnimation))
		{
			_icon.Play(IconAnimation);
		}

		_player = GetTree().GetFirstNodeInGroup(Player.GroupName) as Player;
		if (_player == null)
		{
			GD.PushWarning("[HealthHud] 找不到 group=player 的玩家节点，生命值 UI 不会更新");
			return;
		}

		_player.HealthChanged += OnHealthChanged;
		_player.CoinChanged += OnCoinChanged;

		// 玩家的 HealthChanged 在 _Ready 里已经发过一次（那时 UI 还没连上），这里同步初始值
		OnHealthChanged(_player.Health, _player.MaxHealth);
		OnCoinChanged(_player.Coins);
	}

	/// <summary>金币数：只显示"这一关捡到多少"，通关时才会带出关卡。</summary>
	private void OnCoinChanged(int coins)
	{
		if (_coinLabel != null)
		{
			_coinLabel.Text = coins.ToString();
		}
	}

	public override void _Process(double delta)
	{
		if (_warn == null || !_warnShown)
		{
			return;
		}

		_pulseTime += (float)delta;

		float pulse = 1f + Mathf.Sin(_pulseTime * WarnPulseSpeed) * 0.12f;
		_warn.Scale = _warnBaseScale * pulse;

		float alpha = Mathf.Sin(_pulseTime * WarnPulseSpeed * 1.5f) > 0f ? 1f : 0.45f;
		_warn.Modulate = new Color(1f, 1f, 1f, alpha);
	}

	private void OnHealthChanged(int health, int maxHealth)
	{
		if (_label != null)
		{
			_label.Text = health.ToString();
		}

		// > 1：图标 + 数字；== 1：warn 顶替图标；== 0：全收起（死亡面板接管）
		if (_icon != null)
		{
			_icon.Visible = health > 1;
		}

		SetWarnVisible(health == 1);
	}

	private void SetWarnVisible(bool visible)
	{
		_warnShown = visible;

		if (_warn == null)
		{
			return;
		}

		_warn.Visible = visible;

		if (visible)
		{
			_pulseTime = 0f;
			_warn.Scale = _warnBaseScale;
			_warn.Modulate = Colors.White;
		}
	}
}
