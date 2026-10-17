# JianTou 项目长期约定

## 游戏定位
- Godot 4.6 + C#（net8.0），横版跳跃平台游戏，**没有冲刺**。
- 玩家是"箭头"：8 张方向箭头贴图按**速度的 x/y 组合**切换（含 4 个斜向），
  完全静止时用 `JiaZai100.png`（加载转圈图）作 idle。动画名固定为：
  `idle / up / down / left / right / left_up / right_up / left_down / right_down`。
- 战斗规则：血量 + 受击无敌帧 + 从上方踩踏消灭敌人。**后续可能要做"敌人不可杀"**，
  预留开关在 EnemyBase 的 `CanBeKilled` / `CanBeStomped`（Inspector 可改，不用动代码）。

## 代码约定
- 伤害链路固定为：`HitBox`（检测方，monitoring=true/monitorable=false）
  → 找到对方 `HurtBox.ReceiveHit()` → 自己校验（无敌/不可踩/是否从上方落下）
  → 调用父节点的 `IDamageable.TakeDamage()` → 同时发 `Hurt` 信号给 UI/音效。
  改伤害规则只改 `HurtBox`/`HitBox`，不要在角色脚本里再写一套。
- 角色脚本实现 `IDamageable` 即可自动收到伤害，**不需要**手动连 HurtBox 信号。
- 碰撞层统一定义在 `cs/core/GameLayers.cs`，场景里也会设一遍（双保险，注释里说明过）：
  1 地形 / 2 平台 / 4 玩家本体 / 8 敌人本体 / 16 玩家攻击(踩踏) / 32 敌人伤害 / 64 玩家受击 / 128 敌人受击。
- 玩家和敌人**身体互不碰撞**（可以穿过），伤害与踩踏全走 Area2D 判定。
- 移动平台用 `AnimatableBody2D`（**不要**改回 CharacterBody2D），`sync_to_physics = true`。
- 敌人几何数据从场景的 `RectangleShape2D` 读（`HalfExtents`），改了碰撞盒尺寸不用改代码。
- 注释与 Inspector 分组用中文，`[ExportGroup]` + `[Export]` 便于在编辑器里调参。

## 环境
- dotnet 10.0.203 可用；`dotnet build` 能离线还原并编译，可用来做编译验证。
- Godot 编辑器本体不在命令行环境里，改场景后只能用脚本校验 .tscn 结构（脚本见当日日志思路）。

## 关卡坐标速查（2026-10-17 版，改地形后要重新核对）
- Tile 8px；主地面 y=240，`FallDeathY=470`（地下层已开放，死亡线挪到地图外 448 之下）。
- 整关是封闭盒子：顶 y=64、地下层地板 y=416~448、左右墙各 3 格。
- 主地面唯一缺口 = 坑 x=512~536（24px 宽），也是进出地下层的**唯一**通道；地下净空 256~416。
- 玩家出生点 (551, 229)，在坑右侧。
- 移动平台运行时位置由 RemoteTransform2D 对齐到 PathFollow2D（不是场景里那个 -19.58 偏移），
  三个平台顶面大致在 y≈212→161 / y≈212 / y≈155。
- 解析 tile_map_data：前 2 字节是头，之后每条 12 字节 `<hhhhhh`=(x,y,source,atlas_x,atlas_y,alt)；
  **只有 atlas_x==0 的图块带物理多边形**。

## 道具约定
- 金币计数存在 `Player.Coins`（内存，重开关卡归零），`AddCoin()` + `CoinChanged` 信号。
- `Coin` 用 `BodyEntered` 检测玩家**本体**（layer 0 / mask 4=PlayerBody / monitorable=false），
  不参与任何伤害层，无敌帧期间照样能捡。收集后 player.AddCoin → 发 Collected 信号 → 放大淡出 → QueueFree。
