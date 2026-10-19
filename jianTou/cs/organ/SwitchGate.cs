using System.Collections.Generic;
using Godot;

/// <summary>
/// 机关开关（挂在关卡里 R / B 这种色块标记 Sprite2D 上）。
///
/// 触发方式：**玩家角色碰到它**就触发（脚本自己在 _Ready 里建一个圆形 Area2D 做触碰判定，
/// 不需要在场景里手动加节点）。只有 Player 会触发，敌人踩上来不算。
///
/// 触发后的效果（四种可以混用）：
///   · <see cref="HideOnUse"/>       立刻隐藏（比如 B2 这种标记）；
///   · <see cref="SlideDownOnUse"/>  往下滑一段 + 淡出后隐藏（R2 那种"降下去"的墙）；
///                                   **不会改动 TileMapLayer**，纯视觉移动；
///   · <see cref="RevealOnUse"/>     进关时先隐藏（连碰撞一起关），触发后出现并开始动；
///   · <see cref="StartOnUse"/>      进关时**可见、有碰撞，但动画冻住**，触发后开始移动
///                                   （做"平台一直在，碰到 B 才启动"）；
///   · <see cref="EraseCells"/>      抹掉一块地形格子（默认 Size 为 0 = 不抹）。
///
/// <see cref="OneShot"/> 为 true 时只能触发一次（默认），触发后开关自己**不会消失**，
/// 只是关掉触碰判定、不再响应。
/// </summary>
public partial class SwitchGate : Sprite2D
{
	[ExportGroup("触发（角色碰到就开）")]

	/// <summary>触碰判定半径（像素，按世界坐标算，不受精灵缩放影响）。</summary>
	[Export] public float TouchRadius = 14f;

	/// <summary>只能用一次（触发后关掉触碰判定，不再响应）。</summary>
	[Export] public bool OneShot = true;

	[Signal] public delegate void ActivatedEventHandler();

	/// <summary>是否已经触发过。</summary>
	public bool IsUsed { get; private set; }

	[ExportGroup("效果")]

	/// <summary>触发后立刻隐藏的节点名（相对关卡根节点，比如 "B2"）。</summary>
	[Export] public string[] HideOnUse = System.Array.Empty<string>();

	/// <summary>触发后往下滑走并淡出的节点名（比如 "R2"）。不改地形。</summary>
	[Export] public string[] SlideDownOnUse = System.Array.Empty<string>();

	/// <summary>往下滑多远（像素）。</summary>
	[Export] public float SlideDistance = 64f;

	/// <summary>往下滑用多久（秒）。</summary>
	[Export] public float SlideTime = 0.6f;

	/// <summary>触发后才出现的节点名（进关时会被关掉：隐藏 + 关碰撞 + 动画冻结）。</summary>
	[Export] public string[] RevealOnUse = System.Array.Empty<string>();

	/// <summary>进关就在、但动画冻住的节点名（触发后才开始动，显隐和碰撞不变）。</summary>
	[Export] public string[] StartOnUse = System.Array.Empty<string>();

	/// <summary>要抹掉格子的 TileMapLayer 节点名（相对关卡根节点）。留空 = 不抹。</summary>
	[Export] public string TileMapNodePath = "TileMapLayer";

	/// <summary>要抹掉的格子区域（**tile 坐标**，不是像素）。Size 为 0 表示不抹。</summary>
	[Export] public Rect2I EraseCells = new(0, 0, 0, 0);

	private readonly List<Node> _revealTargets = new();
	private readonly List<Node> _startTargets = new();
	private Area2D _touchArea;

	public override void _Ready()
	{
		// "碰到才启动"的目标：显隐/碰撞都不动，只把动画冻在第 0 帧
		foreach (string name in StartOnUse)
		{
			if (FindLevelNode(name) is Node node)
			{
				_startTargets.Add(node);
				PlayAnimation(node, false, freeze: true);
			}
		}

		// "碰到才出现"的目标：进关时先整个关掉
		foreach (string name in RevealOnUse)
		{
			if (FindLevelNode(name) is Node node)
			{
				_revealTargets.Add(node);
				SetTargetActive(node, false);
			}
		}

		CreateTouchArea();
	}

	/// <summary>建一个只认玩家本体的圆形 Area2D，玩家走过来碰到就触发。</summary>
	private void CreateTouchArea()
	{
		_touchArea = new Area2D
		{
			Name = "TouchArea",
			Monitoring = true,
			Monitorable = false,
			CollisionLayer = 0,
			CollisionMask = GameLayers.PlayerBody,
		};

		// 抵消父精灵的缩放：标记块普遍是 scale (1,1.75)，不补的话判定会变成椭圆的
		_touchArea.Scale = new Vector2(
			1f / (Mathf.Abs(Scale.X) < 0.001f ? 1f : Mathf.Abs(Scale.X)),
			1f / (Mathf.Abs(Scale.Y) < 0.001f ? 1f : Mathf.Abs(Scale.Y)));

		_touchArea.AddChild(new CollisionShape2D
		{
			Shape = new CircleShape2D { Radius = TouchRadius },
		});

		AddChild(_touchArea);
		_touchArea.BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is not Player)
		{
			return;
		}

		Activate();
	}

	/// <summary>触发机关。</summary>
	public void Activate()
	{
		if (IsUsed && OneShot)
		{
			return;
		}

		IsUsed = true;

		foreach (string name in HideOnUse)
		{
			if (FindLevelNode(name) is CanvasItem item)
			{
				item.Visible = false;
			}
		}

		foreach (string name in SlideDownOnUse)
		{
			SlideDownAndFade(name);
		}

		foreach (Node node in _revealTargets)
		{
			SetTargetActive(node, true);
		}

		foreach (Node node in _startTargets)
		{
			PlayAnimation(node, true, freeze: false);
		}

		EraseTiles();

		// 开关自己不消失（R / B 的标记还要留着看），只关掉触碰判定
		if (_touchArea != null && OneShot)
		{
			_touchArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
		}

		EmitSignal(SignalName.Activated);
		GD.Print($"[SwitchGate] {Name} 被玩家触碰，已触发");
	}

	/// <summary>把节点往下滑一段，同时淡出，结束再隐藏。</summary>
	private void SlideDownAndFade(string name)
	{
		if (FindLevelNode(name) is not Node2D node)
		{
			return;
		}

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(node, "position:y", node.Position.Y + SlideDistance, SlideTime)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);

		if (node is CanvasItem)
		{
			tween.TweenProperty(node, "modulate:a", 0f, SlideTime);
		}

		tween.Chain().TweenCallback(Callable.From(() =>
		{
			node.Visible = false;
			// 有碰撞体的话（比如 R2 是 StaticBody2D）一起关掉，否则"看不见的墙"还在挡路
			SetCollisionEnabled(node, false);
		}));
	}

	/// <summary>把 <see cref="EraseCells"/> 这块格子从地形里删掉（墙就真的没了）。默认不启用。</summary>
	private void EraseTiles()
	{
		if (EraseCells.Size.X <= 0 || EraseCells.Size.Y <= 0)
		{
			return;
		}

		if (GetParent()?.GetNodeOrNull<TileMapLayer>(TileMapNodePath) is not TileMapLayer map)
		{
			GD.PushWarning($"[SwitchGate] 找不到 TileMapLayer：{TileMapNodePath}");
			return;
		}

		for (int x = EraseCells.Position.X; x < EraseCells.Position.X + EraseCells.Size.X; x++)
		{
			for (int y = EraseCells.Position.Y; y < EraseCells.Position.Y + EraseCells.Size.Y; y++)
			{
				map.EraseCell(new Vector2I(x, y));
			}
		}

		GD.Print($"[SwitchGate] 抹掉 {EraseCells.Size.X}×{EraseCells.Size.Y} 格地形");
	}

	/// <summary>打开/关闭一个目标：显隐 + 碰撞 + 动画。</summary>
	private static void SetTargetActive(Node root, bool active)
	{
		if (root is CanvasItem canvasItem)
		{
			canvasItem.Visible = active;
		}

		SetCollisionEnabled(root, active);
		PlayAnimation(root, active, freeze: !active);
	}

	/// <summary>
	/// 播放/冻住目标里的 AnimationPlayer（移动平台的位移就是动画驱动的）。
	/// 用 SpeedScale 冻结，不用 Stop()——Stop 会清掉 CurrentAnimation，之后就没得播了。
	/// </summary>
	private static void PlayAnimation(Node root, bool play, bool freeze)
	{
		if (FindFirst<AnimationPlayer>(root) is not AnimationPlayer anim)
		{
			return;
		}

		string animation = anim.Autoplay.ToString();
		if (string.IsNullOrEmpty(animation))
		{
			animation = anim.CurrentAnimation.ToString();
		}

		if (string.IsNullOrEmpty(animation))
		{
			return;
		}

		if (play && !freeze)
		{
			anim.Play(animation);
			anim.SpeedScale = 1f;
			return;
		}

		anim.Play(animation);
		anim.Seek(0, true);
		anim.SpeedScale = 0f;
	}

	private static void SetCollisionEnabled(Node root, bool enabled)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is CollisionShape2D shape)
			{
				shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, !enabled);
			}
			else if (child.GetChildCount() > 0)
			{
				SetCollisionEnabled(child, enabled);
			}
		}
	}

	private static T FindFirst<T>(Node root) where T : Node
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is T match)
			{
				return match;
			}

			if (child.GetChildCount() > 0 && FindFirst<T>(child) is T nested)
			{
				return nested;
			}
		}

		return null;
	}

	/// <summary>按名字在关卡根节点下找节点（开关自己是关卡根的直接子节点）。</summary>
	private Node FindLevelNode(string name)
	{
		Node root = GetParent();
		if (root == null)
		{
			return null;
		}

		if (root.Name.ToString() == name)
		{
			return root;
		}

		return root.FindChild(name, true, false);
	}
}
