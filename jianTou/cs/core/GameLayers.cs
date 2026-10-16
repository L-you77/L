using Godot;

/// <summary>
/// 全局 2D 物理层定义（只此一处，改这里即可整体换层）。
/// 建议同时到「项目设置 → Layer Names → 2D Physics」把名字填成一样的，编辑器里排查碰撞会方便很多。
///
/// 位序       值    用途
/// Layer 1    1     地形（TileMapLayer）
/// Layer 2    2     可站立平台（TileSet / 移动平台）
/// Layer 3    4     玩家本体
/// Layer 4    8     敌人本体
/// Layer 5    16    玩家的攻击判定（踩踏）
/// Layer 6    32    敌人的伤害判定
/// Layer 7    64    玩家的受击区域
/// Layer 8    128   敌人的受击区域
/// </summary>
public static class GameLayers
{
	public const uint World = 1 << 0;   // 1   地形
	public const uint Platform = 1 << 1; // 2   平台
	public const uint PlayerBody = 1 << 2;   // 4   玩家本体
	public const uint EnemyBody = 1 << 3;   // 8   敌人本体
	public const uint PlayerAttack = 1 << 4;   // 16  玩家攻击/踩踏
	public const uint EnemyAttack = 1 << 5;   // 32  敌人伤害
	public const uint PlayerHurt = 1 << 6;   // 64  玩家受击
	public const uint EnemyHurt = 1 << 7;   // 128 敌人受击

	/// <summary>地形 + 平台：任何会“站上去”的东西都在这一组（值为 3，和现有 TileSet 保持一致）。</summary>
	public const uint WorldMask = World | Platform;
}
