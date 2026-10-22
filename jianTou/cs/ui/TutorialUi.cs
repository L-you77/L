using Godot;

/// <summary>
/// 教学关卡的 UI 层（node/ui/tutorial_ui.tscn，CanvasLayer）：
///   · 左下 TBtnL / TBtnR  —— 按住左右移动（绑定 Left / Right 输入动作）；
///   · 右下 TBtnNH / TBtnU —— 近战攻击（NearHit）/ 跳跃（Up）；
///   · 右上暂停按钮 → 暂停面板（继续 / 退出）。
///
/// 这四个按钮只是"按住即按下对应输入动作"，所以移动/跳跃/攻击逻辑完全复用 Player 里那套，
/// 不需要为触屏再写一遍。哪些按钮默认可见由关卡里的 TutorialLevel 用开关控制。
///
/// 关键点：ProcessMode = Always，否则暂停后自己也停了，继续按钮点不动。
/// </summary>
public partial class TutorialUi : CanvasLayer
{
	/// <summary>暂停面板里"退出"回到哪个场景（默认回教学选关）。</summary>
	[Export] public string ExitScenePath = ScenePaths.TutorialSelect;

	/// <summary>"占用暂停状态"的界面共用的分组（强化 / 通关 / 跑酷结算面板）。</summary>
	public const string PauseBlockerGroup = "pause_blocker";

	/// <summary>Esc 也能开关暂停。</summary>
	[Export] public bool PauseWithEscape = true;

	/// <summary>
	/// 自带右上角那个暂停按钮要不要启用。
	/// 主线关卡（node_1）已经有 ui.tscn 的暂停面板，把它关掉免得两个暂停键打架。
	/// </summary>
	[Export] public bool EnablePauseButton = true;

	/// <summary>
	/// 进关时把所有触摸按钮直接显示出来（主线关卡用：左右上下 + 近战 + 远程全开）。
	/// 教学关不用勾，交给 TutorialLevel 的各个 Show*Button 控制。
	/// </summary>
	[Export] public bool ShowAllButtonsOnStart = false;

	/// <summary>
	/// 这一层 UI 的绘制层级（默认 5，在关卡画面之上、强化界面 10 之下）。
	/// 跑酷模式自带一层 HUD（layer 8），想要暂停面板压住它就把这里调成 9。
	/// </summary>
	[Export] public int UiLayer = 5;

	/// <summary>进关时在屏幕上方显示的关卡名（留空 = 不显示）。比如"第二关 · 升降井"。</summary>
	[Export] public string TitleText = "";

	/// <summary>关卡名停留多久开始淡出（秒）。</summary>
	[Export] public float TitleHoldTime = 1.8f;

	/// <summary>关卡名淡出时长（秒）。</summary>
	[Export] public float TitleFadeTime = 0.7f;

	private Control _pausePanel;
	private Control _touchLayer;
	private TextureButton _pauseButton;
	private Label _hint;
	private Label _upgradeLabel;
	private Label _titleLabel;
	private Player _player;
	private bool _paused;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = UiLayer;

		_pausePanel = GetNodeOrNull<Control>("Root/PausePanel");
		_touchLayer = GetNodeOrNull<Control>("Root/TouchLayer");
		_pauseButton = GetNodeOrNull<TextureButton>("Root/PauseButton");
		_hint = GetNodeOrNull<Label>("Root/Hint");
		_upgradeLabel = GetNodeOrNull<Label>("Root/UpgradeLabel");
		_titleLabel = GetNodeOrNull<Label>("Root/TitleLabel");
		BindPlayer();

		Bind("Root/PauseButton", () => SetPaused(true));
		Bind("Root/PausePanel/ResumeButton", () => SetPaused(false));
		Bind("Root/PausePanel/ExitButton", ExitLevel);

		if (!EnablePauseButton)
		{
			// 这个关卡用自己的暂停面板（ui.tscn 里的 PauseMenu），本 UI 的暂停键直接不显示
			if (_pauseButton != null)
			{
				_pauseButton.Visible = false;
			}
			if (_pausePanel != null)
			{
				_pausePanel.Visible = false;
			}
		}

		if (ShowAllButtonsOnStart)
		{
			ShowAllButtons();
		}

		ShowTitle(TitleText);

		// 玩家在设置里调过按钮位置/大小的话，在这里套上去（延时一帧，确保布局已经解析）
		CallDeferred(nameof(ApplyTouchLayout));
		GetViewport().SizeChanged += OnViewportSizeChanged;

		SetPaused(false);
	}

	/// <summary>把玩家自定义的触摸按钮布局应用到本关的按钮上。</summary>
	public void ApplyTouchLayout()
	{
		// 测默认布局用的临时探针不套自定义布局，否则量出来的是"改过的"
		if (HasMeta(TouchLayout.ProbeMeta))
		{
			return;
		}

		TouchLayout.Instance?.ApplyTo(this);
	}

	/// <summary>窗口/分辨率变了（按钮位置是按比例存的）→ 重新套一次。</summary>
	private void OnViewportSizeChanged()
	{
		ApplyTouchLayout();
	}

	/// <summary>关卡名：淡入显示一会儿再淡出，不挡操作。</summary>
	public void ShowTitle(string text)
	{
		if (_titleLabel == null || string.IsNullOrEmpty(text))
		{
			return;
		}

		_titleLabel.Text = text;
		_titleLabel.Visible = true;
		_titleLabel.Modulate = new Color(1f, 1f, 1f, 1f);

		Tween tween = CreateTween();
		tween.TweenInterval(TitleHoldTime);
		tween.TweenProperty(_titleLabel, "modulate:a", 0f, TitleFadeTime);
		tween.TweenCallback(Callable.From(() => _titleLabel.Visible = false));
	}

	/// <summary>订阅玩家的"可强化次数"，右上角显示还剩几次可以点自己升级。</summary>
	private void BindPlayer()
	{
		_player = GetTree().GetFirstNodeInGroup(Player.GroupName) as Player;

		if (_player == null)
		{
			return;
		}

		_player.UpgradeChargeChanged += OnUpgradeChargeChanged;
		OnUpgradeChargeChanged(_player.UpgradeCharges);
	}

	public override void _ExitTree()
	{
		if (_player != null && IsInstanceValid(_player))
		{
			_player.UpgradeChargeChanged -= OnUpgradeChargeChanged;
		}

		Viewport viewport = GetViewport();
		if (viewport != null && IsInstanceValid(viewport))
		{
			viewport.SizeChanged -= OnViewportSizeChanged;
		}
	}

	private void OnUpgradeChargeChanged(int charges)
	{
		if (_upgradeLabel == null)
		{
			return;
		}

		_upgradeLabel.Text = charges > 0 ? $"可强化 ×{charges}" : string.Empty;
		_upgradeLabel.Visible = charges > 0;
	}

	/// <summary>把所有触摸按钮设为可见（不改它们的绑定，只是显隐）。</summary>
	private void ShowAllButtons()
	{
		if (_touchLayer == null)
		{
			return;
		}

		ShowButtonsRecursive(_touchLayer);
		_touchLayer.Visible = true;
	}

	private static void ShowButtonsRecursive(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is TouchScreenButton button)
			{
				button.Visible = true;
				continue;
			}

			if (child.GetChildCount() > 0)
			{
				ShowButtonsRecursive(child);
			}
		}
	}

	/// <summary>别的暂停源（ui.tscn 的 PauseMenu）暂停时，把触摸按钮一起藏起来。</summary>
	public override void _Process(double delta)
	{
		if (_touchLayer == null)
		{
			return;
		}

		bool shouldShow = !GetTree().Paused;
		if (_touchLayer.Visible != shouldShow)
		{
			_touchLayer.Visible = shouldShow;
		}
	}

	/// <summary>设置顶部的关卡提示（空字符串 = 不显示）。屏幕空间文字，任何缩放下都清晰。</summary>
	public void SetHint(string text)
	{
		if (_hint == null)
		{
			return;
		}

		_hint.Text = text ?? string.Empty;
		_hint.Visible = !string.IsNullOrEmpty(_hint.Text);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!EnablePauseButton || !PauseWithEscape || !@event.IsActionPressed("ui_cancel"))
		{
			return;
		}

		// 三选一弹窗开着时，Esc 交给它自己处理（别把暂停面板和它叠在一起）
		if (GetTree().GetFirstNodeInGroup(UpgradePanel.GroupName) is UpgradePanel panel && panel.IsOpen)
		{
			return;
		}

		// 通关界面开着时同理，别让 Esc 把游戏解除暂停
		if (GetTree().GetFirstNodeInGroup(ClearPanel.GroupName) is ClearPanel clear && clear.IsOpen)
		{
			return;
		}

		// 跑酷的结算面板等：它们也占着暂停状态
		if (IsBlockedByOverlay())
		{
			return;
		}

		SetPaused(!_paused);
		GetViewport().SetInputAsHandled();
	}

	/// <summary>当前有没有别的界面占着暂停状态（跑酷结算面板等）。</summary>
	private bool IsBlockedByOverlay()
	{
		foreach (Node node in GetTree().GetNodesInGroup(PauseBlockerGroup))
		{
			if (node is CanvasItem item && item.Visible)
			{
				return true;
			}
		}

		return false;
	}

	private void Bind(string path, System.Action action)
	{
		if (GetNodeOrNull<BaseButton>(path) is BaseButton button)
		{
			button.Pressed += action;
		}
	}

	/// <summary>暂停/继续：同时把触摸按钮藏起来，避免暂停时还能按住移动。</summary>
	public void SetPaused(bool paused)
	{
		_paused = paused;

		GetTree().Paused = paused;

		if (_pausePanel != null)
		{
			_pausePanel.Visible = paused;
		}
		if (_pauseButton != null)
		{
			// 关掉自带暂停键的关卡（主线）永远不显示它
			_pauseButton.Visible = EnablePauseButton && !paused;
		}
		if (_touchLayer != null)
		{
			_touchLayer.Visible = !paused;
		}
	}

	/// <summary>退出教学关：解除暂停后回教学选关界面。</summary>
	private void ExitLevel()
	{
		GetTree().Paused = false;

		Error error = GetTree().ChangeSceneToFile(ExitScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[TutorialUi] 退出失败：{error}（{ExitScenePath}）");
		}
	}
}
