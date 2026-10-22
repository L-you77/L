using Godot;

/// <summary>
/// 设置界面（从 home_2 的「设置」按钮进来）。
///
///   · 音乐音量 / 音效音量：0~100% 滑条，拖动即时生效；
///   · 全屏：勾选后立即切换窗口模式；
///   · 全都存在 <c>user://jiantou_audio.cfg</c>，下次启动沿用。
///
/// 返回回 home_2，Esc 也行。界面本身不暂停游戏（从菜单进来的，本来就是暂停态）。
/// </summary>
public partial class HomeSettingsMenu : Control
{
	/// <summary>"返回"回哪一层（默认 home_2）。</summary>
	[Export] public string BackScenePath = ScenePaths.Home2;

	/// <summary>「按钮布局」页（拖动调整触摸按钮的位置和大小）。</summary>
	[Export] public string TouchLayoutScenePath = ScenePaths.TouchLayoutScene;

	private HSlider _musicSlider;
	private HSlider _sfxSlider;
	private Label _musicValue;
	private Label _sfxValue;
	private CheckButton _fullscreenCheck;

	public override void _Ready()
	{
		GetTree().Paused = false;

		_musicSlider = GetNodeOrNull<HSlider>("Rows/MusicRow/Slider");
		_sfxSlider = GetNodeOrNull<HSlider>("Rows/SfxRow/Slider");
		_musicValue = GetNodeOrNull<Label>("Rows/MusicRow/Value");
		_sfxValue = GetNodeOrNull<Label>("Rows/SfxRow/Value");
		_fullscreenCheck = GetNodeOrNull<CheckButton>("Rows/FullscreenRow/Check");

		float music = AudioManager.Instance?.MusicVolume ?? 0.4f;
		float sfx = AudioManager.Instance?.SfxVolume ?? 0.7f;

		if (_musicSlider != null)
		{
			_musicSlider.Value = music;
			_musicSlider.ValueChanged += OnMusicChanged;
		}
		if (_sfxSlider != null)
		{
			_sfxSlider.Value = sfx;
			_sfxSlider.ValueChanged += OnSfxChanged;
		}

		SyncLabel(_musicValue, music);
		SyncLabel(_sfxValue, sfx);

		if (_fullscreenCheck != null)
		{
			_fullscreenCheck.ButtonPressed = AudioManager.Instance?.Fullscreen ?? false;
			_fullscreenCheck.Toggled += OnFullscreenToggled;
		}

		if (GetNodeOrNull<BaseButton>("ExitButton") is BaseButton exit)
		{
			exit.Pressed += GoBack;
		}

		if (GetNodeOrNull<BaseButton>("Rows/TouchRow/Adjust") is BaseButton adjust)
		{
			adjust.Pressed += OpenTouchLayout;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GoBack();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnMusicChanged(double value)
	{
		AudioManager.Instance?.SetMusicVolume((float)value);
		SyncLabel(_musicValue, (float)value);
	}

	private void OnSfxChanged(double value)
	{
		AudioManager.Instance?.SetSfxVolume((float)value);
		SyncLabel(_sfxValue, (float)value);
	}

	private void OnFullscreenToggled(bool pressed)
	{
		AudioManager.Instance?.SetFullscreen(pressed);
	}

	private static void SyncLabel(Label label, float value)
	{
		if (label != null)
		{
			label.Text = $"{Mathf.RoundToInt(value * 100f)}%";
		}
	}

	/// <summary>进「按钮布局」页。</summary>
	private void OpenTouchLayout()
	{
		Error error = GetTree().ChangeSceneToFile(TouchLayoutScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeSettingsMenu] 打开按钮布局失败：{error}（{TouchLayoutScenePath}）");
		}
	}

	private void GoBack()
	{
		Error error = GetTree().ChangeSceneToFile(BackScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeSettingsMenu] 返回失败：{error}（{BackScenePath}）");
		}
	}
}
