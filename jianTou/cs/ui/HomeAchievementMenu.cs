using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 成就界面（从 home_2 右上角的星星按钮进来）。
///
/// 成就的条件全部基于 <see cref="GameState"/> 里已经存下来的数据：
///   通关次数（TongGuan 结算时 +1）、累计带出金币、背包件数、EYE 是否已买。
/// 所以不需要单独再存一份"成就表"——条件算出来就是解锁状态。
///
/// 行是运行时生成的（条件以后加减只要改 <see cref="Definitions"/>）。
/// </summary>
public partial class HomeAchievementMenu : Control
{
	/// <summary>"返回"回哪一层（默认回 home_2）。</summary>
	[Export] public string BackScenePath = ScenePaths.Home2;

	private const string IconFlag = "res://ps/ui/jiXu.png";
	private const string IconSword = "res://ps/player/nearHit.png";
	private const string IconCoin = "res://ps/coin/coin.png";
	private const string IconBag = "res://ps/player/JiaZai/JiaZai100.png";
	private const string IconEye = "res://ps/player/eye.png";
	private const string IconRun = "res://ps/player/move/Right.png";

	/// <summary>一条成就的定义。</summary>
	private sealed class AchievementDef
	{
		public string Name = string.Empty;
		public string Desc = string.Empty;
		public string Icon = IconFlag;
		public int Target = 1;
		public Func<GameState, int> Progress = _ => 0;
	}

	private static readonly AchievementDef[] Definitions =
	{
		new()
		{
			Name = "初次通关",
			Desc = "通关任意一个关卡",
			Icon = IconFlag,
			Target = 1,
			Progress = state => state.ClearCount,
		},
		new()
		{
			Name = "老练的箭头",
			Desc = "累计通关 5 次",
			Icon = IconFlag,
			Target = 5,
			Progress = state => state.ClearCount,
		},
		new()
		{
			Name = "清图",
			Desc = "清理一张地图上的所有敌人",
			Icon = IconSword,
			Target = 1,
			Progress = state => state.AchievementClearAll ? 1 : 0,
		},
		new()
		{
			Name = "快速通关",
			Desc = "8 秒内通关任意一关",
			Icon = IconFlag,
			Target = 1,
			Progress = state => state.AchievementFastClear ? 1 : 0,
		},
		new()
		{
			Name = "酷跑玩家",
			Desc = "无限跑酷跑到 1000 米",
			Icon = IconRun,
			Target = 1,
			Progress = state => state.AchievementRunner1000 ? 1 : 0,
		},
		new()
		{
			Name = "攒钱罐",
			Desc = "累计带出 20 枚金币",
			Icon = IconCoin,
			Target = 20,
			Progress = state => state.TotalCoinsEarned,
		},
		new()
		{
			Name = "小富翁",
			Desc = "累计带出 100 枚金币",
			Icon = IconCoin,
			Target = 100,
			Progress = state => state.TotalCoinsEarned,
		},
		new()
		{
			Name = "强化收藏家",
			Desc = "背包里同时拥有 5 件强化",
			Icon = IconBag,
			Target = 5,
			Progress = state => state.ItemCount,
		},
		new()
		{
			Name = "好奇心",
			Desc = "在商城买下 EYE",
			Icon = IconEye,
			Target = 1,
			Progress = state => state.EyeUnlocked ? 1 : 0,
		},
	};

	private Label _summary;
	private VBoxContainer _list;

	public override void _Ready()
	{
		GetTree().Paused = false;

		_summary = GetNodeOrNull<Label>("Summary");
		_list = GetNodeOrNull<VBoxContainer>("Scroll/List");

		if (GetNodeOrNull<BaseButton>("ExitButton") is BaseButton exit)
		{
			exit.Pressed += GoBack;
		}

		Build();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GoBack();
			GetViewport().SetInputAsHandled();
		}
	}

	private void GoBack()
	{
		Error error = GetTree().ChangeSceneToFile(BackScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeAchievementMenu] 返回失败：{error}（{BackScenePath}）");
		}
	}

	/// <summary>重建整个列表（进界面时一次就够）。</summary>
	private void Build()
	{
		if (_list == null)
		{
			return;
		}

		for (int i = _list.GetChildCount() - 1; i >= 0; i--)
		{
			Node child = _list.GetChild(i);
			_list.RemoveChild(child);
			child.QueueFree();
		}

		GameState state = GameState.Instance;
		int unlocked = 0;

		foreach (AchievementDef def in Definitions)
		{
			int progress = state != null ? Mathf.Min(def.Progress(state), def.Target) : 0;
			bool done = progress >= def.Target;
			if (done)
			{
				unlocked++;
			}

			_list.AddChild(CreateRow(def, progress, done));
		}

		if (_summary != null)
		{
			_summary.Text = $"已解锁 {unlocked} / {Definitions.Length}";
		}
	}

	private static Control CreateRow(AchievementDef def, int progress, bool done)
	{
		HBoxContainer row = new()
		{
			CustomMinimumSize = new Vector2(0f, 56f),
		};
		row.AddThemeConstantOverride("separation", 12);

		TextureRect icon = new()
		{
			CustomMinimumSize = new Vector2(44f, 44f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = ResourceLoader.Load<Texture2D>(def.Icon),
			Modulate = done ? Colors.White : new Color(1f, 1f, 1f, 0.25f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(icon);

		VBoxContainer texts = new()
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		texts.AddThemeConstantOverride("separation", 2);

		Label name = new() { Text = def.Name };
		name.AddThemeFontSizeOverride("font_size", 22);
		name.AddThemeColorOverride("font_color", done
			? new Color(0.0235294f, 0.0196078f, 0.0196078f)
			: new Color(0.45f, 0.45f, 0.48f));
		texts.AddChild(name);

		Label desc = new() { Text = def.Desc };
		desc.AddThemeFontSizeOverride("font_size", 15);
		desc.AddThemeColorOverride("font_color", new Color(0.42f, 0.42f, 0.46f));
		texts.AddChild(desc);

		row.AddChild(texts);

		Label status = new()
		{
			Text = done ? "已解锁" : $"{progress}/{def.Target}",
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(78f, 0f),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		status.AddThemeFontSizeOverride("font_size", 18);
		status.AddThemeColorOverride("font_color", done
			? new Color(0.78f, 0.55f, 0.05f)
			: new Color(0.55f, 0.55f, 0.58f));
		row.AddChild(status);

		return row;
	}
}
