using Godot;

/// <summary>
/// 教学选关界面（home_jiaoxue）。
///
/// 四个按钮对应四个教学关，按钮下面各有一个标签（第一关~第四关）：
///   Btn1 → node_right（第一关，向右）
///   Btn2 → node_left （第二关，向左）
///   Btn3 → node_up   （第三关，向上）
///   Btn4 → node_near_hit（第四关，近战，真随机强化）
/// </summary>
public partial class HomeJiaoxueMenu : Control
{
	[Export] public string Level1Path = ScenePaths.TutorialRight;
	[Export] public string Level2Path = ScenePaths.TutorialLeft;
	[Export] public string Level3Path = ScenePaths.TutorialUp;
	[Export] public string Level4Path = ScenePaths.TutorialNearHit;

	/// <summary>"返回"回哪一层（默认回 home_2）。</summary>
	[Export] public string BackScenePath = ScenePaths.Home2;

	public override void _Ready()
	{
		GetTree().Paused = false;

		Bind("Btn1", Level1Path);
		Bind("Btn2", Level2Path);
		Bind("Btn3", Level3Path);
		Bind("Btn4", Level4Path);

		if (GetNodeOrNull<BaseButton>("ExitButton") is BaseButton exit)
		{
			exit.Pressed += GoBack;
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

	private void Bind(string buttonName, string scenePath)
	{
		if (GetNodeOrNull<BaseButton>(buttonName) is BaseButton button)
		{
			button.Pressed += () => Go(scenePath);
		}
	}

	private void Go(string path)
	{
		Error error = GetTree().ChangeSceneToFile(path);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeJiaoxueMenu] 切换场景失败：{error}（{path}）");
		}
	}

	private void GoBack()
	{
		Go(BackScenePath);
	}
}
