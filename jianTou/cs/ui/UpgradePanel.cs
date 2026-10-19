using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 三选一强化界面（CanvasLayer，独立于相机，不会被关卡缩放/滚动影响）。
///
/// 用法：
///   UpgradePanel panel = scene.Instantiate&lt;UpgradePanel&gt;();
///   AddChild(panel);
///   panel.Open(cards, "选择一项强化", option =&gt; player.ApplyUpgrade(option.Effect),
///              refresh: () =&gt; UpgradePool.Draw(player, mode));
///
/// 弹出时会暂停场景树（ProcessMode = Always 保证自己还能响应），选完/取消后自动恢复。
/// **不再强制必须选一项**：按 Esc（或点右下角的关闭按钮以外的方式）就能关掉，关了算放弃。
/// 传了 refresh 回调才会显示"刷新"按钮 —— 点一次重新抽三张。
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
	private Button _refreshButton;
	private Action<UpgradeOption> _onPick;
	private Func<List<UpgradeOption>> _refresh;
	private Action _onCancel;
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
		_refreshButton = GetNodeOrNull<Button>("Root/RefreshButton");

		if (_refreshButton != null)
		{
			_refreshButton.Pressed += OnRefreshPressed;
		}

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

	/// <summary>Esc = 不选，直接关掉（会解除暂停）。</summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen || !@event.IsActionPressed("ui_cancel"))
		{
			return;
		}

		Cancel();
		GetViewport().SetInputAsHandled();
	}

	/// <summary>
	/// 打开界面并填充卡片。打开后 0.12 秒内的点击会被吞掉，
	/// 避免"点玩家那一下"顺手把刚弹出来的卡也点掉。
	/// </summary>
	/// <param name="refresh">点"刷新"时重新抽卡的回调；传 null 就不显示刷新按钮。</param>
	/// <param name="onCancel">没选就关掉时调用（比如商城要退钱）。</param>
	public void Open(
		IReadOnlyList<UpgradeOption> options,
		string title,
		Action<UpgradeOption> onPick,
		Func<List<UpgradeOption>> refresh = null,
		Action onCancel = null)
	{
		if (options == null || options.Count == 0)
		{
			GD.PushWarning("[UpgradePanel] 没有可选项，忽略本次弹窗");
			return;
		}

		_onPick = onPick;
		_refresh = refresh;
		_onCancel = onCancel;
		IsOpen = true;
		_inputGuard = 0.12f;

		if (_title != null && !string.IsNullOrEmpty(title))
		{
			_title.Text = title;
		}

		if (_refreshButton != null)
		{
			_refreshButton.Visible = refresh != null;
		}

		FillCards(options);

		GetTree().Paused = true;
		SetOpen(true);
	}

	/// <summary>关闭界面并取消暂停（不触发任何回调 = 放弃这次强化）。</summary>
	public void Close()
	{
		if (!IsOpen)
		{
			return;
		}

		IsOpen = false;
		_onPick = null;
		_refresh = null;
		_onCancel = null;
		SetOpen(false);
		GetTree().Paused = false;
	}

	/// <summary>没选就关掉。</summary>
	public void Cancel()
	{
		if (!IsOpen)
		{
			return;
		}

		Action onCancel = _onCancel;
		Close();
		onCancel?.Invoke();
	}

	/// <summary>把选项填进三张卡（打开和刷新都走这里）。</summary>
	private void FillCards(IReadOnlyList<UpgradeOption> options)
	{
		_pending.Clear();
		for (int i = 0; i < options.Count; i++)
		{
			_pending.Add(options[i]);
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
	}

	/// <summary>刷新：让调用方重新抽一批，再填回卡片。</summary>
	private void OnRefreshPressed()
	{
		if (!IsOpen || _inputGuard > 0f || _refresh == null)
		{
			return;
		}

		List<UpgradeOption> cards = _refresh();
		if (cards == null || cards.Count == 0)
		{
			return;
		}

		FillCards(cards);
		GD.Print("[UpgradePanel] 刷新了选项");
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
