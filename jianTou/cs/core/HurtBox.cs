using Godot;

/// <summary>
/// 受击区域。挂在角色身上，被对方的 HitBox 检测到后：
///   1. 校验能否吃下这次伤害（无敌 / 不可踩 / 踩踏方向是否成立）；
///   2. 自动调用父节点的 <see cref="IDamageable.TakeDamage"/>（父节点实现接口即可，不用手连信号）；
///   3. 发出 <see cref="Hurt"/> 信号，给 UI、音效等外部监听者用。
/// </summary>
public partial class HurtBox : Area2D
{
	/// <summary>无敌：置 true 时忽略一切伤害（留给“敌人不可杀”之类的后续开关）。</summary>
	[Export] public bool Invincible = false;

	/// <summary>是否允许被踩踏。关掉后就踩不动它了。</summary>
	[Export] public bool Stompable = true;

	/// <summary>踩踏成立所需的最小高度差（攻击方中心必须比自身中心高出这么多像素）。</summary>
	[Export] public float MinStompHeight = 1f;

	/// <summary>成功受到一次伤害时发出，供外部监听。</summary>
	[Signal] public delegate void HurtEventHandler(int amount, HitBox source);

	public override void _Ready()
	{
		// 只被别人检测，自己不去检测别人
		Monitoring = false;
		Monitorable = true;
	}

	/// <summary>
	/// 接受来自 <paramref name="source"/> 的一次命中。
	/// </summary>
	/// <returns>是否真的吃下了这次伤害（false = 被无敌/不可踩/方向不对挡掉了）。</returns>
	public bool ReceiveHit(HitBox source)
	{
		if (source == null || Invincible)
		{
			return false;
		}

		if (!IsInsideTree() || IsQueuedForDeletion())
		{
			return false;
		}

		if (source.IsStomp && (!Stompable || !IsStompedFromAbove(source)))
		{
			return false;
		}

		(GetParent() as IDamageable)?.TakeDamage(source.Damage, source);
		EmitSignal(SignalName.Hurt, source.Damage, source);
		return true;
	}

	/// <summary>踩踏成立条件：攻击方中心在自身上方，且正在下落。</summary>
	private bool IsStompedFromAbove(HitBox source)
	{
		if (source.OwnerBody is not Node2D attacker || GetParent() is not Node2D owner)
		{
			return true; // 判断不了就放行，避免误伤
		}

		if (attacker.GlobalPosition.Y > owner.GlobalPosition.Y - MinStompHeight)
		{
			return false; // 站在旁边同高度，不算踩
		}

		if (attacker is CharacterBody2D body && body.Velocity.Y < 0f)
		{
			return false; // 还在往上窜，不算踩
		}

		return true;
	}
}
