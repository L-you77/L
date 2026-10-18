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

	/// <summary>Esc 也能开关暂停。</summary>
	[Export] public bool PauseWithEscape = true;

	private Control _pausePanel;
	private Control _touchLayer;
	private TextureButton _pauseButton;
	private bool _paused;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = 5;

		_pausePanel = GetNodeOrNull<Control>("Root/PausePanel");
		_touchLayer = GetNodeOrNull<Control>("Root/TouchLayer");
		_pauseButton = GetNodeOrNull<TextureButton>("Root/PauseButton");

		Bind("Root/PauseButton", () => SetPaused(true));
		Bind("Root/PausePanel/ResumeButton", () => SetPaused(false));
		Bind("Root/PausePanel/ExitButton", ExitLevel);

		SetPaused(false);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!PauseWithEscape || !@event.IsActionPressed("ui_cancel"))
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

		SetPaused(!_paused);
		GetViewport().SetInputAsHandled();
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
			_pauseButton.Visible = !paused;
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
