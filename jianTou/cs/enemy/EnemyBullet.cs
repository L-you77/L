using Godot;

/// <summary>
/// 敌人的远程弹体：直线飞行，打到玩家或撞到地形就消失。
///
/// 伤害走统一的 HitBox → HurtBox 链路：根节点只管飞行，子节点 HitBox 负责判定。
/// 由 <see cref="EnemyFar"/> 生成并挂到关卡上（不是敌人的子节点，否则会跟着敌人飞）。
/// </summary>
public partial class EnemyBullet : Node2D
{
	/// <summary>飞行速度（像素/秒）。</summary>
	[Export] public float Speed = 90f;

	/// <summary>伤害。</summary>
	[Export] public int Damage = 1;

	/// <summary>存活时间（秒），超时自动消失。</summary>
	[Export] public float Lifetime = 3.5f;

	/// <summary>飞行方向（归一化后使用）。由发射者设置。</summary>
	public Vector2 Direction = Vector2.Right;

	private HitBox _hitBox;
	private float _life;

	public override void _Ready()
	{
		_life = Lifetime;

		// 贴图朝向飞行方向（贴图本身是朝右的菱形）
		Rotation = Direction.Angle();

		_hitBox = GetNodeOrNull<HitBox>("HitBox");
		if (_hitBox != null)
		{
			_hitBox.Damage = Damage;
			_hitBox.IsStomp = false;
			_hitBox.HitLanded += OnHitLanded;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		_life -= dt;
		if (_life <= 0f)
		{
			QueueFree();
			return;
		}

		Vector2 from = GlobalPosition;
		Vector2 to = from + Direction * Speed * dt;

		// 撞墙/地面就消失，别穿墙打人
		if (HitsWorld(from, to))
		{
			QueueFree();
			return;
		}

		GlobalPosition = to;
	}

	/// <summary>打中玩家：消失（可以在这里加命中特效/音效）。</summary>
	private void OnHitLanded(HurtBox target)
	{
		QueueFree();
	}

	private bool HitsWorld(Vector2 from, Vector2 to)
	{
		PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(from, to, GameLayers.WorldMask);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;

		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
	}
}
