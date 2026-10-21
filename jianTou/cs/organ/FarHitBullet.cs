using Godot;

/// <summary>
/// 远程弹体（farHit）：直线飞行，打到敌人或撞到地形就消失。
///
/// 伤害走的是统一的 HitBox → HurtBox 链路：根节点只管飞行，子节点 HitBox 负责判定伤害。
/// 由 <see cref="Player.TryFarHit"/> 生成并挂到关卡里（不是玩家的子节点，否则会跟着玩家跑）。
/// </summary>
public partial class FarHitBullet : Node2D
{
	/// <summary>飞行速度（像素/秒）。实际值由 <see cref="Player.TryFarHit"/> 按角色速度算好后赋值。</summary>
	[Export] public float Speed = 135f;

	/// <summary>伤害。</summary>
	[Export] public int Damage = 1;

	/// <summary>存活时间（秒），超时自动消失。</summary>
	[Export] public float Lifetime = 1.5f;

	/// <summary>朝左还是朝右：1 = 右，-1 = 左。</summary>
	public int Direction = 1;

	private HitBox _hitBox;
	private float _life;

	public override void _Ready()
	{
		_life = Lifetime;

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
		Vector2 to = from + new Vector2(Direction * Speed * dt, 0f);

		// 撞墙就消失，别穿墙
		if (HitsWorld(from, to))
		{
			QueueFree();
			return;
		}

		GlobalPosition = to;
	}

	/// <summary>打中敌人：消失（可以在这里加命中特效/音效）。</summary>
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
