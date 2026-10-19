using System.Collections.Generic;
using Godot;

/// <summary>
/// 教学关卡 / 商城抽卡时使用的规则。
/// </summary>
public enum TutorialForceMode
{
	/// <summary>真随机：从整个池子里抽 3 张不重复的。</summary>
	Random = 0,
	/// <summary>3 张全是"向右"（node_right 用，避免玩家选到别的方向）。</summary>
	ForceRight = 1,
	/// <summary>3 张全是"向左"（node_left）。</summary>
	ForceLeft = 2,
	/// <summary>3 张全是"向上"（node_up）。</summary>
	ForceUp = 3,
	/// <summary>3 张全是"向下"（node_down）。</summary>
	ForceDown = 4,
}

/// <summary>
/// 强化池：按玩家当前状态生成候选卡，再按规则抽 3 张。
///
/// 两个入口：
///   · <see cref="Draw"/>      教学关卡用（有真实 Player，数值与库存都从玩家身上读）；
///   · <see cref="DrawShop"/>  首页商城用（没有 Player，用默认数值 + 永久强化记录判断）。
/// </summary>
public static class UpgradePool
{
	/// <summary>三选一固定给 3 张。</summary>
	public const int CardCount = 3;

	/// <summary>商城里没有 Player 可读时用的默认数值（和 Player 上的导出默认值保持一致）。</summary>
	private const float DefaultSpeedUp = 0.15f;
	private const float DefaultJumpUp = 0.12f;
	private const float DefaultCooldownDown = 0.15f;
	private const int DefaultDamageUp = 1;

	private const string IconRight = "res://ps/player/move/Right.png";
	private const string IconLeft = "res://ps/player/move/Left.png";
	private const string IconUp = "res://ps/player/move/Up.png";
	private const string IconDown = "res://ps/player/move/Down.png";
	private const string IconNearHit = "res://ps/player/nearHit.png";
	private const string IconFarHit = "res://ps/player/farHit.png";
	private const string IconHealth = "res://ps/player/JiaZai/JiaZai100.png";

	/// <summary>"跳得更高"这张卡有 20% 的概率变成"空中跳跃"。</summary>
	private const float AirJumpChance = 0.2f;

	private static readonly RandomNumberGenerator Rng = new();
	private static bool _seeded;

	/// <summary>教学关卡抽卡。</summary>
	public static List<UpgradeOption> Draw(Player player, TutorialForceMode mode)
	{
		Seed();

		return mode switch
		{
			TutorialForceMode.ForceRight => Repeat(MakeRight(player.SpeedUpPerLevel)),
			TutorialForceMode.ForceLeft => Repeat(MakeLeft(player.SpeedUpPerLevel)),
			TutorialForceMode.ForceUp => Repeat(MakeUp(player.JumpUpPerLevel)),
			TutorialForceMode.ForceDown => Repeat(MakeDown(player.SpeedUpPerLevel)),
			_ => DrawRandom(BuildPool(
				player.HasNearHit,
				player.HasFarHit,
				player.SpeedUpPerLevel,
				player.JumpUpPerLevel,
				player.CooldownDownPerLevel,
				player.DamageUpPerLevel)),
		};
	}

	/// <summary>商城抽卡：没有 Player 实例，用默认数值 + 永久强化记录决定池子。</summary>
	public static List<UpgradeOption> DrawShop(bool hasNearHit, bool hasFarHit)
	{
		Seed();

		return DrawRandom(BuildPool(
			hasNearHit,
			hasFarHit,
			DefaultSpeedUp,
			DefaultJumpUp,
			DefaultCooldownDown,
			DefaultDamageUp));
	}

	/// <summary>只播一次种：每次抽取都 Randomize 会让同一毫秒内的两次抽取完全一样。</summary>
	private static void Seed()
	{
		if (_seeded)
		{
			return;
		}

		Rng.Randomize();
		_seeded = true;
	}

	private static List<UpgradeOption> Repeat(UpgradeOption option)
	{
		List<UpgradeOption> cards = new(CardCount);
		for (int i = 0; i < CardCount; i++)
		{
			cards.Add(option);
		}
		return cards;
	}

	/// <summary>随机池。卡片数比 3 多，洗牌后取前 3 张。</summary>
	private static List<UpgradeOption> BuildPool(
		bool hasNearHit, bool hasFarHit, float speedUp, float jumpUp, float cooldownDown, int damageUp)
	{
		return new List<UpgradeOption>
		{
			MakeRight(speedUp),
			MakeLeft(speedUp),
			MakeUp(jumpUp, allowAirJump: true),
			MakeDown(speedUp),
			MakeHealth(),
			MakeNearHit(hasNearHit, damageUp, cooldownDown),
			MakeFarHit(hasFarHit, damageUp, cooldownDown),
		};
	}

	private static List<UpgradeOption> DrawRandom(List<UpgradeOption> pool)
	{
		// Fisher-Yates 洗牌，取前 3 张（池子 5 张，天然不会重复）
		for (int i = pool.Count - 1; i > 0; i--)
		{
			int j = Rng.RandiRange(0, i);
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}

		List<UpgradeOption> cards = new(CardCount);
		for (int i = 0; i < CardCount && i < pool.Count; i++)
		{
			cards.Add(pool[i]);
		}
		return cards;
	}

	private static UpgradeOption MakeRight(float speedUp)
	{
		return new UpgradeOption
		{
			Effect = UpgradeEffect.SpeedRight,
			Title = "向右加速",
			Desc = $"向右速度 +{FormatPercent(speedUp)}",
			IconPath = IconRight,
		};
	}

	private static UpgradeOption MakeLeft(float speedUp)
	{
		return new UpgradeOption
		{
			Effect = UpgradeEffect.SpeedLeft,
			Title = "向左加速",
			Desc = $"向左速度 +{FormatPercent(speedUp)}",
			IconPath = IconLeft,
		};
	}

	private static UpgradeOption MakeUp(float jumpUp, bool allowAirJump = false)
	{
		// 20% 概率变成"空中跳跃"（同样用向上的箭头图标）
		if (allowAirJump && Rng.Randf() < AirJumpChance)
		{
			return new UpgradeOption
			{
				Effect = UpgradeEffect.AirJump,
				Title = "空中跳跃",
				Desc = "空中可以多跳一次",
				IconPath = IconUp,
			};
		}

		return new UpgradeOption
		{
			Effect = UpgradeEffect.JumpHeight,
			Title = "跳得更高",
			Desc = $"跳跃高度 +{FormatPercent(jumpUp)}",
			IconPath = IconUp,
		};
	}

	/// <summary>生命值卡：图标用角色的 JiaZai（加载转圈）图。</summary>
	private static UpgradeOption MakeHealth()
	{
		return new UpgradeOption
		{
			Effect = UpgradeEffect.MaxHealth,
			Title = "生命上限 +1",
			Desc = "最大生命值 +1，并回复 1 点",
			IconPath = IconHealth,
		};
	}

	/// <summary>"向下"卡：只在 node_down 这种强制向下的教学关出现，顺带把"向下"按钮放出来。</summary>
	private static UpgradeOption MakeDown(float speedUp)
	{
		return new UpgradeOption
		{
			Effect = UpgradeEffect.DownSpeed,
			Title = "向下加速",
			Desc = $"向下速度 +{FormatPercent(speedUp)}",
			IconPath = IconDown,
		};
	}

	private static UpgradeOption MakeNearHit(bool unlocked, int damageUp, float cooldownDown)
	{
		if (!unlocked)
		{
			return new UpgradeOption
			{
				Effect = UpgradeEffect.UnlockNearHit,
				Title = "获得近战",
				Desc = "解锁近战攻击",
				IconPath = IconNearHit,
				IsUnlock = true,
			};
		}

		// 已经有武器：随机在"加伤害"和"加攻速"之间二选一，标签写清楚这次加的是什么
		bool damage = Rng.Randf() < 0.5f;
		return new UpgradeOption
		{
			Effect = damage ? UpgradeEffect.NearHitDamage : UpgradeEffect.NearHitSpeed,
			Title = "近战强化",
			Desc = damage
				? $"近战伤害 +{damageUp}"
				: $"近战攻击间隔 -{FormatPercent(cooldownDown)}",
			IconPath = IconNearHit,
		};
	}

	private static UpgradeOption MakeFarHit(bool unlocked, int damageUp, float cooldownDown)
	{
		if (!unlocked)
		{
			return new UpgradeOption
			{
				Effect = UpgradeEffect.UnlockFarHit,
				Title = "获得远程",
				Desc = "解锁远程攻击",
				IconPath = IconFarHit,
				IsUnlock = true,
			};
		}

		bool damage = Rng.Randf() < 0.5f;
		return new UpgradeOption
		{
			Effect = damage ? UpgradeEffect.FarHitDamage : UpgradeEffect.FarHitSpeed,
			Title = "远程强化",
			Desc = damage
				? $"远程伤害 +{damageUp}"
				: $"远程攻击间隔 -{FormatPercent(cooldownDown)}",
			IconPath = IconFarHit,
		};
	}

	/// <summary>背包里显示的道具名。</summary>
	public static string DisplayName(UpgradeEffect effect)
	{
		return effect switch
		{
			UpgradeEffect.SpeedRight => "向右加速",
			UpgradeEffect.SpeedLeft => "向左加速",
			UpgradeEffect.JumpHeight => "跳得更高",
			UpgradeEffect.AirJump => "空中跳跃",
			UpgradeEffect.DownSpeed => "向下加速",
			UpgradeEffect.MaxHealth => "生命上限 +1",
			UpgradeEffect.UnlockNearHit => "近战武器",
			UpgradeEffect.NearHitDamage => "近战伤害强化",
			UpgradeEffect.NearHitSpeed => "近战攻速强化",
			UpgradeEffect.UnlockFarHit => "远程武器",
			UpgradeEffect.FarHitDamage => "远程伤害强化",
			UpgradeEffect.FarHitSpeed => "远程攻速强化",
			_ => effect.ToString(),
		};
	}

	/// <summary>道具图标路径（背包用）。</summary>
	public static string IconFor(UpgradeEffect effect)
	{
		return effect switch
		{
			UpgradeEffect.SpeedRight => IconRight,
			UpgradeEffect.SpeedLeft => IconLeft,
			UpgradeEffect.JumpHeight => IconUp,
			UpgradeEffect.AirJump => IconUp,
			UpgradeEffect.DownSpeed => IconDown,
			UpgradeEffect.MaxHealth => IconHealth,
			UpgradeEffect.UnlockNearHit => IconNearHit,
			UpgradeEffect.NearHitDamage => IconNearHit,
			UpgradeEffect.NearHitSpeed => IconNearHit,
			UpgradeEffect.UnlockFarHit => IconFarHit,
			UpgradeEffect.FarHitDamage => IconFarHit,
			UpgradeEffect.FarHitSpeed => IconFarHit,
			_ => IconRight,
		};
	}

	/// <summary>0.15 → "15%"。</summary>
	private static string FormatPercent(float value)
	{
		return $"{Mathf.RoundToInt(value * 100f)}%";
	}
}
