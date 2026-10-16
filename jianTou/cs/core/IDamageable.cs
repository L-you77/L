using Godot;

/// <summary>
/// 可承伤对象。挂在 HurtBox 的父节点上就会被自动找到，不需要手动连信号。
/// </summary>
public interface IDamageable
{
	/// <param name="amount">伤害值</param>
	/// <param name="source">伤害来源的 HitBox（可用来取来源角色、判断是否为踩踏）</param>
	void TakeDamage(int amount, HitBox source);
}
