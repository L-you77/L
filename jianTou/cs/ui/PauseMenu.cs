using Godot;

/// <summary>
/// 暂停界面（挂在 node/ui/ui.tscn 的根 Control 上）。
///   点「暂停」按钮 → GetTree().Paused = true，弹出面板；
///   面板里三个按钮：退出 / 重来 / 继续，Esc 也可以来回切。
///
/// 关键点：本节点 ProcessMode 必须是 Always，否则暂停后它自己也停了，按钮就点不动了。
/// </summary>
public partial class PauseMenu : Control
{
	private Control _panel;
	private TextureButton _pauseButton;
	private TextureButton _resumeButton;
	private TextureButton _restartButton;
	private TextureButton _exitButton;

	public override void _Ready()
	{
		// 暂停时 UI 必须照常响应
		ProcessMode = ProcessModeEnum.Always;

		_panel = GetNodeOrNull<Control>("PausePanel");
		_pauseButton = GetNodeOrNull<TextureButton>("PauseButton");
		_resumeButton = GetNodeOrNull<TextureButton>("PausePanel/ResumeButton");
		_restartButton = GetNodeOrNull<TextureButton>("PausePanel/RestartButton");
		_exitButton = GetNodeOrNull<TextureButton>("PausePanel/ExitButton");

		if (_pauseButton != null)
		{
			_pauseButton.Pressed += OnPausePressed;
		}
		if (_resumeButton != null)
		{
			_resumeButton.Pressed += OnResumePressed;
		}
		if (_restartButton != null)
		{
			_restartButton.Pressed += OnRestartPressed;
		}
		if (_exitButton != null)
		{
			_exitButton.Pressed += OnExitPressed;
		}

		// 从暂停状态重开场景时，避免一进来就是暂停的
		SetPaused(false);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			SetPaused(!GetTree().Paused);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnPausePressed()
	{
		SetPaused(true);
	}

	private void OnResumePressed()
	{
		SetPaused(false);
	}

	/// <summary>重来：解除暂停并重开本关（新场景才不会带着暂停状态）。</summary>
	private void OnRestartPressed()
	{
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}

	/// <summary>退出：解除暂停并回到主页。</summary>
	/// <summary>退出当前关卡：回到当前关卡所属的选关界面（教学关回教学选关，主线回主线选关）。</summary>
	private void OnExitPressed()
	{
		GetTree().Paused = false;

		string target = ScenePaths.CurrentLevelSelect(GetTree());
		Error error = GetTree().ChangeSceneToFile(target);
		if (error != Error.Ok)
		{
			GD.PushError($"[PauseMenu] 退出关卡失败：{error}（{target}）");
		}
	}

	private void SetPaused(bool paused)
	{
		GetTree().Paused = paused;

		if (_panel != null)
		{
			_panel.Visible = paused;
		}
		if (_pauseButton != null)
		{
			_pauseButton.Visible = !paused;
		}
	}
}
