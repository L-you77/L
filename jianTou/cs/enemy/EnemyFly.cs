using Godot;

/// <summary>
/// 飞行敌人：不吃重力，在出生点周围的一块矩形范围里随机游荡。
/// 每飞到一个点就原地浮动一会儿，再挑下一个点；撞到墙/地面会立刻重新挑点，不会卡在角落。
/// 想改成“上下直线浮动”就把 WanderExtents 的 X 设成 0；想改成“纯原地浮动”就把 X、Y 都设成 0。
/// </summary>
public partial class EnemyFly : EnemyBase
{
	[ExportGroup("飞行")]

	/// <summary>飞行速度（像素/秒）。</summary>
	[Export] public float FlySpeed = 22f;

	/// <summary>游荡范围：以出生点为中心的半宽 / 半高。</summary>
	[Export] public Vector2 WanderExtents = new Vector2(40f, 24f);

	/// <summary>判定“到了”的距离。</summary>
	[Export] public float ArriveDistance = 2f;

	/// <summary>到达后原地停留的最短 / 最长时间。</summary>
	[Export] public float RestMinTime = 0.8f;
	[Export] public float RestMaxTime = 2.0f;

	/// <summary>停留时是否上下浮动。</summary>
	[Export] public bool BobWhenResting = true;

	/// <summary>浮动速度（像素/秒），即正弦的振幅速度。</summary>
	[Export] public float BobAmplitude = 10f;

	/// <summary>浮动频率（次/秒）。</summary>
	[Export] public float BobFrequency = 0.5f;

	private enum FlyState
	{
		Moving,
		Resting,
	}

	private FlyState _state = FlyState.Moving;
	private Vector2 _home;
	private Vector2 _target;
	private float _restTimer;
	private float _phase;

	public override void _Ready()
	{
		base._Ready();

		_home = GlobalPosition;
		PickTarget();
	}

	protected override void MoveBehaviour(float delta)
	{
		_phase += delta;

		switch (_state)
		{
			case FlyState.Moving:
				UpdateMoving(delta);
				break;

			case FlyState.Resting:
				UpdateResting(delta);
				break;
		}
	}

	private void UpdateMoving(float delta)
	{
		Vector2 toTarget = _target - GlobalPosition;
		if (toTarget.Length() <= ArriveDistance)
		{
			EnterResting();
			return;
		}

		Velocity = toTarget.Normalized() * FlySpeed;
		MoveAndSlide();

		// 撞到东西了（墙、地面、天花板）就马上换一个目标点
		if (IsOnWall() || IsOnFloor() || IsOnCeiling())
		{
			PickTarget();
		}
	}

	private void UpdateResting(float delta)
	{
		_restTimer -= delta;

		Velocity = BobWhenResting
			? new Vector2(0f, Mathf.Sin(_phase * BobFrequency * Mathf.Tau) * BobAmplitude)
			: Vector2.Zero;
		MoveAndSlide();

		if (_restTimer <= 0f)
		{
			PickTarget();
		}
	}

	private void EnterResting()
	{
		_state = FlyState.Resting;
		Velocity = Vector2.Zero;
		_restTimer = (float)GD.RandRange(RestMinTime, RestMaxTime);
	}

	private void PickTarget()
	{
		_state = FlyState.Moving;
		_target = _home + new Vector2(
			(float)GD.RandRange(-WanderExtents.X, WanderExtents.X),
			(float)GD.RandRange(-WanderExtents.Y, WanderExtents.Y));
	}
}
