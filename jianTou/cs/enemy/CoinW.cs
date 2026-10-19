using Godot;

/// <summary>
/// CoinW：只会挨打、**不会攻击玩家**的"金币罐子"（挂在关卡里那个 CoinW 贴图上）。
///
/// 每受到一次伤害：
///   · 闪红 + 弹一下，并飘一行"受到攻击"的提示（受击提示）；
///   · 有 <see cref="CoinChance"/>（默认 50%）的概率给玩家 1 枚 coin，飘一行"+1"（得币提示）。
///
/// 它没有 HitBox，所以永远不会伤到玩家；血量也不设上限，可以一直打。
///
/// 提示文字是运行时创建的 Label：**字号开大（48）再缩到 0.05**，
/// 这样相机 zoom=5 放大后仍然是清晰的（直接用 6 号字会被放糊）。
/// </summary>
public partial class CoinW : Sprite2D, IDamageable
{
	[ExportGroup("给币")]
	/// <summary>每次受伤给币的概率（0~1）。</summary>
	[Export] public float CoinChance = 0.5f;

	/// <summary>命中一次给几枚。</summary>
	[Export] public int CoinAmount = 1;

	[ExportGroup("受击表现")]
	/// <summary>闪红持续多久（秒）。</summary>
	[Export] public float FlashTime = 0.18f;

	/// <summary>受击时放大的比例（0.3 = 最大放大到 1.3 倍）。</summary>
	[Export] public float PopAmount = 0.3f;

	/// <summary>受击提示文字。默认留空 = 不飘字（只保留闪红 + 弹一下）；想加提示就在 Inspector 里填。</summary>
	[Export] public string HitText = "";

	/// <summary>得币提示文字，留空 = 不显示。</summary>
	[Export] public string CoinText = "+1";

	/// <summary>被打过多少次（调试/统计用）。</summary>
	public int HitCount { get; private set; }

	private readonly RandomNumberGenerator _rng = new();
	private Vector2 _baseScale = Vector2.One;
	private Color _baseModulate = Colors.White;
	private float _flashTimer;

	public override void _Ready()
	{
		_baseScale = Scale;
		_baseModulate = Modulate;
		_rng.Randomize();
	}

	public override void _Process(double delta)
	{
		if (_flashTimer <= 0f)
		{
			return;
		}

		_flashTimer -= (float)delta;

		float k = Mathf.Clamp(_flashTimer / Mathf.Max(0.001f, FlashTime), 0f, 1f);
		Modulate = _baseModulate.Lerp(new Color(1f, 0.45f, 0.45f), k);
		Scale = _baseScale * (1f + PopAmount * k);

		if (_flashTimer <= 0f)
		{
			Modulate = _baseModulate;
			Scale = _baseScale;
		}
	}

	/// <summary>挨打：给受击提示，然后掷一次 50% 的给币判定。</summary>
	public void TakeDamage(int amount, HitBox source)
	{
		HitCount++;
		_flashTimer = FlashTime;

		Popup(HitText, new Color(1f, 1f, 1f), -14f);

		if (_rng.Randf() < CoinChance)
		{
			GiveCoin();
		}
	}

	private void GiveCoin()
	{
		if (GetTree().GetFirstNodeInGroup(Player.GroupName) is not Player player)
		{
			return;
		}

		player.AddCoin(CoinAmount);
		Popup($"{CoinText}", new Color(1f, 0.86f, 0.28f), -22f);
	}

	/// <summary>
	/// 在世界坐标里飘一行字。字号 48 + 缩放 0.05 → 约 2.4 世界像素高，
	/// 相机 zoom=5 放大后约 12 屏幕像素，而且是从 48px 光栅化后缩下来的，不会糊。
	/// </summary>
	private void Popup(string text, Color color, float yOffset)
	{
		if (string.IsNullOrEmpty(text) || GetParent() is not Node parent)
		{
			return;
		}

		Label label = new()
		{
			Text = text,
			Size = new Vector2(400f, 80f),
			Scale = new Vector2(0.05f, 0.05f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		label.AddThemeFontSizeOverride("font_size", 48);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.02f, 0.02f, 1f));
		label.AddThemeConstantOverride("outline_size", 8);

		parent.AddChild(label);

		// Size 是 400×80、缩放 0.05 → 世界尺寸 20×4，所以 x 往左推 10 就正好居中
		label.GlobalPosition = GlobalPosition + new Vector2(-10f, yOffset);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(label, "position:y", label.Position.Y - 7f, 0.75f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(label, "modulate:a", 0f, 0.45f).SetDelay(0.3f);
		tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
	}
}
