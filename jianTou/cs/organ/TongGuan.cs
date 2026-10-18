using Godot;

/// <summary>
/// 通关点（tongGuan）。玩家碰到就判定通关：
///   1. 只触发一次，触发后关掉碰撞；
///   2. 发 <see cref="Cleared"/> 信号（留给音效/存档用）；
///   3. 弹出通关界面（<see cref="ClearPanel"/>），里面有"下一关 / 返回"两个按钮。
///
/// 判定只认玩家**本体**（mask = PlayerBody），不参与伤害层，所以无敌帧、踩怪期间碰到都算通关。
/// 每个实例可以在 Inspector 里单独配 <see cref="NextScenePath"/>：
/// 留空 = 这一关是最后一关，只显示"返回"。
/// </summary>
public partial class TongGuan : Area2D
{
	/// <summary>通关界面场景路径（一般不用改）。</summary>
	[Export] public string ClearPanelScenePath = "res://node/ui/clear_panel.tscn";

	/// <summary>大标题。</summary>
	[Export] public string ClearTitle = "通关！";

	/// <summary>副标题，比如"第一关 · 向右"。留空则不显示。</summary>
	[Export] public string ClearSubtitle = "";

	/// <summary>点"下一关"去哪。留空 = 没有下一关（只显示返回）。</summary>
	[Export] public string NextScenePath = "";

	/// <summary>点"返回"去哪（默认回教学选关）。</summary>
	[Export] public string BackScenePath = ScenePaths.TutorialSelect;

	/// <summary>通关后是否把通关点本身藏起来。</summary>
	[Export] public bool HideAfterClear = true;

	[Signal] public delegate void ClearedEventHandler();

	/// <summary>是否已经通关过（避免重复触发）。</summary>
	public bool IsCleared { get; private set; }

	public override void _Ready()
	{
		// 兜底：只检测玩家本体，自己不在任何层上（场景里也设了一遍）
		CollisionLayer = 0;
		CollisionMask = GameLayers.PlayerBody;
		Monitorable = false;
		Monitoring = true;

		if (GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") is AnimatedSprite2D sprite
			&& sprite.SpriteFrames != null
			&& sprite.SpriteFrames.HasAnimation("tongGuan"))
		{
			sprite.Play("tongGuan");
		}

		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (IsCleared || body is not Player)
		{
			return;
		}

		IsCleared = true;

		// 立刻关掉判定，防止同一帧/后续帧重复触发
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D shape)
		{
			shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}

		EmitSignal(SignalName.Cleared);
		GD.Print($"[TongGuan] 通关（下一关：{(string.IsNullOrEmpty(NextScenePath) ? "无" : NextScenePath)}）");

		ShowClearPanel();

		if (HideAfterClear)
		{
			Visible = false;
		}
	}

	/// <summary>弹出通关界面并暂停游戏。</summary>
	private void ShowClearPanel()
	{
		PackedScene scene = ResourceLoader.Load<PackedScene>(ClearPanelScenePath);
		if (scene == null)
		{
			GD.PushError($"[TongGuan] 通关界面加载失败：{ClearPanelScenePath}");
			return;
		}

		if (scene.Instantiate() is not ClearPanel panel)
		{
			GD.PushError("[TongGuan] 通关界面根节点不是 ClearPanel");
			return;
		}

		// 挂在关卡根节点上（CanvasLayer，不会跟着相机缩放）
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			panel.QueueFree();
			return;
		}

		parent.AddChild(panel);
		panel.ShowClear(ClearTitle, ClearSubtitle, NextScenePath, BackScenePath);
	}
}
