using System.Collections.Generic;
using Godot;

/// <summary>
/// 菜单背景（node/ui/menu_background.tscn）：
///   · 竖向渐变底（上下两个颜色，运行时用 GradientTexture2D 生成，不用图片）；
///   · 两团柔光（径向渐变），让画面不那么平；
///   · 几枚半透明的方向箭头缓慢旋转 + 上下浮动，呼应"玩家是一支箭头"的主题。
///
/// 用 <see cref="Dark"/> 切换深/浅两套配色：home（第一层主页）是深色的，其余菜单是浅色的。
/// 箭头位置按视口比例算，所以任何分辨率都铺得满；纯装饰，mouse_filter = Ignore 不吃点击。
/// </summary>
public partial class MenuBackground : Control
{
	/// <summary>深色版本（给 home 用）。</summary>
	[Export] public bool Dark = false;

	/// <summary>箭头每秒旋转多少度（正负交替）。</summary>
	[Export] public float SpinSpeed = 5f;

	/// <summary>箭头上下浮动的幅度（像素）。</summary>
	[Export] public float FloatAmplitude = 12f;

	/// <summary>浮动速度。</summary>
	[Export] public float FloatSpeed = 0.55f;

	/// <summary>箭头在屏幕上的相对位置（0~1）。</summary>
	private static readonly Vector2[] ArrowLayout =
	{
		new(0.10f, 0.22f),
		new(0.30f, 0.80f),
		new(0.54f, 0.13f),
		new(0.74f, 0.72f),
		new(0.90f, 0.30f),
		new(0.44f, 0.50f),
	};

	private TextureRect _gradient;
	private TextureRect _glowA;
	private TextureRect _glowB;
	private Node2D _arrowLayer;

	private readonly List<Sprite2D> _arrows = new();
	private readonly List<Vector2> _arrowBase = new();
	private float _time;

	public override void _Ready()
	{
		// 纯背景，不吃鼠标
		MouseFilter = MouseFilterEnum.Ignore;

		_gradient = GetNodeOrNull<TextureRect>("Gradient");
		_glowA = GetNodeOrNull<TextureRect>("GlowA");
		_glowB = GetNodeOrNull<TextureRect>("GlowB");
		_arrowLayer = GetNodeOrNull<Node2D>("ArrowLayer");

		ApplyColors();
		CollectArrows();
		Resized += LayoutArrows;
		LayoutArrows();
	}

	public override void _Process(double delta)
	{
		if (_arrowLayer == null || !_arrowLayer.Visible)
		{
			return;
		}

		_time += (float)delta;

		for (int i = 0; i < _arrows.Count; i++)
		{
			Sprite2D arrow = _arrows[i];

			float dir = i % 2 == 0 ? 1f : -1f;
			arrow.Rotation += Mathf.DegToRad(SpinSpeed * dir) * (float)delta;

			arrow.Position = _arrowBase[i] + new Vector2(
				0f,
				Mathf.Sin(_time * FloatSpeed + i * 1.3f) * FloatAmplitude);
		}
	}

	private void ApplyColors()
	{
		if (_gradient != null)
		{
			_gradient.Texture = Dark
				? MakeLinear(new Color(0.145f, 0.149f, 0.176f), new Color(0.075f, 0.078f, 0.098f))
				: MakeLinear(new Color(0.960f, 0.964f, 0.980f), new Color(0.851f, 0.867f, 0.925f));
		}

		if (_glowA != null)
		{
			_glowA.Texture = MakeRadial(
				new Color(1f, 1f, 1f, Dark ? 0.10f : 0.55f),
				new Color(1f, 1f, 1f, 0f));

			_glowA.Modulate = Dark
				? new Color(0.55f, 0.62f, 1f, 1f)
				: new Color(1f, 1f, 1f, 1f);
		}

		if (_glowB != null)
		{
			_glowB.Texture = MakeRadial(
				new Color(1f, 1f, 1f, Dark ? 0.12f : 0.60f),
				new Color(1f, 1f, 1f, 0f));

			_glowB.Modulate = Dark
				? new Color(0.62f, 0.48f, 0.92f, 1f)
				: new Color(0.74f, 0.76f, 0.98f, 1f);
		}

		// 深色底上黑箭头看不见，干脆不显示
		if (_arrowLayer != null)
		{
			_arrowLayer.Visible = !Dark;
		}
	}

	private void CollectArrows()
	{
		_arrows.Clear();
		_arrowBase.Clear();

		if (_arrowLayer == null)
		{
			return;
		}

		foreach (Node child in _arrowLayer.GetChildren())
		{
			if (child is Sprite2D sprite)
			{
				_arrows.Add(sprite);
				_arrowBase.Add(sprite.Position);
			}
		}
	}

	/// <summary>按视口大小摆箭头（0~1 的相对坐标 × 尺寸）。</summary>
	private void LayoutArrows()
	{
		if (_arrows.Count == 0)
		{
			return;
		}

		Vector2 size = Size;
		if (size.X < 1f || size.Y < 1f)
		{
			size = GetViewportRect().Size;
		}

		for (int i = 0; i < _arrows.Count; i++)
		{
			Vector2 rel = ArrowLayout[i % ArrowLayout.Length];
			Vector2 basePos = new(size.X * rel.X, size.Y * rel.Y);

			_arrowBase[i] = basePos;
			_arrows[i].Position = basePos;
		}
	}

	private static Texture2D MakeLinear(Color top, Color bottom)
	{
		Gradient gradient = new();
		gradient.SetColor(0, top);
		gradient.SetColor(1, bottom);
		gradient.SetOffset(0, 0f);
		gradient.SetOffset(1, 1f);

		return new GradientTexture2D
		{
			Gradient = gradient,
			Width = 8,
			Height = 256,
			FillFrom = new Vector2(0f, 0f),
			FillTo = new Vector2(0f, 1f),
		};
	}

	private static Texture2D MakeRadial(Color center, Color edge)
	{
		Gradient gradient = new();
		gradient.SetColor(0, center);
		gradient.SetColor(1, edge);
		gradient.SetOffset(0, 0f);
		gradient.SetOffset(1, 1f);

		return new GradientTexture2D
		{
			Gradient = gradient,
			Width = 256,
			Height = 256,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f),
		};
	}
}
