using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 三选一强化界面（CanvasLayer，独立于相机，不会被关卡缩放/滚动影响）。
///
/// 用法：
///   UpgradePanel panel = scene.Instantiate&lt;UpgradePanel&gt;();
///   AddChild(panel);
///   panel.Open(cards, "选择一项强化", option =&gt; player.ApplyUpgrade(option.Effect));
///
/// 弹出时会暂停场景树（ProcessMode = Always 保证自己还能响应），选完自动恢复。
/// 必须选一张才会关闭 —— 教学关卡就是靠"不选就走不了"来引导玩家的。
/// </summary>
public partial class UpgradePanel : CanvasLayer
{
	/// <summary>选中一张卡后发出，参数是卡上的效果。</summary>
	[Signal] public delegate void ChosenEventHandler(int effect);

	/// <summary>界面节点所在的分组（暂停界面靠它判断"三选一是不是正开着"）。</summary>
	public const string GroupName = "upgrade_panel";

	private const int MaxCards = 3;

	private readonly List<Button> _cards = new();
	private readonly List<TextureRect> _icons = new();
	private readonly List<Label> _names = new();
	private readonly List<Label> _descs = new();
	private readonly List<UpgradeOption> _pending = new();

	private Control _root;
	private Label _title;
	private Action<UpgradeOption> _onPick;
	private float _inputGuard;

	/// <summary>界面是否正开着。</summary>
	public bool IsOpen { get; private set; }

	public override void _Ready()
	{
		// 暂停时界面还要能点
		ProcessMode = ProcessModeEnum.Always;
		Layer = 10;
		AddToGroup(GroupName);

		_root = GetNodeOrNull<Control>("Root");
		_title = GetNodeOrNull<Label>("Root/Title");

		for (int i = 0; i < MaxCards; i++)
		{
			if (GetNodeOrNull($"Root/Cards/Card{i}") is not Button card)
			{
				continue;
			}

			int index = _cards.Count;
			_cards.Add(card);
			_icons.Add(card.GetNodeOrNull<TextureRect>("Icon"));
			_names.Add(card.GetNodeOrNull<Label>("Name"));
			_descs.Add(card.GetNodeOrNull<Label>("Desc"));
			card.Pressed += () => OnCardPressed(index);
		}

		SetOpen(false);
	}

	public override void _Process(double delta)
	{
		if (_inputGuard > 0f)
		{
			_inputGuard -= (float)delta;
		}
	}

	/// <summary>
	/// 打开界面并填充卡片。打开后 0.12 秒内的点击会被吞掉，
	/// 避免"点玩家那一下"顺手把刚弹出来的卡也点掉。
	/// </summary>
	public void Open(IReadOnlyList<UpgradeOption> options, string title, Action<UpgradeOption> onPick)
	{
		if (options == null || options.Count == 0)
		{
			GD.PushWarning("[UpgradePanel] 没有可选项，忽略本次弹窗");
			return;
		}

		_pending.Clear();
		for (int i = 0; i < options.Count; i++)
		{
			_pending.Add(options[i]);
		}

		_onPick = onPick;
		IsOpen = true;
		_inputGuard = 0.12f;

		if (_title != null && !string.IsNullOrEmpty(title))
		{
			_title.Text = title;
		}

		for (int i = 0; i < _cards.Count; i++)
		{
			bool has = i < _pending.Count;
			_cards[i].Visible = has;
			if (!has)
			{
				continue;
			}

			UpgradeOption option = _pending[i];

			if (_icons[i] != null)
			{
				_icons[i].Texture = ResourceLoader.Load<Texture2D>(option.IconPath);
				_icons[i].Modulate = option.IsUnlock ? new Color(1f, 0.9f, 0.35f) : Colors.White;
			}
			if (_names[i] != null)
			{
				_names[i].Text = option.Title;
			}
			if (_descs[i] != null)
			{
				_descs[i].Text = option.Desc;
				// 解锁武器类的说明用暖色，一眼能看出"这次是拿到东西"而不是纯数值
				_descs[i].Modulate = option.IsUnlock ? new Color(0.72f, 0.42f, 0f) : new Color(0.18f, 0.18f, 0.2f);
			}
		}

		GetTree().Paused = true;
		SetOpen(true);
	}

	/// <summary>关闭界面并取消暂停。</summary>
	public void Close()
	{
		if (!IsOpen)
		{
			return;
		}

		IsOpen = false;
		_onPick = null;
		SetOpen(false);
		GetTree().Paused = false;
	}

	private void OnCardPressed(int index)
	{
		if (!IsOpen || _inputGuard > 0f || index >= _pending.Count)
		{
			return;
		}

		UpgradeOption picked = _pending[index];
		Action<UpgradeOption> callback = _onPick;

		Close();

		EmitSignal(SignalName.Chosen, (int)picked.Effect);
		callback?.Invoke(picked);
	}

	private void SetOpen(bool open)
	{
		if (_root != null)
		{
			_root.Visible = open;
		}
	}
}
