using System.Collections.Generic;
using Godot;

/// <summary>
/// 教学关卡抽取强化的规则。
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
/// 强化池：按玩家当前状态生成候选卡，再按教学关卡的规则抽 3 张。
/// </summary>
public static class UpgradePool
{
	/// <summary>三选一固定给 3 张。</summary>
	public const int CardCount = 3;

	private const string IconRight = "res://ps/player/move/Right.png";
	private const string IconLeft = "res://ps/player/move/Left.png";
	private const string IconUp = "res://ps/player/move/Up.png";
	private const string IconDown = "res://ps/player/move/Down.png";
	private const string IconNearHit = "res://ps/player/nearHit.png";
	private const string IconFarHit = "res://ps/player/farHit.png";

	private static readonly RandomNumberGenerator Rng = new();
	private static bool _seeded;

	/// <summary>
	/// 生成 3 张强化卡。
	///   · ForceRight/ForceLeft/ForceUp：3 张全是同一个方向，教学关卡用它逼玩家走对方向；
	///   · Random：从池子里抽 3 张不重复的；
	///   · 没拿到武器时池子里是"获得武器"，拿到之后自动变成"伤害 +1"或"攻击间隔缩短"（随机二选一）。
	/// </summary>
	public static List<UpgradeOption> Draw(Player player, TutorialForceMode mode)
	{
		// 只播一次种：每次 Draw 都 Randomize 会让同一毫秒内的两次抽取完全一样
		if (!_seeded)
		{
			Rng.Randomize();
			_seeded = true;
		}

		return mode switch
		{
			TutorialForceMode.ForceRight => Repeat(MakeRight(player)),
			TutorialForceMode.ForceLeft => Repeat(MakeLeft(player)),
			TutorialForceMode.ForceUp => Repeat(MakeUp(player)),
			TutorialForceMode.ForceDown => Repeat(MakeDown(player)),
			_ => DrawRandom(player),
		};
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

	private static List<UpgradeOption> DrawRandom(Player player)
	{
		List<UpgradeOption> pool = new()
		{
			MakeRight(player),
			MakeLeft(player),
			MakeUp(player),
			MakeNearHit(player),
			MakeFarHit(player),
		};

		// Fisher-Yates 洗牌，取前 3 张（池子只有 5 张，天然不会重复）
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

	private static UpgradeOption MakeRight(Player player)
	{
		return new UpgradeOption
		{
			Effect = UpgradeEffect.SpeedRight,
			Title = "向右加速",
			Desc = $"向右速度 +{FormatPercent(player.SpeedUpPerLevel)}",
			IconPath = IconRight,
		};
	}

	private static UpgradeOption MakeLeft(Player player)
	{
		return new UpgradeOption
		{
			Effect = UpgradeEffect.SpeedLeft,
			Title = "向左加速",
			Desc = $"向左速度 +{FormatPercent(player.SpeedUpPerLevel)}",
			IconPath = IconLeft,
		};
	}

	private static UpgradeOption MakeUp(Player player)
	{
		return new UpgradeOption
		{
			Effect = UpgradeEffect.JumpHeight,
			Title = "跳得更高",
			Desc = $"跳跃高度 +{FormatPercent(player.JumpUpPerLevel)}",
			IconPath = IconUp,
		};
	}

	/// <summary>"向下"卡：只在 node_down 这种强制向下的教学关出现，顺带把"向下"按钮放出来。</summary>
	private static UpgradeOption MakeDown(Player player)
	{
		return new UpgradeOption
		{
			Effect = UpgradeEffect.DownSpeed,
			Title = "向下加速",
			Desc = $"向下速度 +{FormatPercent(player.SpeedUpPerLevel)}",
			IconPath = IconDown,
		};
	}

	private static UpgradeOption MakeNearHit(Player player)
	{
		if (!player.HasNearHit)
		{
			return new UpgradeOption
			{
				Effect = UpgradeEffect.UnlockNearHit,
				Title = "获得近战",
				Desc = "解锁近战攻击（J 键）",
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
				? $"近战伤害 +{player.DamageUpPerLevel}"
				: $"近战攻击间隔 -{FormatPercent(player.CooldownDownPerLevel)}",
			IconPath = IconNearHit,
		};
	}

	private static UpgradeOption MakeFarHit(Player player)
	{
		if (!player.HasFarHit)
		{
			return new UpgradeOption
			{
				Effect = UpgradeEffect.UnlockFarHit,
				Title = "获得远程",
				Desc = "解锁远程攻击（U 键）",
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
				? $"远程伤害 +{player.DamageUpPerLevel}"
				: $"远程攻击间隔 -{FormatPercent(player.CooldownDownPerLevel)}",
			IconPath = IconFarHit,
		};
	}

	/// <summary>0.15 → "15%"。</summary>
	private static string FormatPercent(float value)
	{
		return $"{Mathf.RoundToInt(value * 100f)}%";
	}
}
