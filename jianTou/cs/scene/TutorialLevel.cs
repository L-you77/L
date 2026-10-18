using System.Collections.Generic;
using Godot;

/// <summary>
/// 教学关卡管理器（挂在 node/jiaoXue/ 下 node_right / node_left / node_up / node_near_hit 的根节点上）。
///
/// 职责：
///   · 决定这一关的三选一怎么抽（<see cref="Mode"/>：全右 / 全左 / 全上 / 真随机）；
///   · 玩家点自己时弹出强化界面，并把选中的效果打到玩家身上；
///   · 选完之后把对应的触摸按钮显出来（选了"向右"才出 TBtnR，选了"近战"才出 TBtnNH）。
///
/// 强化次数由 coinSpe 提供：**捡一个 coinSpe 就能点自己一次**，
/// 所以这里只管"有没有次数"，不限制每关次数（关卡里放几个就是几次）。
///
/// 界面不是在场景里摆的，而是运行时实例化 upgrade_panel.tscn —— 四个关卡共用一份 UI，改样式只改那一处。
/// 触摸按钮则在 tutorial_ui.tscn（CanvasLayer）里，这里按名字去那里找。
/// </summary>
public partial class TutorialLevel : Node2D
{
	/// <summary>教学关卡的场景分组，Player2 用它反查当前关卡。</summary>
	public const string GroupName = "tutorial_level";

	/// <summary>这一关的抽取规则。</summary>
	[Export] public TutorialForceMode Mode = TutorialForceMode.Random;

	/// <summary>弹窗标题。</summary>
	[Export] public string UpgradeTitle = "选择一项强化";

	/// <summary>强化界面场景路径。</summary>
	[Export] public string UpgradePanelScenePath = "res://node/ui/upgrade_panel.tscn";

	/// <summary>触摸按钮所在的 UI 节点（tutorial_ui.tscn 的实例）在关卡里的节点名。</summary>
	[Export] public string TouchUiPath = "TutorialUi";

	/// <summary>
	/// 进关时这四个触摸按钮的初始可见状态（按钮本身在 tutorial_ui.tscn 里，默认全隐藏）。
	/// 哪一关一开始就能往哪个方向走，就把它对应的开关勾上；其余等选了强化再放出来。
	/// </summary>
	[ExportGroup("初始可见的触摸按钮")]
	[Export] public bool ShowRightButton = false;
	[Export] public bool ShowLeftButton = false;
	[Export] public bool ShowUpButton = false;
	[Export] public bool ShowNearHitButton = false;

	private UpgradePanel _panel;
	private Node _touchUi;

	public override void _Ready()
	{
		AddToGroup(GroupName);

		_touchUi = GetNodeOrNull(TouchUiPath);
		ApplyInitialButtonVisibility();
	}

	/// <summary>
	/// 玩家点击自己时调用。返回 true 表示确实弹出了界面。
	/// 没捡到 coinSpe（charge = 0）时不会弹，并打一条提示。
	/// </summary>
	public bool RequestUpgrade(Player player)
	{
		if (player == null || !player.IsAlive)
		{
			return false;
		}

		if (_panel != null && _panel.IsOpen)
		{
			return false;
		}

		if (player.UpgradeCharges <= 0)
		{
			GD.Print("[TutorialLevel] 没有强化次数 —— 先去捡 coinSpe");
			return false;
		}

		if (!EnsurePanel())
		{
			return false;
		}

		if (!player.TryConsumeUpgradeCharge())
		{
			return false;
		}

		List<UpgradeOption> cards = UpgradePool.Draw(player, Mode);
		_panel.Open(cards, UpgradeTitle, option => OnPicked(player, option));
		return true;
	}

	/// <summary>选中某张卡：生效、放出对应按钮。</summary>
	private void OnPicked(Player player, UpgradeOption option)
	{
		if (option == null)
		{
			return;
		}

		player.ApplyUpgrade(option.Effect);

		string buttonName = ButtonNameFor(option.Effect);
		if (!string.IsNullOrEmpty(buttonName))
		{
			RevealButton(buttonName);
		}

		GD.Print($"[TutorialLevel] 选择了 {option.Title}（{option.Desc}）");
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
			GD.PushError($"[TutorialLevel] 强化界面场景加载失败：{UpgradePanelScenePath}");
			return false;
		}

		_panel = scene.Instantiate<UpgradePanel>();
		if (_panel == null)
		{
			GD.PushError("[TutorialLevel] 强化界面实例化失败（根节点不是 UpgradePanel）");
			return false;
		}

		AddChild(_panel);
		return true;
	}

	/// <summary>按关卡配置摆好四个触摸按钮的初始可见状态（其余一律隐藏）。</summary>
	private void ApplyInitialButtonVisibility()
	{
		SetButtonVisible("TBtnR", ShowRightButton);
		SetButtonVisible("TBtnL", ShowLeftButton);
		SetButtonVisible("TBtnU", ShowUpButton);
		SetButtonVisible("TBtnNH", ShowNearHitButton);
		SetButtonVisible("TBtnFH", false);
	}

	private void RevealButton(string name)
	{
		SetButtonVisible(name, true);
	}

	private void SetButtonVisible(string name, bool visible)
	{
		if (FindButton(name) is not CanvasItem button)
		{
			// 这一关没有这个按钮是正常的（比如 node_right 里就没有 TBtnNH）
			return;
		}

		button.Visible = visible;
	}

	/// <summary>按钮在 tutorial_ui 这个 CanvasLayer 里，不在关卡节点上，所以要先去 UI 找。</summary>
	private CanvasItem FindButton(string name)
	{
		if (_touchUi != null && IsInstanceValid(_touchUi))
		{
			// owned: false —— UI 是场景实例，内部节点不算本场景"拥有"
			if (_touchUi.FindChild(name, true, false) is CanvasItem inUi)
			{
				return inUi;
			}
		}

		return GetNodeOrNull<CanvasItem>(name);
	}

	/// <summary>强化效果 → 应该显示出来的触摸按钮。</summary>
	private static string ButtonNameFor(UpgradeEffect effect)
	{
		return effect switch
		{
			UpgradeEffect.SpeedRight => "TBtnR",
			UpgradeEffect.SpeedLeft => "TBtnL",
			UpgradeEffect.JumpHeight => "TBtnU",
			UpgradeEffect.UnlockNearHit => "TBtnNH",
			UpgradeEffect.UnlockFarHit => "TBtnFH",
			_ => string.Empty,
		};
	}
}
