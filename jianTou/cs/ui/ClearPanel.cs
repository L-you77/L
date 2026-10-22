using Godot;

/// <summary>
/// 通关界面（由 <see cref="TongGuan"/> 在玩家碰到通关点时实例化）。
///
/// 显示"通关！" + 关卡副标题，下面两个按钮：
///   · 下一关（有 <c>NextScenePath</c> 才显示）
///   · 返回（默认回教学选关）
/// 弹出时暂停游戏；按钮切换场景前会先解除暂停，新场景才不会一进去就是暂停状态。
///
/// Layer = 12，压在教学 UI（5）和三选一（10）上面。
/// </summary>
public partial class ClearPanel : CanvasLayer
{
	/// <summary>节点分组，教学 UI 靠它判断"通关界面是不是正开着"。</summary>
	public const string GroupName = "clear_panel";

	private Control _root;
	private Label _title;
	private Label _subtitle;
	private Label _coinLabel;
	private Label _bonusLabel;
	private TextureButton _nextButton;
	private TextureButton _backButton;
	private Label _nextLabel;
	private Label _backLabel;

	private string _nextPath = string.Empty;
	private string _backPath = string.Empty;

	/// <summary>界面是否正开着。</summary>
	public bool IsOpen { get; private set; }

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = 12;
		AddToGroup(GroupName);

		_root = GetNodeOrNull<Control>("Root");
		_title = GetNodeOrNull<Label>("Root/Title");
		_subtitle = GetNodeOrNull<Label>("Root/Subtitle");
		_coinLabel = GetNodeOrNull<Label>("Root/CoinLabel");
		_bonusLabel = GetNodeOrNull<Label>("Root/BonusLabel");
		_nextButton = GetNodeOrNull<TextureButton>("Root/NextButton");
		_nextLabel = GetNodeOrNull<Label>("Root/NextLabel");
		_backButton = GetNodeOrNull<TextureButton>("Root/BackButton");
		_backLabel = GetNodeOrNull<Label>("Root/BackLabel");

		if (_nextButton != null)
		{
			_nextButton.Pressed += OnNextPressed;
		}
		if (_backButton != null)
		{
			_backButton.Pressed += OnBackPressed;
		}

		SetOpen(false);
	}

	/// <summary>
	/// 显示通关界面并暂停。
	/// <paramref name="coins"/> = 本关带出的金币数；传负数（默认）就不显示这一行。
	/// </summary>
	/// <param name="bonus">额外奖励（教学全部通关），传 0 就不显示这一行。</param>
	public void ShowClear(string title, string subtitle, string nextPath, string backPath, int coins = -1, int bonus = 0)
	{
		_nextPath = nextPath ?? string.Empty;
		_backPath = backPath ?? string.Empty;

		if (_title != null && !string.IsNullOrEmpty(title))
		{
			_title.Text = title;
		}

		if (_subtitle != null)
		{
			_subtitle.Text = subtitle ?? string.Empty;
			_subtitle.Visible = !string.IsNullOrEmpty(_subtitle.Text);
		}

		if (_coinLabel != null)
		{
			_coinLabel.Visible = coins >= 0;
			_coinLabel.Text = coins > 0
				? $"本关金币 +{coins} · 已存入"
				: "本关没有带出金币";
		}

		if (_bonusLabel != null)
		{
			_bonusLabel.Visible = bonus > 0;
			_bonusLabel.Text = $"教学全部通关 +{bonus} coin";
		}

		bool hasNext = !string.IsNullOrEmpty(_nextPath);

		if (_nextButton != null)
		{
			_nextButton.Visible = hasNext;
		}
		if (_nextLabel != null)
		{
			_nextLabel.Visible = hasNext;
		}

		LayoutButtons(hasNext);

		GetTree().Paused = true;
		SetOpen(true);
	}

	/// <summary>没有下一关时，"返回"按钮居中；有两个按钮时左右排开。</summary>
	private void LayoutButtons(bool hasNext)
	{
		if (_backButton != null)
		{
			_backButton.OffsetLeft = hasNext ? -100f : -44f;
			_backButton.OffsetRight = hasNext ? -12f : 44f;
		}
		if (_backLabel != null)
		{
			_backLabel.OffsetLeft = hasNext ? -100f : -44f;
			_backLabel.OffsetRight = hasNext ? -12f : 44f;
		}
		if (_nextButton != null)
		{
			_nextButton.OffsetLeft = 12f;
			_nextButton.OffsetRight = 100f;
		}
		if (_nextLabel != null)
		{
			_nextLabel.OffsetLeft = 12f;
			_nextLabel.OffsetRight = 100f;
		}
	}

	private void OnNextPressed()
	{
		Go(_nextPath);
	}

	private void OnBackPressed()
	{
		Go(_backPath);
	}

	private void Go(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return;
		}

		GetTree().Paused = false;

		Error error = GetTree().ChangeSceneToFile(path);
		if (error != Error.Ok)
		{
			GD.PushError($"[ClearPanel] 切换场景失败：{error}（{path}）");
		}
	}

	private void SetOpen(bool open)
	{
		IsOpen = open;

		if (_root != null)
		{
			_root.Visible = open;
		}
	}
}
