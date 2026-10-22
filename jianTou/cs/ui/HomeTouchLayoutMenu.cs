using System.Collections.Generic;
using Godot;

/// <summary>
/// 「按钮布局」设置页（从「设置」里的「调整」进来）。
///
///   · 中间是一块按 16:9 缩放的"假屏幕"（Stage），里面按当前布局摆着所有触摸按钮的预览；
///   · **拖动预览**就能改位置（松手自动存盘，下一个关卡立刻生效）；
///   · **点一下**选中某个按钮，右边的「选中按钮大小」滑条改它的大小；
///   · 「整体大小」按比例缩放全部按钮，「重置为默认」回到场景原始布局。
///
/// 数据都存在 <see cref="TouchLayout"/> 里（归一化坐标），所以换分辨率也不会跑位。
/// </summary>
public partial class HomeTouchLayoutMenu : Control
{
	/// <summary>"返回"回哪一层（默认回设置页）。</summary>
	[Export] public string BackScenePath = ScenePaths.Settings;

	/// <summary>Stage 没有布局完成时的兜底宽高（场景里摆的是 624×351）。</summary>
	[Export] public Vector2 FallbackStageSize = new(624f, 351f);

	/// <summary>按钮名 → 界面上的中文名。</summary>
	private static readonly Dictionary<string, string> DisplayNames = new()
	{
		{ "TBtnL", "向左" },
		{ "TBtnR", "向右" },
		{ "TBtnFH", "远程" },
		{ "TBtnNH", "近战" },
		{ "TBtnD", "向下" },
		{ "TBtnU", "跳跃" },
	};

	private Control _stage;
	private HSlider _allSlider;
	private HSlider _oneSlider;
	private Label _allValue;
	private Label _oneTitle;
	private Label _oneValue;
	private BaseButton _resetButton;

	private readonly Dictionary<string, TextureRect> _previews = new();
	private readonly Dictionary<string, Vector2> _textureSizes = new();

	private string _selected;
	private string _dragging;
	private float _allScale = 1f;
	private bool _syncing;

	private TouchLayout Layout => TouchLayout.Instance;

	private float PreviewScale
	{
		get
		{
			float width = _stage != null && _stage.Size.X > 1f ? _stage.Size.X : FallbackStageSize.X;
			float viewportWidth = GetViewport().GetVisibleRect().Size.X;
			return viewportWidth > 1f ? width / viewportWidth : 1f;
		}
	}

	public override async void _Ready()
	{
		GetTree().Paused = false;

		_stage = GetNodeOrNull<Control>("Stage/Inner");
		_allSlider = GetNodeOrNull<HSlider>("Side/Body/AllSlider");
		_oneSlider = GetNodeOrNull<HSlider>("Side/Body/OneSlider");
		_allValue = GetNodeOrNull<Label>("Side/Body/AllValue");
		_oneTitle = GetNodeOrNull<Label>("Side/Body/OneTitle");
		_oneValue = GetNodeOrNull<Label>("Side/Body/OneValue");
		_resetButton = GetNodeOrNull<BaseButton>("Side/Body/ResetButton");

		if (GetNodeOrNull<BaseButton>("ExitButton") is BaseButton exit)
		{
			exit.Pressed += GoBack;
		}

		if (_allSlider != null)
		{
			_allSlider.Value = 1.0;
			_allSlider.ValueChanged += OnAllScaleChanged;
		}
		if (_oneSlider != null)
		{
			_oneSlider.ValueChanged += OnOneScaleChanged;
		}
		if (_resetButton != null)
		{
			_resetButton.Pressed += OnResetPressed;
		}

		SyncAllLabel();
		SyncSelectionUi();

		// 默认布局要实例化一份按钮场景量一帧，量完再画预览
		await TouchLayout.EnsureDefaultsAsync(this);

		if (!IsInstanceValid(this) || !IsInsideTree())
		{
			return;
		}

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (!IsInstanceValid(this) || _stage == null)
		{
			return;
		}

		_stage.Resized += OnStageResized;
		BuildPreviews();
		RefreshPreviews();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GoBack();
			GetViewport().SetInputAsHandled();
		}
	}

	// ------------------------------------------------------------------
	// 预览
	// ------------------------------------------------------------------

	private void BuildPreviews()
	{
		if (Layout == null || TouchLayout.Defaults == null)
		{
			GD.PushWarning("[HomeTouchLayout] 拿不到默认布局，预览为空");
			return;
		}

		foreach (string name in TouchLayout.Defaults.Keys)
		{
			Texture2D texture = TouchLayout.TextureFor(name);
			Vector2 size = texture?.GetSize() ?? new Vector2(100f, 100f);
			_textureSizes[name] = size;

			TextureRect preview = new()
			{
				Name = name,
				Texture = texture,
				MouseFilter = MouseFilterEnum.Stop,
				MouseDefaultCursorShape = CursorShape.PointingHand,
				TooltipText = DisplayName(name),
			};

			string key = name;
			preview.GuiInput += @event => OnPreviewInput(@event, key);

			_stage.AddChild(preview);
			_previews[name] = preview;
		}
	}

	/// <summary>按当前布局重设每个预览的大小和位置。</summary>
	private void RefreshPreviews()
	{
		if (Layout == null || _stage == null)
		{
			return;
		}

		float scale = PreviewScale;
		Vector2 stage = StageSize;

		foreach (KeyValuePair<string, TextureRect> pair in _previews)
		{
			if (!IsInstanceValid(pair.Value))
			{
				continue;
			}

			TouchLayout.Entry entry = Layout.Get(pair.Key);
			Vector2 baseSize = _textureSizes.TryGetValue(pair.Key, out Vector2 tex) ? tex : new Vector2(100f, 100f);
			Vector2 size = baseSize * entry.Scale * scale;

			pair.Value.Size = size;
			pair.Value.Position = entry.Norm * stage - size * 0.5f;
			pair.Value.Modulate = pair.Key == _selected
				? new Color(1f, 0.93f, 0.62f)
				: Colors.White;
		}
	}

	private Vector2 StageSize => _stage != null && _stage.Size.X > 1f ? _stage.Size : FallbackStageSize;

	private void OnStageResized()
	{
		RefreshPreviews();
	}

	private void OnPreviewInput(InputEvent @event, string name)
	{
		if (Layout == null || !_previews.TryGetValue(name, out TextureRect preview))
		{
			return;
		}

		switch (@event)
		{
			case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left:
				if (button.Pressed)
				{
					_dragging = name;
					preview.MoveToFront();
					Select(name);
				}
				else if (_dragging == name)
				{
					_dragging = null;
				}
				preview.AcceptEvent();
				break;

			case InputEventMouseMotion motion when _dragging == name:
				if ((motion.ButtonMask & MouseButtonMask.Left) == 0)
				{
					_dragging = null;
					break;
				}

				Vector2 stage = StageSize;
				Vector2 position = preview.Position + motion.Relative;
				position.X = Mathf.Clamp(position.X, 0f, Mathf.Max(0f, stage.X - preview.Size.X));
				position.Y = Mathf.Clamp(position.Y, 0f, Mathf.Max(0f, stage.Y - preview.Size.Y));
				preview.Position = position;

				Vector2 norm = (position + preview.Size * 0.5f) / stage;
				TouchLayout.Entry current = Layout.Get(name);
				Layout.Set(name, norm, current.Scale);

				preview.AcceptEvent();
				break;
		}
	}

	private void Select(string name)
	{
		_selected = name;

		TouchLayout.Entry entry = Layout?.Get(name) ?? new TouchLayout.Entry { Scale = TouchLayout.DefaultScale };

		_syncing = true;
		if (_oneSlider != null)
		{
			_oneSlider.Value = entry.Scale;
		}
		_syncing = false;

		SyncSelectionUi();
		RefreshPreviews();
	}

	private void SyncSelectionUi()
	{
		string display = string.IsNullOrEmpty(_selected) ? "无" : DisplayName(_selected);

		if (_oneTitle != null)
		{
			_oneTitle.Text = $"选中：{display}";
		}

		if (_oneValue != null)
		{
			double scale = _oneSlider?.Value ?? TouchLayout.DefaultScale;
			_oneValue.Text = string.IsNullOrEmpty(_selected) ? "—" : $"{scale:0.00}×";
		}

		if (_oneSlider != null)
		{
			_oneSlider.Editable = !string.IsNullOrEmpty(_selected);
			_oneSlider.Modulate = string.IsNullOrEmpty(_selected)
				? new Color(1f, 1f, 1f, 0.45f)
				: Colors.White;
		}
	}

	// ------------------------------------------------------------------
	// 滑条
	// ------------------------------------------------------------------

	/// <summary>整体大小：按比例缩放所有按钮（保留单个按钮之间的相对大小）。</summary>
	private void OnAllScaleChanged(double value)
	{
		if (_syncing || Layout == null || TouchLayout.Defaults == null)
		{
			return;
		}

		float target = (float)value;
		float ratio = _allScale > 0.001f ? target / _allScale : 1f;

		if (!Mathf.IsEqualApprox(ratio, 1f))
		{
			foreach (string name in TouchLayout.Defaults.Keys)
			{
				TouchLayout.Entry current = Layout.Get(name);
				Layout.Set(name, current.Norm, current.Scale * ratio);
			}
		}

		_allScale = target;
		SyncAllLabel();
		RefreshPreviews();

		// 选中项的显示跟着变
		if (!string.IsNullOrEmpty(_selected))
		{
			Select(_selected);
		}
	}

	private void SyncAllLabel()
	{
		if (_allValue != null)
		{
			_allValue.Text = $"{Mathf.RoundToInt(_allScale * 100f)}%";
		}
	}

	private void OnOneScaleChanged(double value)
	{
		if (_syncing || Layout == null || string.IsNullOrEmpty(_selected))
		{
			return;
		}

		TouchLayout.Entry current = Layout.Get(_selected);
		Layout.Set(_selected, current.Norm, (float)value);

		_oneValue.Text = $"{value:0.00}×";
		RefreshPreviews();
	}

	private void OnResetPressed()
	{
		Layout?.ResetAll();

		_syncing = true;
		if (_allSlider != null)
		{
			_allSlider.Value = 1.0;
		}
		_syncing = false;

		_allScale = 1f;
		SyncAllLabel();

		if (!string.IsNullOrEmpty(_selected))
		{
			Select(_selected);
		}
		else
		{
			RefreshPreviews();
		}
	}

	// ------------------------------------------------------------------
	// 其它
	// ------------------------------------------------------------------

	private static string DisplayName(string name)
	{
		return DisplayNames.TryGetValue(name, out string display) ? display : name;
	}

	private void GoBack()
	{
		// 拖动是延时写盘的，离开前先落盘，免得刚拖完就返回导致丢失
		Layout?.Flush();

		Error error = GetTree().ChangeSceneToFile(BackScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeTouchLayout] 返回失败：{error}（{BackScenePath}）");
		}
	}
}
