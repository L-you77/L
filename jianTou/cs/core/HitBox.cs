using Godot;

/// <summary>
/// 伤害源（攻击/踩踏判定）。挂在角色身上，作为**检测方**主动去找对方的 HurtBox。
///
/// 约定：
///   · HitBox 只负责“检测 + 转交”，不关心对方怎么掉血；
///   · 检测到 HurtBox 且对方接受了这次伤害时，会发出 <see cref="HitLanded"/>，
///     攻击方可以借此做反弹、连击、音效等反馈（例如玩家踩到敌人后弹起）。
/// </summary>
public partial class HitBox : Area2D
{
	/// <summary>伤害值。</summary>
	[Export] public int Damage = 1;

	/// <summary>
	/// 是否为“踩踏”判定。为 true 时，受击方会额外校验：
	/// 必须从上方落下才能生效（见 <see cref="HurtBox.ReceiveHit"/>）。
	/// </summary>
	[Export] public bool IsStomp = false;

	/// <summary>发出伤害的角色本体，默认取父节点。</summary>
	public Node2D OwnerBody => GetParent() as Node2D;

	/// <summary>成功打中对方（对方确实接受了这次伤害）时发出。</summary>
	[Signal] public delegate void HitLandedEventHandler(HurtBox target);

	public override void _Ready()
	{
		// 只用它来检测别人，不让别人检测它，避免同一次命中被处理两遍
		Monitoring = true;
		Monitorable = false;
		AreaEntered += OnAreaEntered;
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is not HurtBox hurtBox)
		{
			return;
		}

		if (hurtBox.ReceiveHit(this))
		{
			EmitSignal(SignalName.HitLanded, hurtBox);
		}
	}
}
