using Godot;

/// <summary>
/// 死亡面板（挂在 ui.tscn 的 DeathPanel 上）：玩家死亡后弹出半透明遮罩 + 重来按钮。
/// 重来 = 让玩家 <see cref="Player.Restart"/> 重开本关（Player.WaitForRestart 要开着，
/// 否则玩家会自己先重开，按钮来不及点）。
/// </summary>
public partial class DeathPanel : Control
{
	private Player _player;
	private TextureButton _restartButton;
	private TextureButton _exitButton;

	public override void _Ready()
	{
		// 暂停/死亡时 UI 也要能点
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;

		_restartButton = GetNodeOrNull<TextureButton>("RestartButton");
		if (_restartButton != null)
		{
			_restartButton.Pressed += OnRestartPressed;
		}

		_exitButton = GetNodeOrNull<TextureButton>("ExitButton");
		if (_exitButton != null)
		{
			_exitButton.Pressed += OnExitPressed;
		}

		_player = GetTree().GetFirstNodeInGroup(Player.GroupName) as Player;
		if (_player == null)
		{
			GD.PushWarning("[DeathPanel] 找不到玩家节点，死亡面板不会自动弹出");
			return;
		}

		_player.Died += OnPlayerDied;
	}

	private void OnPlayerDied()
	{
		Visible = true;
	}

	private void OnRestartPressed()
	{
		if (_player != null)
		{
			_player.Restart();
			return;
		}

		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}

	/// <summary>退出：回到主页。</summary>
	/// <summary>退出当前关卡：回到当前关卡所属的选关界面。</summary>
	private void OnExitPressed()
	{
		GetTree().Paused = false;

		string target = ScenePaths.CurrentLevelSelect(GetTree());
		Error error = GetTree().ChangeSceneToFile(target);
		if (error != Error.Ok)
		{
			GD.PushError($"[DeathPanel] 退出关卡失败：{error}（{target}）");
		}
	}
}
