using Godot;

/// <summary>
/// 地面敌人（贴图是“怪”）。
/// 行为：左右匀速巡逻，撞到墙或走到平台边缘就掉头。
/// 巡逻方向靠物理射线探测（墙 / 脚下有没有地面），所以在楼梯、移动平台上也不会掉下去。
/// </summary>
public partial class Enemy : EnemyBase
{
	[ExportGroup("巡逻")]

	/// <summary>巡逻速度（像素/秒）。</summary>
	[Export] public float PatrolSpeed = 30f;

	/// <summary>初始朝向：-1 = 向左，1 = 向右。</summary>
	[Export] public int PatrolDirection = -1;

	/// <summary>重力。</summary>
	[Export] public float Gravity = 500f;

	/// <summary>最大下落速度。</summary>
	[Export] public float MaxFallSpeed = 320f;

	/// <summary>撞墙探测的距离。</summary>
	[Export] public float WallProbeDistance = 1.5f;

	/// <summary>脚下探测的距离，越大越早掉头。</summary>
	[Export] public float GroundProbeDistance = 8f;

	/// <summary>撞墙是否掉头。</summary>
	[Export] public bool TurnAtWall = true;

	/// <summary>走到平台边缘是否掉头。</summary>
	[Export] public bool TurnAtLedge = true;

	private static readonly StringName AnimMove = "move";
	private int _direction = -1;

	public override void _Ready()
	{
		base._Ready();

		_direction = PatrolDirection >= 0 ? 1 : -1;
		PlayAnimation(AnimMove);
	}

	protected override void MoveBehaviour(float delta)
	{
		Vector2 velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity.Y = Mathf.Min(velocity.Y + Gravity * delta, MaxFallSpeed);
		}

		// 只在贴着地面时判断掉头，避免刚落地时误判
		if (IsOnFloor())
		{
			if (TurnAtWall && HasWallAhead(_direction))
			{
				_direction = -_direction;
			}
			else if (TurnAtLedge && !HasGroundAhead(_direction))
			{
				_direction = -_direction;
			}
		}

		velocity.X = _direction * PatrolSpeed;
		Velocity = velocity;
		MoveAndSlide();
	}

	/// <summary>前方是否有墙。</summary>
	private bool HasWallAhead(int direction)
	{
		Vector2 from = GlobalPosition + new Vector2(direction * (HalfExtents.X + 0.1f), 0f);
		Vector2 to = from + new Vector2(direction * WallProbeDistance, 0f);
		return Probe(from, to);
	}

	/// <summary>前方脚下是否还有地面。</summary>
	private bool HasGroundAhead(int direction)
	{
		Vector2 from = GlobalPosition + new Vector2(direction * HalfExtents.X * 0.8f, HalfExtents.Y - 0.5f);
		Vector2 to = from + new Vector2(0f, GroundProbeDistance);
		return Probe(from, to);
	}

	/// <summary>打一条射线，看是否命中地形/平台。</summary>
	private bool Probe(Vector2 from, Vector2 to)
	{
		PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(
			from, to, GameLayers.WorldMask, new Godot.Collections.Array<Rid> { GetRid() });
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;

		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
	}
}
