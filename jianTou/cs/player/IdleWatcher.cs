using Godot;

/// <summary>
/// 静止监视（挂在玩家身上）。
///
/// 流程：玩家连续静止 10 秒 → 头顶闪 3 秒 warn(!) → 身体被白色方块盖住，并浮出一双眼睛。
/// 眼睛行为：
///   · 静止时在体内随机游走（每 0.4~1.2 秒换一个随机点，缓慢飘过去）；
///   · 移动时朝移动方向偏移（贴到身体内缘），速度按方向实时变化。
///
/// 注意：触发之后是**永久**状态（否则"移动时眼睛跟着方向走"就没机会看到了）。
/// 想让它变成可循环的，把 ResetOnMove 打开即可。
/// </summary>
public partial class IdleWatcher : Node2D
{
	[ExportGroup("计时")]

	/// <summary>需要连续静止多少秒才触发 warn。</summary>
	[Export] public float IdleTimeToWarn = 10f;

	/// <summary>warn 显示多少秒。</summary>
	[Export] public float WarnDuration = 3f;

	/// <summary>速度低于这个值算"静止"（像素/秒）。</summary>
	[Export] public float IdleSpeedThreshold = 1f;

	/// <summary>warn 期间玩家一移动就取消（默认不取消：警告一旦亮起就来不及了）。</summary>
	[Export] public bool CancelWarnOnMove = false;

	/// <summary>
	/// warn 期间把玩家自己的箭头贴图（AnimatedSprite2D）设为不可见，让感叹号的白块顶替角色。
	/// 关掉则感叹号叠在箭头上、箭头一直可见。
	/// </summary>
	[Export] public bool HideArrowDuringWarn = true;

	/// <summary>眼睛出现后玩家一移动就隐藏、重新开始计时。</summary>
	[Export] public bool ResetOnMove = false;

	[ExportGroup("眼睛")]

	/// <summary>静止时随机游走的半径（像素，别超过身体一半）。</summary>
	[Export] public float WanderRadius = 1.5f;

	/// <summary>随机游走的换点间隔（秒）。</summary>
	[Export] public float WanderRetargetMin = 0.4f;
	[Export] public float WanderRetargetMax = 1.2f;

	/// <summary>随机游走的移动速度（像素/秒）。</summary>
	[Export] public float WanderSpeed = 3f;

	/// <summary>移动时眼睛朝方向偏移的最大距离（像素）。</summary>
	[Export] public float FollowDistance = 1.8f;

	/// <summary>跟随方向时的收缩速度（像素/秒）。</summary>
	[Export] public float FollowSpeed = 14f;

	/// <summary>速度超过这个值算"在移动"（像素/秒）。</summary>
	[Export] public float MoveThreshold = 4f;

	private enum State
	{
		Idle,       // 正常等待
		Warning,    // 正在闪 ! 
		Revealed,   // 白色身体 + 眼睛
	}

	private State _state = State.Idle;

	private Player _player;
	private AnimatedSprite2D _playerSprite;
	private Sprite2D _warn;
	private Sprite2D _body;
	private Sprite2D _eye;

	private Vector2 _warnBaseScale = Vector2.One;
	private Vector2 _warnBasePosition;
	private Vector2 _eyeOffset;
	private Vector2 _eyeTarget;
	private float _idleTimer;
	private float _warnTimer;
	private float _pulseTime;
	private float _retargetTimer;

	public override void _Ready()
	{
		_player = GetParentOrNull<Player>();
		_playerSprite = _player?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_warn = GetNodeOrNull<Sprite2D>("Warn");
		_body = GetNodeOrNull<Sprite2D>("BlankBody");
		_eye = GetNodeOrNull<Sprite2D>("Eye");

		if (_warn != null)
		{
			_warnBaseScale = _warn.Scale;
			_warnBasePosition = _warn.Position;
		}

		HideAll();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_player == null || !_player.IsAlive)
		{
			_idleTimer = 0f;
			return;
		}

		// 贴地时忽略纵向速度残留（落地后 Velocity.Y 可能有极小残值，会让"静止"判定永远不成立）
		bool onFloor = _player.IsOnFloor();
		Vector2 velocity = _player.Velocity;
		Vector2 direction = new Vector2(velocity.X, onFloor ? 0f : velocity.Y);
		bool moving = direction.Length() > MoveThreshold;

		switch (_state)
		{
			case State.Idle:
				_idleTimer = moving ? 0f : _idleTimer + dt;
				if (_idleTimer >= IdleTimeToWarn)
				{
					EnterWarning();
				}
				break;

			case State.Warning:
				_pulseTime += dt;
				UpdateWarnPulse();

				if (CancelWarnOnMove && moving)
				{
					HideAll();
					_idleTimer = 0f;
					break;
				}

				_warnTimer -= dt;
				if (_warnTimer <= 0f)
				{
					Reveal();
				}
				break;

			case State.Revealed:
				if (ResetOnMove && moving)
				{
					HideAll();
					_idleTimer = 0f;
					break;
				}
				UpdateEye(dt, moving, direction);
				break;
		}
	}

	/// <summary>外部需要重置时调用（比如复活）。</summary>
	public void ResetWatcher()
	{
		_idleTimer = 0f;
		_warnTimer = 0f;
		_state = State.Idle;
		_eyeOffset = Vector2.Zero;
		HideAll();
	}

	private void EnterWarning()
	{
		_state = State.Warning;
		_warnTimer = WarnDuration;
		_pulseTime = 0f;

		if (_warn != null)
		{
			_warn.Visible = true;
			_warn.Modulate = Colors.White;
		}

		// 可选：warn 期间只用感叹号替代角色
		if (HideArrowDuringWarn && _playerSprite != null)
		{
			_playerSprite.Visible = false;
		}
	}

	private void UpdateWarnPulse()
	{
		if (_warn == null)
		{
			return;
		}

		// 缩放脉冲 + 闪烁
		float pulse = 1f + Mathf.Sin(_pulseTime * 12f) * 0.15f;
		_warn.Scale = _warnBaseScale * pulse;
		_warn.Position = _warnBasePosition + new Vector2(0f, -Mathf.Abs(Mathf.Sin(_pulseTime * 6f)) * 1.5f);

		float alpha = Mathf.Sin(_pulseTime * 18f) > 0f ? 1f : 0.35f;
		_warn.Modulate = new Color(1f, 1f, 1f, alpha);
	}

	private void Reveal()
	{
		_state = State.Revealed;

		if (_warn != null)
		{
			_warn.Visible = false;
			_warn.Scale = _warnBaseScale;
			_warn.Position = _warnBasePosition;
		}
		if (_body != null)
		{
			_body.Visible = true;
		}
		if (_eye != null)
		{
			_eye.Visible = true;
		}

		_eyeOffset = Vector2.Zero;
		_eyeTarget = Vector2.Zero;
		_retargetTimer = 0f;

		GD.Print("[IdleWatcher] 静止太久，眼睛出现了");
	}

	/// <summary>眼睛在体内游走 / 朝移动方向偏移。</summary>
	private void UpdateEye(float dt, bool moving, Vector2 direction)
	{
		if (_eye == null)
		{
			return;
		}

		if (moving)
		{
			// 移动：朝速度方向顶到身体内缘
			_eyeTarget = direction.Normalized() * FollowDistance;
			_eyeOffset = _eyeOffset.MoveToward(_eyeTarget, FollowSpeed * dt);
		}
		else
		{
			// 静止：在体内随机游走
			_retargetTimer -= dt;
			if (_retargetTimer <= 0f)
			{
				float angle = (float)GD.RandRange(0.0, Mathf.Tau);
				float distance = (float)GD.RandRange(0.0, WanderRadius);
				_eyeTarget = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
				_retargetTimer = (float)GD.RandRange(WanderRetargetMin, WanderRetargetMax);
			}

			_eyeOffset = _eyeOffset.MoveToward(_eyeTarget, WanderSpeed * dt);
		}

		// 兜底：别让眼睛跑出身体
		float limit = WanderRadius + FollowDistance * 0.6f;
		if (_eyeOffset.Length() > limit)
		{
			_eyeOffset = _eyeOffset.Normalized() * limit;
		}

		_eye.Position = _eyeOffset;
	}

	private void HideAll()
	{
		// 任何重置都要把玩家的箭头贴图放回来
		if (_playerSprite != null)
		{
			_playerSprite.Visible = true;
		}

		if (_warn != null)
		{
			_warn.Visible = false;
			_warn.Scale = _warnBaseScale;
			_warn.Position = _warnBasePosition;
			_warn.Modulate = Colors.White;
		}
		if (_body != null)
		{
			_body.Visible = false;
		}
		if (_eye != null)
		{
			_eye.Visible = false;
			_eye.Position = Vector2.Zero;
		}
	}
}
