using Godot;

/// <summary>
/// 教学关卡专用玩家（player2.tscn，继承自 player.tscn）。
///
/// 在普通玩家的基础上多了两件事：
///   1. 开局没有武器（<see cref="Player.UnlockAttacksAtStart"/> 在 player2.tscn 里被关掉）；
///   2. 鼠标/手指点到自己 会弹三选一强化界面（具体抽什么由 <see cref="TutorialLevel.Mode"/> 决定）。
/// </summary>
public partial class Player2 : Player
{
	/// <summary>
	/// 玩家被点到、但当前场景没有 <see cref="TutorialLevel"/> 时发出。
	/// 给"自己管理强化界面"的模式用（比如无限跑酷：捡到 coinSpe 后点自己才弹三选一）。
	/// </summary>
	[Signal] public delegate void UpgradeRequestedEventHandler();

	/// <summary>点击判定半径（世界单位）。角色本体只有 6px，给到 8 就是"点到自己附近就算"。</summary>
	[Export] public float ClickRadius = 8f;

	/// <summary>是否把点击日志打到 Output（调试用）。</summary>
	[Export] public bool LogClick = true;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (GetTree().Paused || !IsAlive)
		{
			return;
		}

		if (!TryGetClickWorldPosition(@event, out Vector2 worldPosition))
		{
			return;
		}

		if (worldPosition.DistanceTo(GlobalPosition) > ClickRadius)
		{
			return;
		}

		if (LogClick)
		{
			GD.Print("[Player2] 点到自己了");
		}

		TryOpenUpgrade();
		GetViewport().SetInputAsHandled();
	}

	/// <summary>把"点到玩家"翻译成"弹出强化界面"。场景里没有 TutorialLevel 时什么也不做。</summary>
	public void TryOpenUpgrade()
	{
		if (!IsAlive || GetTree().Paused)
		{
			return;
		}

		// 玩家一般就是关卡的直接子节点；嵌套深了就用分组兜底
		TutorialLevel level = GetParentOrNull<TutorialLevel>()
			?? GetTree().GetFirstNodeInGroup(TutorialLevel.GroupName) as TutorialLevel;

		if (level == null)
		{
			// 没有关卡管理器：交给场景自己处理（例如无限跑酷）
			EmitSignal(SignalName.UpgradeRequested);
			return;
		}

		level.RequestUpgrade(this);
	}

	/// <summary>判断这次输入是不是"按下"，并换算成世界坐标。</summary>
	private bool TryGetClickWorldPosition(InputEvent @event, out Vector2 worldPosition)
	{
		worldPosition = Vector2.Zero;

		switch (@event)
		{
			case InputEventMouseButton mouse when mouse.Pressed && mouse.ButtonIndex == MouseButton.Left:
				// GetGlobalMousePosition 已经处理了相机与画布缩放
				worldPosition = GetGlobalMousePosition();
				return true;

			case InputEventScreenTouch touch when touch.Pressed:
				worldPosition = GetCanvasTransform().AffineInverse() * touch.Position;
				return true;

			default:
				return false;
		}
	}
}
