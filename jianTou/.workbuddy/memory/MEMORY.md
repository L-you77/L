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

## 场景结构（2026-10-18 版）
- 启动场景 = `node/scene/home.tscn`（HomeMenu.cs）→ **home_2**（Home2Menu.cs）→ 教学选关 `home_jiaoxue.tscn`
  或主线 `node_1.tscn`。教学四关：`node_right`(第一关/向右) / `node_left`(第二关/向左) /
  `node_up`(第三关/向上) / `node_near_hit`(第四关/近战，真随机)。
- 场景路径统一走 `cs/core/ScenePaths.cs`，不要在别处硬编码 `res://node/scene/...`。
- UI = `node/ui/ui.tscn`，在关卡里挂在一个 `CanvasLayer` 下（不能直接挂 Node2D，会跟着相机缩放）。
- **相机 zoom = 5，关卡里不要放 Label**：文字会按小字号光栅化再放大 5 倍，必然模糊。
  所有提示文字走 UI 层的屏幕空间 Label（教学关卡用 `TutorialLevel.HintText` → `TutorialUi.SetHint`）。
- 角色/敌人/道具/机关分别在 `node/{player,enemy,organ}`；脚本在 `cs/{player,enemy,organ,ui,core,scene,upgrade}`。
- **中文字体**：`ps/font/simhei.ttf`（从系统复制）+ `project.godot` 的 `[gui] theme/custom_font`，
  所以现在可以放心用中文 Label。新加的字体/贴图需要编辑器扫一次才会导入。

## 强化与教学关卡约定（2026-10-18 版）
- 强化 = `UpgradeEffect` 枚举 + `UpgradeOption` 卡片；**效果只由 `Player.ApplyUpgrade()` 执行**，
  UI 不知道具体数值，改强度只改 Player 上的导出字段（SpeedUpPerLevel / JumpUpPerLevel /
  CooldownDownPerLevel / DamageUpPerLevel）。
- 左右移速**各算各的**（RightSpeedMultiplier / LeftSpeedMultiplier），不要合并成一个。
- 武器是解锁制：`HasNearHit` / `HasFarHit` 为 false 时 J/U 直接 return；
  主线 `player.tscn` 用 `UnlockAttacksAtStart=true` 保持原行为，教学用 `player2.tscn`（继承场景）关掉。
- 教学关卡根节点挂 `cs/scene/TutorialLevel.cs`，`Mode` 决定抽卡（Random/ForceRight/ForceLeft/ForceUp）。
  **强化次数由 coinSpe 决定**：`Player.UpgradeCharges`，捡一个点一次（`CoinSpe` → `AddUpgradeCharge`），
  关卡里放几个 coinSpe 就是几次强化。选完自动放出对应按钮（SpeedRight→TBtnR … UnlockNearHit→TBtnNH）。
- 触摸按钮属于 UI，不在关卡里：`node/ui/tutorial_ui.tscn`（TBtnL/TBtnR/TBtnNH/TBtnU + 暂停），
  按钮只绑 `action`（Left/Right/Up/NearHit），逻辑复用 Player。
  初始可见性由 `TutorialLevel` 的 `ShowRightButton/ShowLeftButton/ShowUpButton/ShowNearHitButton` 控制；
  **不要**在 `_Ready` 里无条件隐藏所有教学按钮（会误藏 node_left 里本该可见的 TBtnR）。
- 三选一 UI 是运行时实例化的 `node/ui/upgrade_panel.tscn`（CanvasLayer layer=10，弹出暂停）。
  **不强制选一项**：Esc 关掉算放弃；扣次数/扣币都在"真正选中"那一刻（商城例外，见下）。
  教学暂停面板在同一 CanvasLayer 里（layer=5），Esc 遇到强化界面开着时会跳过。
- **三选一刷新要花 coin**：`RefreshBaseCost`(4) + `RefreshCostStep`(2) × 已刷次数 → 4/6/8/10…
  每次弹窗从第一次重新算；`Open()` 第 6 个参数 `payRefresh` 决定扣谁的钱（关卡扣 `Player.Coins`，
  商城扣 `GameState.Coins`）。钱不够不重抽，底部红字提示。刷新成功不弹文字（按钮上写着价格）。
  商城的 8 coin 是**打开面板时先扣、取消时退回**，免得刷新把钱花掉导致选卡时扣不动。

## 敌人类型（都继承 EnemyBase，能踩死）
- `Enemy`（enemy.tscn）：地面巡逻，射线探墙/探边缘掉头。
- `EnemyFly`（enemy_fly.tscn）：`motion_mode=1` 不吃重力，矩形范围随机游荡。
- `EnemyFar`（enemy_far.tscn）：不吃重力，左右往返 + 视线内蓄力射弹体（`enemy_bullet.tscn`，`EnemyBullet.cs`）。
  弹体挂到**关卡**上而不是敌人身上，否则会跟着敌人飞。
- 白底贴图（warn/farHit 那种）当"会动的物件"会画成白方块，需要另做透明底贴图（如 `ps/enemy/enemy_bullet.png`）。

## 关卡坐标速查（2026-10-17 版，改地形后要重新核对）
- Tile 8px；主地面 y=240，`FallDeathY=470`（地下层已开放，死亡线挪到地图外 448 之下）。
- 整关是封闭盒子：顶 y=64、地下层地板 y=416~448、左右墙各 3 格。
- 主地面唯一缺口 = 坑 x=512~536（24px 宽），也是进出地下层的**唯一**通道；地下净空 256~416。
- 玩家出生点 (551, 229)，在坑右侧。
- 移动平台运行时位置由 RemoteTransform2D 对齐到 PathFollow2D（不是场景里那个 -19.58 偏移），
  三个平台顶面大致在 y≈212→161 / y≈212 / y≈155。
- 解析 tile_map_data：前 2 字节是头，之后每条 12 字节 `<hhhhhh`=(x,y,source,atlas_x,atlas_y,alt)；
  **只有 atlas_x==0 的图块带物理多边形**。

## 机关 SwitchGate（2026-10-19 版）
- `cs/organ/SwitchGate.cs` 直接挂在关卡里的色块标记 Sprite2D（R / B）上；
  **触发 = 玩家角色碰到**（脚本 `_Ready` 里自建圆形 `Area2D`：layer 0 / mask = `GameLayers.PlayerBody`，
  并把 Area 的 scale 取倒数抵消精灵的 `(1,1.75)` 缩放）。不需要按键、不需要点鼠标。
- 效果分四类，可混用：
  `HideOnUse`（立刻隐藏）/ `SlideDownOnUse`（往下滑 `SlideDistance` + 淡出后隐藏，**不动 TileMapLayer**）/
  `RevealOnUse`（进关全关，触发后出现）/ `StartOnUse`（进关可见+有碰撞但动画冻住，触发后开始动）。
  `EraseCells` 保留但默认 Size=0，**不要再拿它改地形**。
- 移动平台靠 `AnimationPlayer` 驱动 `PathFollow2D:progress_ratio`，冻结用 `SpeedScale = 0`
  （**不要 Stop()**，Stop 会清 CurrentAnimation）；启动用 `Play()` + `SpeedScale = 1`。
- 开关触发后**自己不消失**，只把触碰 Area 的 `Monitoring` 关掉。

## 主线关卡 node_1 的触屏按钮
- node_1 挂了一份 `node/ui/tutorial_ui.tscn` 实例，`ShowAllButtonsOnStart = true` → 左右上下 + 近战 + 远程全开。
- 主线已有 `ui.tscn` 的 PauseMenu，所以这份 UI 用 `EnablePauseButton = false` 关掉自带暂停键；
  暂停时靠 `TutorialUi._Process` 检测 `GetTree().Paused` 把触摸层藏起来（两个暂停源不会打架）。

## 道具约定
- 金币计数存在 `Player.Coins`（内存，重开关卡归零），`AddCoin()` / `TrySpendCoins()` + `CoinChanged` 信号。
- **带出规则**：关卡靠 `TongGuan` 通关时 `GameState.BankCoins(player.Coins)`；**只有无限跑酷是例外**——
  `EndlessRunner.BankRunCoins()` 在死亡时和 `_ExitTree`（中途退出/重开）都会结算，`_bankedCoins` 保证只算一次。
- **教学全通奖励**：`GameState.RecordTutorialClear(levelPath)`，关卡路径用 `GetTree().CurrentScene.SceneFilePath`
  （**不要用 TongGuan 自己的 `SceneFilePath`**，那是 `tong_guan.tscn`）。集齐 `ScenePaths.AllTutorialLevels`
  六关 → 一次性 `AddCoins(20)`，存档 `[tutorial] clears / reward_claimed`。
- `Coin` 用 `BodyEntered` 检测玩家**本体**（layer 0 / mask 4=PlayerBody / monitorable=false），
  不参与任何伤害层，无敌帧期间照样能捡。收集后 player.AddCoin → 发 Collected 信号 → 放大淡出 → QueueFree。
