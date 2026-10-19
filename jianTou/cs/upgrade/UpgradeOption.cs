/// <summary>
/// 强化的具体效果类型。玩家真正执行的是这个，UI 只负责展示。
/// </summary>
public enum UpgradeEffect
{
	/// <summary>向右移动速度提升。</summary>
	SpeedRight,
	/// <summary>向左移动速度提升。</summary>
	SpeedLeft,
	/// <summary>跳跃高度提升。</summary>
	JumpHeight,
	/// <summary>向下爬梯速度提升（也用来放出"向下"按钮）。</summary>
	DownSpeed,
	/// <summary>解锁近战攻击（J）。</summary>
	UnlockNearHit,
	/// <summary>近战伤害 +1。</summary>
	NearHitDamage,
	/// <summary>近战攻击间隔缩短。</summary>
	NearHitSpeed,
	/// <summary>解锁远程攻击（U）。</summary>
	UnlockFarHit,
	/// <summary>远程伤害 +1。</summary>
	FarHitDamage,
	/// <summary>远程攻击间隔缩短。</summary>
	FarHitSpeed,
}

/// <summary>
/// 一张强化卡：图标 + 标题 + 效果说明（说明文字就是"加什么"的标签）。
/// </summary>
public sealed class UpgradeOption
{
	/// <summary>选中后对玩家生效的效果。</summary>
	public UpgradeEffect Effect { get; init; }

	/// <summary>卡片大标题，例如"向右加速"。</summary>
	public string Title { get; init; } = string.Empty;

	/// <summary>标题下方的效果标签，例如"向右速度 +15%"。</summary>
	public string Desc { get; init; } = string.Empty;

	/// <summary>卡片图标资源路径。</summary>
	public string IconPath { get; init; } = string.Empty;

	/// <summary>是否是"获得武器"这类解锁卡（UI 可以给它加点强调色）。</summary>
	public bool IsUnlock { get; init; }
}
