using Godot;

/// <summary>
/// 首页商城（home_shop）。
///
///   · 显示**已带出关卡**的 coin 总数（<see cref="GameState.Coins"/>）；
///   · 每 <see cref="UpgradeCost"/> 枚可以买一次升级：扣币 → 弹三选一 → 选中的效果记成永久强化；
///   · 永久强化在每次进关卡时由 <see cref="GameState.ApplyPermanent"/> 打到玩家身上。
///
/// 币不够时"升级"按钮会变暗且点不动。
/// </summary>
public partial class HomeShopMenu : Control
{
	/// <summary>一次升级要花多少 coin。</summary>
	[Export] public int UpgradeCost = 8;

	/// <summary>EYE（静止久了露出眼睛）的价格。</summary>
	[Export] public int EyeCost = 10;

	/// <summary>三选一界面场景。</summary>
	[Export] public string UpgradePanelScenePath = "res://node/ui/upgrade_panel.tscn";

	/// <summary>弹窗标题。</summary>
	[Export] public string UpgradeTitle = "选择一项永久强化";

	/// <summary>"返回"回哪一层。</summary>
	[Export] public string BackScenePath = ScenePaths.Home2;

	/// <summary>右上角「背包」按钮进哪个场景。</summary>
	[Export] public string BagScenePath = ScenePaths.Bag;

	/// <summary>EYE 买完之后显示的文字。</summary>
	[Export] public string SoldOutText = "售空了";

	private Label _coinLabel;
	private BaseButton _buyButton;
	private Label _buyLabel;
	private BaseButton _eyeButton;
	private Label _eyeLabel;
	private Sprite2D _eyeCoinIcon;
	private UpgradePanel _panel;

	// 场景里手写的标签文字：运行时以它为准，脚本不会覆盖
	private string _buyTextDefault = string.Empty;
	private string _eyeTextDefault = string.Empty;

	public override void _Ready()
	{
		GetTree().Paused = false;

		_coinLabel = GetNodeOrNull<Label>("CoinLabel");
		_buyButton = GetNodeOrNull<BaseButton>("BuyButton");
		_buyLabel = GetNodeOrNull<Label>("BuyLabel");
		_eyeButton = GetNodeOrNull<BaseButton>("EyeButton");
		_eyeLabel = GetNodeOrNull<Label>("EyeLabel");
		_eyeCoinIcon = GetNodeOrNull<Sprite2D>("EyeLabel/Coin");

		// 先记下编辑器里写的文案，后面只在这些文案之间切换，绝不自己拼字符串
		_buyTextDefault = _buyLabel?.Text ?? string.Empty;
		_eyeTextDefault = _eyeLabel?.Text ?? string.Empty;

		if (_buyButton != null)
		{
			_buyButton.Pressed += OnBuyPressed;
		}

		if (_eyeButton != null)
		{
			_eyeButton.Pressed += OnEyePressed;
		}

		if (GetNodeOrNull<BaseButton>("ExitButton") is BaseButton exit)
		{
			exit.Pressed += GoBack;
		}

		if (GetNodeOrNull<BaseButton>("BagButton") is BaseButton bag)
		{
			bag.Pressed += OpenBag;
		}

		if (GameState.Instance != null)
		{
			GameState.Instance.CoinsChanged += OnCoinsChanged;
		}

		Refresh();
	}

	public override void _ExitTree()
	{
		// GameState 是 autoload，比本场景活得久，退场时一定要断开
		if (GameState.Instance != null)
		{
			GameState.Instance.CoinsChanged -= OnCoinsChanged;
		}
	}

	private void OnCoinsChanged(int coins)
	{
		Refresh();
	}

	/// <summary>刷新币数和两个商品的按钮状态。</summary>
	private void Refresh()
	{
		int coins = GameState.Instance?.Coins ?? 0;
		bool eyeOwned = GameState.Instance?.EyeUnlocked ?? false;

		if (_coinLabel != null)
		{
			_coinLabel.Text = coins.ToString();
		}

		// 升级（可以重复买）。文案以场景里写的为准，脚本不重写
		bool canUpgrade = coins >= UpgradeCost;
		if (_buyLabel != null)
		{
			_buyLabel.Text = _buyTextDefault;
		}
		if (_buyButton != null)
		{
			_buyButton.Disabled = !canUpgrade;
			_buyButton.Modulate = canUpgrade ? Colors.White : new Color(1f, 1f, 1f, 0.35f);
		}

		// EYE（只能买一次，买完显示"售空了"）
		bool canBuyEye = !eyeOwned && coins >= EyeCost;
		if (_eyeLabel != null)
		{
			_eyeLabel.Text = eyeOwned ? SoldOutText : _eyeTextDefault;
		}
		if (_eyeButton != null)
		{
			_eyeButton.Disabled = !canBuyEye;
			_eyeButton.Modulate = canBuyEye ? Colors.White : new Color(1f, 1f, 1f, 0.35f);
		}

		// 售空之后就别在"售空了"旁边挂个价格图标了
		if (_eyeCoinIcon != null)
		{
			_eyeCoinIcon.Visible = !eyeOwned;
		}
	}

	/// <summary>买 EYE：一次性解锁"静止 → warn → 白块 + 眼睛"那套表现。</summary>
	private void OnEyePressed()
	{
		if (GameState.Instance == null || GameState.Instance.EyeUnlocked)
		{
			return;
		}

		if (!GameState.Instance.TrySpendCoins(EyeCost))
		{
			return;
		}

		GameState.Instance.UnlockEye();
		Refresh();
	}

	private void OnBuyPressed()
	{
		if (GameState.Instance == null)
		{
			GD.PushWarning("[HomeShop] 没有 GameState（autoload 没生效？）");
			return;
		}

		if (GameState.Instance.Coins < UpgradeCost)
		{
			return;
		}

		if (!EnsurePanel())
		{
			return;
		}

		// 已经有永久武器的话，卡池里对应的是"强化武器"而不是"获得武器"
		bool hasNear = GameState.Instance.HasPermanent(UpgradeEffect.UnlockNearHit);
		bool hasFar = GameState.Instance.HasPermanent(UpgradeEffect.UnlockFarHit);

		// 币在"真正选中一张"的时候才扣 —— 三选一现在可以关掉，别白花钱
		_panel.Open(
			UpgradePool.DrawShop(hasNear, hasFar),
			UpgradeTitle,
			OnPicked,
			() => UpgradePool.DrawShop(hasNear, hasFar));
	}

	private void OnPicked(UpgradeOption option)
	{
		if (option == null || GameState.Instance == null)
		{
			return;
		}

		if (!GameState.Instance.TrySpendCoins(UpgradeCost))
		{
			return;
		}

		GameState.Instance.AddPermanent(option.Effect);
		Refresh();
	}

	private bool EnsurePanel()
	{
		if (_panel != null && IsInstanceValid(_panel))
		{
			return true;
		}

		PackedScene scene = ResourceLoader.Load<PackedScene>(UpgradePanelScenePath);
		if (scene == null)
		{
			GD.PushError($"[HomeShop] 强化界面加载失败：{UpgradePanelScenePath}");
			return false;
		}

		_panel = scene.Instantiate<UpgradePanel>();
		if (_panel == null)
		{
			GD.PushError("[HomeShop] 强化界面根节点不是 UpgradePanel");
			return false;
		}

		AddChild(_panel);
		return true;
	}

	/// <summary>打开背包（列出买到的永久道具，可以按件卖）。</summary>
	private void OpenBag()
	{
		GetTree().Paused = false;

		Error error = GetTree().ChangeSceneToFile(BagScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeShop] 打开背包失败：{error}（{BagScenePath}）");
		}
	}

	private void GoBack()
	{
		GetTree().Paused = false;

		Error error = GetTree().ChangeSceneToFile(BackScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeShop] 返回失败：{error}（{BackScenePath}）");
		}
	}
}
