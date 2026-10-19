using Godot;

/// <summary>
/// 往下的通道（挂在关卡里那个 ↓ 标记上）。
///
/// 这个节点本身是 **StaticBody2D**，有碰撞 —— 玩家可以像踩平台一样站到它顶面；
/// 站上去（脚在顶面那条"站立带"里）并按住"向下"（键盘 S / 触屏 TBtnD）就会原地往下钻，
/// 钻进去之后切换到 <see cref="TargetScenePath"/>。
///
/// 只有"站在顶面"才算，站在旁边地面上按向下不会触发。
/// 下钻期间要先把玩家自己的物理和输入关掉，否则 CharacterBody2D 会被碰撞顶回来，看上去就是"抖一下没进去"。
/// </summary>
public partial class DownHatch : StaticBody2D
{
	[ExportGroup("触发")]
	/// <summary>按哪个动作下钻（要和输入映射里的名字一致）。</summary>
	[Export] public string Action = "Down";

	/// <summary>钻下去之后去哪个场景。</summary>
	[Export] public string TargetScenePath = ScenePaths.Main1_1;

	[ExportGroup("下钻动画")]
	/// <summary>往下钻多远（像素）。</summary>
	[Export] public float SinkDistance = 22f;

	/// <summary>下钻用多久（秒），结束后切场景。</summary>
	[Export] public float SinkTime = 0.55f;

	private bool _triggered;
	private bool _playerOnTop;

	public override void _Ready()
	{
		CreateStandArea();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_triggered)
		{
			return;
		}

		if (GetTree().GetFirstNodeInGroup(Player.GroupName) is not Player player || !player.IsAlive)
		{
			return;
		}

		// 必须真的站在顶面上（同时按着向下）
		if (!_playerOnTop || !player.IsOnFloor())
		{
			return;
		}

		if (!Input.IsActionPressed(Action))
		{
			return;
		}

		BeginSink(player);
	}

	/// <summary>在顶面上放一条薄薄的判定带：玩家站上去时身体会压进这条带子里。</summary>
	private void CreateStandArea()
	{
		Rect2 rect = LocalShapeRect();

		Area2D standArea = new()
		{
			Name = "StandArea",
			Monitoring = true,
			Monitorable = false,
			CollisionLayer = 0,
			CollisionMask = GameLayers.PlayerBody,
			Position = new Vector2(0f, rect.Position.Y + 3f),
		};

		standArea.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(rect.Size.X + 2f, 8f) },
		});

		standArea.BodyEntered += body =>
		{
			if (body is Player)
			{
				_playerOnTop = true;
			}
		};

		standArea.BodyExited += body =>
		{
			if (body is Player)
			{
				_playerOnTop = false;
			}
		};

		AddChild(standArea);
	}

	/// <summary>取自己 CollisionShape2D 的矩形（用来算顶面位置/宽度）。</summary>
	private Rect2 LocalShapeRect()
	{
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape is RectangleShape2D rect)
		{
			return new Rect2(-rect.Size / 2f, rect.Size);
		}

		return new Rect2(-10f, -8f, 20f, 16f);
	}

	private void BeginSink(Player player)
	{
		_triggered = true;

		// 关掉玩家自身的物理与输入：不然碰撞每帧都会把它推回去，钻不下去
		player.Velocity = Vector2.Zero;
		player.SetPhysicsProcess(false);
		player.SetProcessInput(false);
		player.SetProcessUnhandledInput(false);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(player, "position:y", player.Position.Y + SinkDistance, SinkTime)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		tween.TweenProperty(player, "modulate:a", 0f, SinkTime).SetDelay(SinkTime * 0.45f);
		tween.Chain().TweenCallback(Callable.From(ChangeScene));

		GD.Print($"[DownHatch] 玩家开始下钻 → {TargetScenePath}");
	}

	private void ChangeScene()
	{
		Error error = GetTree().ChangeSceneToFile(TargetScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[DownHatch] 切换场景失败：{error}（{TargetScenePath}）");
		}
	}
}
