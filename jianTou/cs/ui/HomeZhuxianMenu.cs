using Godot;

/// <summary>
/// 主线选关界面（home_zhuxian），布局和教学选关 home_jiaoxue 一致：
/// 一排关卡按钮 + 各自下方的"第 N 关"标签 + 右下返回。
///
/// 关卡按钮在场景里摆了 3 个槽位（Slot1~Slot3），
/// **<see cref="Level2Path"/> / <see cref="Level3Path"/> 留空时对应的槽位会自动隐藏**，
/// 以后加了主线关卡，填上路径就会自己露出来。
/// </summary>
public partial class HomeZhuxianMenu : Control
{
	/// <summary>第一关（默认是 node_1）。</summary>
	[Export] public string Level1Path = ScenePaths.Game;

	/// <summary>第二关（留空 = 隐藏）。</summary>
	[Export] public string Level2Path = ScenePaths.Main2;

	/// <summary>第三关（留空 = 隐藏）。</summary>
	[Export] public string Level3Path = ScenePaths.Main3;

	/// <summary>"返回"回哪一层（默认回 home_2）。</summary>
	[Export] public string BackScenePath = ScenePaths.Home2;

	public override void _Ready()
	{
		GetTree().Paused = false;

		BindSlot(1, Level1Path);
		BindSlot(2, Level2Path);
		BindSlot(3, Level3Path);

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

	/// <summary>绑定第 index 个关卡槽位；路径为空就把整个槽位藏掉。</summary>
	private void BindSlot(int index, string path)
	{
		if (GetNodeOrNull<Control>($"LevelList/Slot{index}") is not Control slot)
		{
			return;
		}

		bool hasLevel = !string.IsNullOrEmpty(path);
		slot.Visible = hasLevel;

		if (!hasLevel)
		{
			return;
		}

		if (slot.GetNodeOrNull<BaseButton>("Btn") is BaseButton button)
		{
			button.Pressed += () => Go(path);
		}
	}

	private void Go(string path)
	{
		Error error = GetTree().ChangeSceneToFile(path);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeZhuxianMenu] 切换场景失败：{error}（{path}）");
		}
	}

	private void GoBack()
	{
		Go(BackScenePath);
	}
}
