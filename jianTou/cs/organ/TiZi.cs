using Godot;

/// <summary>
/// 梯子（tiZi）：一块"可以爬"的区域。
///
/// 玩家身体进来就登记到 <see cref="Player"/> 的梯子列表里，出去就注销；
/// 玩家在梯子上时不吃重力，按 上/下 以 LadderClimbSpeed 爬。
///
/// 用法：把 tiZi.tscn 拖进关卡，需要长梯子就直接拉高节点的 Y 轴缩放
/// （贴图和判定盒会一起变高）。
/// </summary>
public partial class TiZi : Area2D
{
	public override void _Ready()
	{
		// 只去检测玩家本体，自己不参与任何判定
		CollisionLayer = 0;
		CollisionMask = GameLayers.PlayerBody;
		Monitoring = true;
		Monitorable = false;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player player)
		{
			player.EnterLadder(this);
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is Player player)
		{
			player.ExitLadder(this);
		}
	}
}
