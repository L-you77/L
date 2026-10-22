using System.Collections.Generic;
using Godot;

/// <summary>
/// 无限跑酷（类似天天酷跑）：地形一边往前生成、一边把身后的删掉，玩家自动向右跑。
///
/// 操作：↑ 跳 · ↓ 蹲（钻过矮杆）· J 近战 · U 远程。
///
/// 地形由 <see cref="ChunkWidth"/> 格一块的"块"拼成（1 格 = 8px）：
///   · 每块先铺地面，再按概率塞一种障碍（沟 / 尖刺 / 矮杆 / 敌人）和几枚币；
///   · 生成到玩家前方 <see cref="GenerateAhead"/>，删掉身后 <see cref="CleanupBehind"/> 以外的块。
/// 矮杆是运行时拼出来的 Area2D（HitBox，layer=EnemyAttack/mask=PlayerHurt），
/// 判定盒比角色矮 4px —— 站着撞上去会死，蹲下就能钻过去。
/// </summary>
public partial class EndlessRunner : Node2D
{
	[ExportGroup("地形")]
	/// <summary>地形层节点路径。</summary>
	[Export] public string TileMapPath = "TileMapLayer";

	/// <summary>地面表面所在的 tile 行（这一行是实心的第一行）。</summary>
	[Export] public int SurfaceRow = 0;

	/// <summary>一块地形的宽度（格）。</summary>
	[Export] public int ChunkWidth = 8;

	/// <summary>提前生成多远（像素）。</summary>
	[Export] public float GenerateAhead = 420f;

	/// <summary>身后多远以外的块删掉（像素）。</summary>
	[Export] public float CleanupBehind = 320f;

	/// <summary>开局前几块保持空白，给玩家适应。</summary>
	[Export] public int SafeChunks = 3;

	[ExportGroup("生成内容")]
	[Export] public string SpikeScenePath = "res://node/organ/jian_ci.tscn";
	[Export] public string EnemyScenePath = "res://node/enemy/enemy.tscn";
	[Export] public string CoinScenePath = "res://node/organ/coin.tscn";
	[Export] public string CoinSpeScenePath = "res://node/organ/coin_spe.tscn";
	[Export] public string CoinWScenePath = "res://node/organ/coin_w.tscn";

	/// <summary>每块地形出现 CoinW（挨打掉币的罐子）的概率。</summary>
	[Export] public float CoinWChance = 0.14f;

	/// <summary>跑到多少距离解锁"酷跑玩家"成就（HUD 里的距离数值）。</summary>
	[Export] public int RunnerGoalDistance = 1000;
	[Export] public string UpgradePanelScenePath = "res://node/ui/upgrade_panel.tscn";

	/// <summary>每块地形出现强化点的概率。</summary>
	[Export] public float CoinSpeChance = 0.12f;
	[Export] public string BarTexturePath = "res://ps/tileMap/Write8.png";

	/// <summary>矮杆宽度 / 厚度 / 离地间隙（像素）。</summary>
	[Export] public float BarWidth = 32f;
	[Export] public float BarThickness = 4f;
	[Export] public float BarGap = 4f;

	[ExportGroup("难度")]
	/// <summary>开局跑速（会写回玩家的 MoveSpeed）。</summary>
	[Export] public float StartSpeed = 90f;

	/// <summary>每多一块地形加速多少。</summary>
	[Export] public float AddSpeedPerChunk = 1.2f;

	/// <summary>速度上限。</summary>
	[Export] public float MaxSpeed = 170f;

	[ExportGroup("界面")]
	[Export] public string DistanceLabelPath = "RunnerHud/DistanceLabel";
	[Export] public string GameOverPanelPath = "RunnerHud/GameOverPanel";
	[Export] public string GameOverLabelPath = "RunnerHud/GameOverPanel/ResultLabel";
	[Export] public string HealthLabelPath = "RunnerHud/HealthLabel";

	/// <summary>"返回"回哪一屏。</summary>
	[Export] public string BackScenePath = ScenePaths.Home2;

	private sealed class Chunk
	{
		public int StartX;
		public int EndX;
		public readonly List<Node> Nodes = new();
	}

	/// <summary>占用暂停状态的界面共用的分组（暂停界面看到它就不要再抢 Esc）。</summary>
	public const string PauseBlockerGroup = TutorialUi.PauseBlockerGroup;

	private readonly List<Chunk> _chunks = new();
	private readonly RandomNumberGenerator _rng = new();

	private TileMapLayer _map;
	private Player _player;
	private Label _distanceLabel;
	private Control _gameOverPanel;
	private Label _gameOverLabel;

	private PackedScene _spikeScene;
	private PackedScene _enemyScene;
	private PackedScene _coinScene;
	private PackedScene _coinSpeScene;
	private PackedScene _coinWScene;
	private Label _healthLabel;
	private UpgradePanel _upgradePanel;
	private Texture2D _barTexture;

	private int _nextChunkX;
	private int _chunkIndex;
	private int _coins;
	private bool _bankedCoins;
	private float _startX;
	private bool _dead;

	/// <summary>已经跑过的格数（HUD 显示用）。</summary>
	public int Distance { get; private set; }

	public override void _Ready()
	{
		_map = GetNodeOrNull<TileMapLayer>(TileMapPath);
		_player = GetTree().GetFirstNodeInGroup(Player.GroupName) as Player;
		_distanceLabel = GetNodeOrNull<Label>(DistanceLabelPath);
		_gameOverPanel = GetNodeOrNull<Control>(GameOverPanelPath);
		_gameOverLabel = GetNodeOrNull<Label>(GameOverLabelPath);
		_healthLabel = GetNodeOrNull<Label>(HealthLabelPath);

		_rng.Randomize();

		if (_gameOverPanel != null)
		{
			// 注意：**不要**把 runner 自己设成 Always。
			// 生成出来的敌人 / 金币都是 runner 的子节点，runner 一旦 Always，
			// 它们会跟着"永远运行"，暂停时敌人还在走、还能撞到（已暂停的）玩家。
			// 只让死亡面板自己 Always 就够了：它在暂停时也要能点。
			_gameOverPanel.Visible = false;
			_gameOverPanel.ProcessMode = ProcessModeEnum.Always;

			// 让暂停界面知道"现在有别的界面占着暂停"，别把 Esc 抢过去
			_gameOverPanel.AddToGroup(PauseBlockerGroup);
		}

		BindButton("RunnerHud/GameOverPanel/RetryButton", Retry);
		BindButton("RunnerHud/GameOverPanel/BackButton", GoBack);

		// 自动跑的模式用不到左右键，把这两个触屏按钮藏掉
		HideTouchButton("TBtnL");
		HideTouchButton("TBtnR");

		if (_player != null)
		{
			_player.Died += OnPlayerDied;
			_player.CoinChanged += OnCoinChanged;
			_player.HealthChanged += OnHealthChanged;
			OnHealthChanged(_player.Health, _player.MaxHealth);
			_startX = _player.GlobalPosition.X;
			_player.MoveSpeed = StartSpeed;

			// 点自己 → 弹三选一（Player2 在没有 TutorialLevel 的场景里会发这个信号）
			if (_player is Player2 player2)
			{
				player2.UpgradeRequested += OpenUpgradePanel;
			}
			else
			{
				GD.PushWarning("[EndlessRunner] player 没有挂 Player2 脚本，点自己不会弹强化界面");
			}
		}

		GenerateUntil(_startX + GenerateAhead);
		UpdateDistanceLabel();
	}

	public override void _Process(double delta)
	{
		if (_dead || _map == null || _player == null || !IsInstanceValid(_player))
		{
			return;
		}

		float px = _player.GlobalPosition.X;

		GenerateUntil(px + GenerateAhead);
		CleanupOldChunks(px - CleanupBehind);
		UpdateSpeed();

		Distance = Mathf.Max(0, Mathf.FloorToInt((px - _startX) / 8f));
		UpdateDistanceLabel();

		if (Distance >= RunnerGoalDistance)
		{
			GameState.Instance?.UnlockRunnerAchievement();
		}
	}

	// ------------------------------------------------------------------
	// 生成
	// ------------------------------------------------------------------

	private void GenerateUntil(float targetX)
	{
		if (_map == null)
		{
			return;
		}

		while (_nextChunkX * 8f < targetX)
		{
			GenerateChunk(_nextChunkX);
			_nextChunkX += ChunkWidth;
		}
	}

	private void GenerateChunk(int startX)
	{
		Chunk chunk = new() { StartX = startX, EndX = startX + ChunkWidth };

		// 地面（两行厚，够玩家踩）
		for (int x = startX; x < startX + ChunkWidth; x++)
		{
			_map.SetCell(new Vector2I(x, SurfaceRow), 0, new Vector2I(0, 0));
			_map.SetCell(new Vector2I(x, SurfaceRow + 1), 0, new Vector2I(0, 0));
		}

		int index = _chunkIndex++;

		if (index >= SafeChunks)
		{
			SpawnFeature(chunk, startX, index);
		}
		else if (index == SafeChunks - 1)
		{
			// 最后一块"安全区"放几枚币，暗示后面有东西
			for (int i = 0; i < 3; i++)
			{
				SpawnCoin(chunk, new Vector2((startX + 2 + i * 2) * 8f + 4f, SurfaceRow * 8f - 12f));
			}
		}

		_chunks.Add(chunk);
	}

	private void SpawnFeature(Chunk chunk, int startX, int index)
	{
		float ground = SurfaceRow * 8f;
		float left = startX * 8f;

		// 越往后越容易出障碍（最多 +15%）
		int bias = Mathf.Min(15, index / 4);
		int roll = _rng.RandiRange(0, 99);

		if (roll < 20 + bias)
		{
			// ① 沟：挖掉中间两格，跳过去
			for (int i = 3; i <= 4; i++)
			{
				_map.EraseCell(new Vector2I(startX + i, SurfaceRow));
				_map.EraseCell(new Vector2I(startX + i, SurfaceRow + 1));
			}

			SpawnCoin(chunk, new Vector2(left + 28f, ground - 14f));
		}
		else if (roll < 45 + bias)
		{
			// ② 尖刺：跳过去
			SpawnSpike(chunk, new Vector2(left + 26f, ground - 3f));
			if (_rng.Randf() < 0.45f)
			{
				SpawnSpike(chunk, new Vector2(left + 34f, ground - 3f));
			}
		}
		else if (roll < 70 + bias)
		{
			// ③ 矮杆：蹲下钻过去
			SpawnBar(chunk, new Vector2(left + 24f + BarWidth * 0.5f, ground - BarGap - BarThickness * 0.5f));
		}
		else if (roll < 88 + bias)
		{
			// ④ 敌人：打掉或者踩死
			SpawnEnemy(chunk, new Vector2(left + 32f, ground - 10f));
		}
		else
		{
			// ⑤ 纯金币串
			for (int i = 0; i < 4; i++)
			{
				SpawnCoin(chunk, new Vector2(left + 12f + i * 10f, ground - 12f));
			}
		}

		// 额外撒一枚币
		if (_rng.Randf() < 0.5f)
		{
			SpawnCoin(chunk, new Vector2(left + _rng.RandfRange(8f, 56f), ground - _rng.RandfRange(10f, 24f)));
		}

		// 强化点（捡到就弹三选一）
		if (_rng.Randf() < CoinSpeChance)
		{
			SpawnCoinSpe(chunk, new Vector2(left + _rng.RandfRange(12f, 52f), ground - 14f));
		}

		// CoinW：打它一下有概率掉币（不吃伤害、也不会伤人）
		if (_rng.Randf() < CoinWChance)
		{
			SpawnCoinW(chunk, new Vector2(left + _rng.RandfRange(14f, 50f), ground - 14f));
		}
	}

	private void SpawnSpike(Chunk chunk, Vector2 position)
	{
		_spikeScene ??= ResourceLoader.Load<PackedScene>(SpikeScenePath);
		Attach(chunk, _spikeScene, position);
	}

	private void SpawnEnemy(Chunk chunk, Vector2 position)
	{
		_enemyScene ??= ResourceLoader.Load<PackedScene>(EnemyScenePath);

		// 跑酷的敌人是无限刷的，不参与"清图"成就，否则杀一只就会被判成清图
		if (Attach(chunk, _enemyScene, position) is EnemyBase enemy)
		{
			enemy.CountsForClearAll = false;
		}
	}

	private void SpawnCoin(Chunk chunk, Vector2 position)
	{
		_coinScene ??= ResourceLoader.Load<PackedScene>(CoinScenePath);
		Attach(chunk, _coinScene, position);
	}

	private void SpawnCoinSpe(Chunk chunk, Vector2 position)
	{
		_coinSpeScene ??= ResourceLoader.Load<PackedScene>(CoinSpeScenePath);
		Attach(chunk, _coinSpeScene, position);
	}

	private void SpawnCoinW(Chunk chunk, Vector2 position)
	{
		_coinWScene ??= ResourceLoader.Load<PackedScene>(CoinWScenePath);
		Attach(chunk, _coinWScene, position);
	}

	private Node2D Attach(Chunk chunk, PackedScene scene, Vector2 position)
	{
		if (scene == null)
		{
			return null;
		}

		Node node = scene.Instantiate();
		if (node is not Node2D node2D)
		{
			node.QueueFree();
			return null;
		}

		// 先把坐标摆好再入树：Coin._Ready 会把自己的 Position 记成浮动基准点，
		// 反过来写的话基准点会记成 (0,0)，所有金币都会堆到地图起点。
		node2D.Position = position;
		AddChild(node2D);
		chunk.Nodes.Add(node2D);

		return node2D;
	}

	/// <summary>矮杆：站着会撞到，蹲下（判定高度减半）能钻过去。</summary>
	private void SpawnBar(Chunk chunk, Vector2 position)
	{
		Node2D bar = new()
		{
			Name = "LowBar",
			Position = position,
		};

		_barTexture ??= ResourceLoader.Load<Texture2D>(BarTexturePath);
		if (_barTexture != null)
		{
			bar.AddChild(new Sprite2D
			{
				Texture = _barTexture,
				Scale = new Vector2(BarWidth / 8f, BarThickness / 8f),
			});
		}

		HitBox hit = new()
		{
			Name = "HitBox",
			Damage = 1,
			CollisionLayer = GameLayers.EnemyAttack,
			CollisionMask = GameLayers.PlayerHurt,
		};
		hit.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(BarWidth, BarThickness) },
		});

		bar.AddChild(hit);
		AddChild(bar);
		chunk.Nodes.Add(bar);
	}

	private void CleanupOldChunks(float minX)
	{
		while (_chunks.Count > 0 && _chunks[0].EndX * 8f < minX)
		{
			Chunk chunk = _chunks[0];
			_chunks.RemoveAt(0);

			for (int x = chunk.StartX; x < chunk.EndX; x++)
			{
				_map.EraseCell(new Vector2I(x, SurfaceRow));
				_map.EraseCell(new Vector2I(x, SurfaceRow + 1));
			}

			foreach (Node node in chunk.Nodes)
			{
				if (IsInstanceValid(node))
				{
					node.QueueFree();
				}
			}
		}
	}

	// ------------------------------------------------------------------
	// 难度 / 界面 / 结束
	// ------------------------------------------------------------------

	private void UpdateSpeed()
	{
		if (_player == null)
		{
			return;
		}

		_player.MoveSpeed = Mathf.Min(MaxSpeed, StartSpeed + _chunkIndex * AddSpeedPerChunk);
	}

	private void UpdateDistanceLabel()
	{
		if (_distanceLabel != null)
		{
			_distanceLabel.Text = $"距离 {Distance}　金币 {_coins}";
		}
	}

	private void OnCoinChanged(int coins)
	{
		_coins = coins;
		UpdateDistanceLabel();
	}

	private void OnHealthChanged(int health, int maxHealth)
	{
		if (_healthLabel != null)
		{
			_healthLabel.Text = $"生命 {health}/{maxHealth}";
		}
	}

	/// <summary>点到自己：有强化次数才弹三选一（暂停着选完再继续跑）。</summary>
	public void OpenUpgradePanel()
	{
		if (_dead || _player == null || !IsInstanceValid(_player) || _player.UpgradeCharges <= 0)
		{
			return;
		}

		if (_upgradePanel != null && IsInstanceValid(_upgradePanel) && _upgradePanel.IsOpen)
		{
			return;
		}

		EnsureUpgradePanel();
		if (_upgradePanel == null)
		{
			return;
		}

		// 先扣次数（选中后不会再重复扣），关掉不选会在 onCancel 里退回来
		if (!_player.TryConsumeUpgradeCharge())
		{
			return;
		}

		_upgradePanel.Open(
			UpgradePool.Draw(_player, TutorialForceMode.Random),
			"选择一项强化",
			option =>
			{
				if (option != null)
				{
					_player.ApplyUpgrade(option.Effect);
				}
			},
			() => UpgradePool.Draw(_player, TutorialForceMode.Random),
			() =>
			{
				// 关掉不选：把次数退回去，下次点自己还能再开
				_player?.AddUpgradeCharge(1);
			},
			cost => _player != null && _player.TrySpendCoins(cost));
	}

	/// <summary>把这一局捡到的币结算进总账（只结算一次，死了/中途退出都算）。</summary>
	private void BankRunCoins()
	{
		if (_bankedCoins)
		{
			return;
		}

		_bankedCoins = true;

		if (_coins > 0)
		{
			GameState.Instance?.BankCoins(_coins);
			GD.Print($"[EndlessRunner] 带出 {_coins} coin（距离 {Distance}）");
		}
	}

	public override void _ExitTree()
	{
		// 没死就退出（暂停 → 退出 / 直接切场景）也要带出去
		BankRunCoins();
	}

	private void EnsureUpgradePanel()
	{
		if (_upgradePanel != null && IsInstanceValid(_upgradePanel))
		{
			return;
		}

		PackedScene scene = ResourceLoader.Load<PackedScene>(UpgradePanelScenePath);
		if (scene == null)
		{
			GD.PushError($"[EndlessRunner] 强化界面加载失败：{UpgradePanelScenePath}");
			return;
		}

		_upgradePanel = scene.Instantiate<UpgradePanel>();
		AddChild(_upgradePanel);
	}

	private void OnPlayerDied()
	{
		if (_dead)
		{
			return;
		}

		_dead = true;

		// 跑酷的币不看通关：死了也照常带出去
		BankRunCoins();

		if (_gameOverLabel != null)
		{
			_gameOverLabel.Text = _coins > 0
				? $"距离 {Distance}　金币 +{_coins}（已存入）"
				: $"距离 {Distance}";
		}

		if (_gameOverPanel != null)
		{
			_gameOverPanel.Visible = true;
		}

		// 死亡期间暂停键没意义（会盖在结算面板上），先藏掉
		if (GetNodeOrNull<CanvasItem>("TutorialUi/Root/PauseButton") is CanvasItem pauseButton)
		{
			pauseButton.Visible = false;
		}

		GetTree().Paused = true;
	}

	private void Retry()
	{
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}

	private void GoBack()
	{
		GetTree().Paused = false;

		Error error = GetTree().ChangeSceneToFile(BackScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[EndlessRunner] 返回失败：{error}（{BackScenePath}）");
		}
	}

	private void BindButton(string path, System.Action action)
	{
		if (GetNodeOrNull<BaseButton>(path) is BaseButton button)
		{
			button.Pressed += action;
		}
	}

	private void HideTouchButton(string name)
	{
		foreach (Node node in GetChildren())
		{
			if (node.FindChild(name, true, false) is CanvasItem item)
			{
				item.Visible = false;
			}
		}
	}
}
