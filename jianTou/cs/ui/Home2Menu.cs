using Godot;

/// <summary>
/// 第二层主页（home_2）：教学 / 主线 / 无限 / 退出。
///
///   · jiaoXue  → 教学选关（home_jiaoxue）
///   · zhuXian  → 主线选关（home_zhuxian），再从那进具体关卡
///   · 无限（右下 wuXian 按钮）→ 目前暂用主线关卡，等"无限模式"做出来再改 <see cref="InfiniteScenePath"/>
///   · 退出 → 关游戏
/// </summary>
public partial class Home2Menu : Control
{
	/// <summary>教学选关场景。</summary>
	[Export] public string TutorialSelectScenePath = ScenePaths.TutorialSelect;

	/// <summary>主线选关场景。</summary>
	[Export] public string MainSelectScenePath = ScenePaths.MainSelect;

	/// <summary>无限模式场景（暂时指向主线，做了再单独建场景）。</summary>
	[Export] public string InfiniteScenePath = ScenePaths.Game;

	/// <summary>zhuXian 按钮里那个 logo 的动画名。</summary>
	[Export] public StringName LogoAnimation = "idle";

	public override void _Ready()
	{
		GetTree().Paused = false;

		Bind("jiaoXue", () => Go(TutorialSelectScenePath));
		Bind("zhuXian", () => Go(MainSelectScenePath));
		Bind("ExitButton2", () => Go(InfiniteScenePath));
		Bind("ExitButton", QuitGame);

		if (GetNodeOrNull<AnimatedSprite2D>("zhuXian/Logo") is AnimatedSprite2D logo
			&& logo.SpriteFrames != null
			&& logo.SpriteFrames.HasAnimation(LogoAnimation))
		{
			logo.Play(LogoAnimation);
		}
	}

	private void Bind(string nodeName, System.Action action)
	{
		if (GetNodeOrNull<BaseButton>(nodeName) is BaseButton button)
		{
			button.Pressed += action;
		}
	}

	private void Go(string path)
	{
		Error error = GetTree().ChangeSceneToFile(path);
		if (error != Error.Ok)
		{
			GD.PushError($"[Home2Menu] 切换场景失败：{error}（{path}）");
		}
	}

	private void QuitGame()
	{
		GetTree().Quit();
	}
}
