using Godot;

/// <summary>
/// 主页：标题 + 开始 / 退出。
///   · 开始 → 切到 <see cref="GameScenePath"/>（默认 node/scene/node_1.tscn）；
///   · 退出 → 关掉游戏（游戏里的"退出"是回主页，见 PauseMenu / DeathPanel）；
///   · Enter / 空格 也能开始。
///
/// 布局：文字/按钮全部用锚点自适应（窗口变宽变高会自动重新居中、底部提示自动贴底），
/// 只有 logo 是 AnimatedSprite2D（Node2D 不吃锚点），所以按根节点尺寸在代码里手动居中。
/// </summary>
public partial class HomeMenu : Control
{
	/// <summary>点开始后要进的关卡场景。</summary>
	[Export] public string GameScenePath = ScenePaths.Game;

	/// <summary>logo 播放的动画名（home.tscn 里 Logo 的 SpriteFrames 里只有一条 idle）。</summary>
	[Export] public StringName LogoAnimation = "idle";

	/// <summary>logo 中心相对屏幕中心的上移量（像素）。</summary>
	[Export] public float LogoOffsetY = 188f;

	/// <summary>logo 额外的自转速度（度/秒）。帧动画本身已经在转了，默认 0，想要更飘就调大。</summary>
	[Export] public float LogoSpinSpeed = 0f;

	private AnimatedSprite2D _logo;

	public override void _Ready()
	{
		// 从游戏里退回/重开时，别带着暂停状态
		GetTree().Paused = false;

		_logo = GetNodeOrNull<AnimatedSprite2D>("Logo");
		if (_logo != null && _logo.SpriteFrames != null && _logo.SpriteFrames.HasAnimation(LogoAnimation))
		{
			_logo.Play(LogoAnimation);
		}

		// 窗口尺寸一变就重新摆 logo
		Resized += LayoutLogo;
		LayoutLogo();

		TextureButton startButton = GetNodeOrNull<TextureButton>("StartButton");
		if (startButton != null)
		{
			startButton.Pressed += StartGame;
		}

		TextureButton exitButton = GetNodeOrNull<TextureButton>("ExitButton");
		if (exitButton != null)
		{
			exitButton.Pressed += QuitGame;
		}
	}

	public override void _Process(double delta)
	{
		if (_logo == null || LogoSpinSpeed == 0f)
		{
			return;
		}

		float rotation = _logo.Rotation + Mathf.DegToRad(LogoSpinSpeed) * (float)delta;
		_logo.Rotation = rotation > Mathf.Tau ? rotation - Mathf.Tau : rotation;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_accept"))
		{
			StartGame();
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>让 logo 始终水平居中、垂直方向比屏幕中心高 LogoOffsetY。</summary>
	private void LayoutLogo()
	{
		if (_logo == null)
		{
			return;
		}

		_logo.Position = new Vector2(Size.X * 0.5f, Size.Y * 0.5f - LogoOffsetY);
	}

	/// <summary>进入游戏。</summary>
	public void StartGame()
	{
		Error error = GetTree().ChangeSceneToFile(GameScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeMenu] 切换场景失败：{error}（{GameScenePath}）");
		}
	}

	private void QuitGame()
	{
		GetTree().Quit();
	}
}
