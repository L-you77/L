using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 跨场景的游戏进度（autoload，挂在 /root/GameState）。
///
///   · <see cref="Coins"/>：**已经带出关卡**的 coin 总数（商城花的就是它）；
///   · 关卡里捡到/打死敌人拿到的币只记在 Player.Coins 上，**通关时**才由 TongGuan 调
///     <see cref="BankCoins"/> 结算进来；没通关就重开的话自然就没了；
///   · <see cref="PermanentUpgrades"/>：商城里花钱抽到的永久强化，进关卡时由
///     <see cref="ApplyPermanent"/> 逐条打给 Player。
///
/// 会存到 user://jiantou_save.cfg，关掉游戏再开也还在。想清档就把这个文件删掉。
/// </summary>
public partial class GameState : Node
{
	/// <summary>全局唯一实例（autoload）。</summary>
	public static GameState Instance { get; private set; }

	/// <summary>币数变化时发出，商城 UI 靠它刷新。</summary>
	[Signal] public delegate void CoinsChangedEventHandler(int coins);

	/// <summary>永久强化变化时发出。</summary>
	[Signal] public delegate void PermanentChangedEventHandler();

	private const string SavePath = "user://jiantou_save.cfg";
	private const string SectionPlayer = "player";
	private const string SectionUpgrades = "upgrades";
	private const string KeyCoins = "coins";
	private const string KeyEyeUnlocked = "eye_unlocked";

	private readonly Dictionary<UpgradeEffect, int> _permanent = new();

	/// <summary>已带出的 coin 总数。</summary>
	public int Coins { get; private set; }

	/// <summary>是否已在商城买到 EYE（静止久了会出现眼睛的那个功能）。</summary>
	public bool EyeUnlocked { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		Load();
	}

	// ------------------------------------------------------------------
	// 币
	// ------------------------------------------------------------------

	/// <summary>通关结算：把关卡里收集到的币加进总数。</summary>
	public void BankCoins(int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		Coins += amount;
		GD.Print($"[GameState] 带出 {amount} 枚 coin，总计 {Coins}");
		Save();
		EmitSignal(SignalName.CoinsChanged, Coins);
	}

	/// <summary>直接加币（卖道具返还等）。</summary>
	public void AddCoins(int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		Coins += amount;
		Save();
		EmitSignal(SignalName.CoinsChanged, Coins);
	}

	/// <summary>花钱。余额不够返回 false。</summary>
	public bool TrySpendCoins(int cost)
	{
		if (cost <= 0 || Coins < cost)
		{
			return false;
		}

		Coins -= cost;
		Save();
		EmitSignal(SignalName.CoinsChanged, Coins);
		return true;
	}

	// ------------------------------------------------------------------
	// 永久强化
	// ------------------------------------------------------------------

	/// <summary>记录一次永久强化（同一个效果可以叠多次）。</summary>
	public void AddPermanent(UpgradeEffect effect)
	{
		_permanent.TryGetValue(effect, out int count);
		_permanent[effect] = count + 1;

		GD.Print($"[GameState] 永久强化 +1：{effect}（累计 {count + 1}）");
		Save();
		EmitSignal(SignalName.PermanentChanged);
	}

	/// <summary>某个永久强化叠了几层。</summary>
	public int GetPermanentCount(UpgradeEffect effect)
	{
		return _permanent.TryGetValue(effect, out int count) ? count : 0;
	}

	/// <summary>是否已经有这个永久强化（商城抽卡时用来判断是"解锁武器"还是"强化武器"）。</summary>
	public bool HasPermanent(UpgradeEffect effect)
	{
		return GetPermanentCount(effect) > 0;
	}

	/// <summary>把永久强化全部打到玩家身上（Player._Ready 时调用）。</summary>
	public void ApplyPermanent(Player player)
	{
		if (player == null || _permanent.Count == 0)
		{
			return;
		}

		foreach (KeyValuePair<UpgradeEffect, int> entry in _permanent)
		{
			for (int i = 0; i < entry.Value; i++)
			{
				player.ApplyUpgrade(entry.Key);
			}
		}

		GD.Print($"[GameState] 已应用 {_permanent.Count} 种永久强化");
	}

	/// <summary>永久强化种类数（商城/结算界面可以用）。</summary>
	public int PermanentKindCount => _permanent.Count;

	// ------------------------------------------------------------------
	// 背包 / 出售
	// ------------------------------------------------------------------

	/// <summary>卖一件道具返还多少 coin。</summary>
	public const int SellPrice = 5;

	/// <summary>背包内容：按效果聚合的（效果, 数量）。顺序按枚举固定，界面不会乱跳。</summary>
	public List<KeyValuePair<UpgradeEffect, int>> GetPermanentItems()
	{
		List<KeyValuePair<UpgradeEffect, int>> items = new();

		foreach (UpgradeEffect effect in Enum.GetValues<UpgradeEffect>())
		{
			int count = GetPermanentCount(effect);
			if (count > 0)
			{
				items.Add(new KeyValuePair<UpgradeEffect, int>(effect, count));
			}
		}

		return items;
	}

	/// <summary>背包里一共几件（含 EYE）。</summary>
	public int ItemCount
	{
		get
		{
			int total = EyeUnlocked ? 1 : 0;
			foreach (KeyValuePair<UpgradeEffect, int> entry in _permanent)
			{
				total += entry.Value;
			}
			return total;
		}
	}

	/// <summary>卖掉一件永久强化（只减 1 层），成功则返还 <see cref="SellPrice"/> 枚 coin。</summary>
	public bool SellPermanent(UpgradeEffect effect)
	{
		int count = GetPermanentCount(effect);
		if (count <= 0)
		{
			return false;
		}

		if (count == 1)
		{
			_permanent.Remove(effect);
		}
		else
		{
			_permanent[effect] = count - 1;
		}

		GD.Print($"[GameState] 卖出 {effect}，+{SellPrice} coin");
		AddCoins(SellPrice);
		Save();
		EmitSignal(SignalName.PermanentChanged);
		return true;
	}

	/// <summary>卖掉 EYE（会重新锁上"静止久了露眼睛"的功能）。</summary>
	public bool SellEye()
	{
		if (!EyeUnlocked)
		{
			return false;
		}

		EyeUnlocked = false;
		GD.Print($"[GameState] 卖出 EYE，+{SellPrice} coin");
		AddCoins(SellPrice);
		Save();
		EmitSignal(SignalName.PermanentChanged);
		return true;
	}

	// ------------------------------------------------------------------
	// EYE（商城解锁的"静止久了露出眼睛"功能）
	// ------------------------------------------------------------------

	/// <summary>买下 EYE。已经买过就直接返回。</summary>
	public void UnlockEye()
	{
		if (EyeUnlocked)
		{
			return;
		}

		EyeUnlocked = true;
		GD.Print("[GameState] 已解锁 EYE");
		Save();
		EmitSignal(SignalName.PermanentChanged);
	}

	// ------------------------------------------------------------------
	// 存档
	// ------------------------------------------------------------------

	private void Save()
	{
		ConfigFile config = new();
		config.SetValue(SectionPlayer, KeyCoins, Coins);
		config.SetValue(SectionPlayer, KeyEyeUnlocked, EyeUnlocked);

		foreach (UpgradeEffect effect in Enum.GetValues<UpgradeEffect>())
		{
			config.SetValue(SectionUpgrades, effect.ToString(), GetPermanentCount(effect));
		}

		Error error = config.Save(SavePath);
		if (error != Error.Ok)
		{
			GD.PushWarning($"[GameState] 存档失败：{error}（{SavePath}）");
		}
	}

	private void Load()
	{
		ConfigFile config = new();
		if (config.Load(SavePath) != Error.Ok)
		{
			return; // 第一次玩没有存档，正常
		}

		Coins = config.GetValue(SectionPlayer, KeyCoins, 0).AsInt32();
		EyeUnlocked = config.GetValue(SectionPlayer, KeyEyeUnlocked, false).AsBool();

		foreach (UpgradeEffect effect in Enum.GetValues<UpgradeEffect>())
		{
			int count = config.GetValue(SectionUpgrades, effect.ToString(), 0).AsInt32();
			if (count > 0)
			{
				_permanent[effect] = count;
			}
		}

		GD.Print($"[GameState] 读档：coin {Coins}，永久强化 {_permanent.Count} 种");
	}
}
