using System.Collections.Generic;
using Godot;

/// <summary>
/// 背包界面（从商城右上角的「背包」按钮进来）。
///
/// 内容 = 商城里买到的**永久强化**（同一个效果买了几次就显示成 ×N）+ 买过的 EYE。
/// 每件都能单独卖，**每件卖 5 coin**（比买的时候便宜，相当于折价回收）。
/// 行是运行时创建出来的（道具数量不固定，场景里只摆一个空列表容器）。
/// </summary>
public partial class HomeBagMenu : Control
{
	/// <summary>"返回"回哪一层（默认回商城）。</summary>
	[Export] public string BackScenePath = ScenePaths.Shop;

	private Label _coinLabel;
	private Label _emptyLabel;
	private VBoxContainer _list;
	private Button _sellAllButton;

	public override void _Ready()
	{
		GetTree().Paused = false;

		_coinLabel = GetNodeOrNull<Label>("CoinLabel");
		_emptyLabel = GetNodeOrNull<Label>("EmptyLabel");
		_list = GetNodeOrNull<VBoxContainer>("Scroll/List");
		_sellAllButton = GetNodeOrNull<Button>("SellAllButton");

		if (GetNodeOrNull<BaseButton>("ExitButton") is BaseButton exit)
		{
			exit.Pressed += GoBack;
		}

		if (_sellAllButton != null)
		{
			_sellAllButton.Pressed += OnSellAllPressed;
		}

		if (GameState.Instance != null)
		{
			GameState.Instance.CoinsChanged += OnCoinsChanged;
			GameState.Instance.PermanentChanged += Rebuild;
		}

		Rebuild();
	}

	public override void _ExitTree()
	{
		// GameState 是 autoload，比本场景活得久，退场时一定要断开
		if (GameState.Instance != null)
		{
			GameState.Instance.CoinsChanged -= OnCoinsChanged;
			GameState.Instance.PermanentChanged -= Rebuild;
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

	private void OnCoinsChanged(int coins)
	{
		RefreshCoins();
	}

	private void RefreshCoins()
	{
		if (_coinLabel != null)
		{
			_coinLabel.Text = (GameState.Instance?.Coins ?? 0).ToString();
		}
	}

	/// <summary>重建整个列表（买/卖之后都会调）。</summary>
	private void Rebuild()
	{
		RefreshCoins();

		if (_list == null)
		{
			return;
		}

		for (int i = _list.GetChildCount() - 1; i >= 0; i--)
		{
			Node child = _list.GetChild(i);
			_list.RemoveChild(child);
			child.QueueFree();
		}

		int rows = 0;

		if (GameState.Instance != null)
		{
			foreach (KeyValuePair<UpgradeEffect, int> item in GameState.Instance.GetPermanentItems())
			{
				_list.AddChild(CreateUpgradeRow(item.Key, item.Value));
				rows++;
			}

			if (GameState.Instance.EyeUnlocked)
			{
				_list.AddChild(CreateEyeRow());
				rows++;
			}
		}

		if (_emptyLabel != null)
		{
			_emptyLabel.Visible = rows == 0;
		}

		if (_sellAllButton != null)
		{
			_sellAllButton.Disabled = rows == 0;
			_sellAllButton.Modulate = rows == 0 ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
		}
	}

	private Control CreateUpgradeRow(UpgradeEffect effect, int count)
	{
		HBoxContainer row = NewRow();
		row.AddChild(NewIcon(UpgradePool.IconFor(effect)));
		row.AddChild(NewName(count > 1
			? $"{UpgradePool.DisplayName(effect)} ×{count}"
			: UpgradePool.DisplayName(effect)));

		Button sell = NewSellButton();
		sell.Pressed += () =>
		{
			if (GameState.Instance != null && GameState.Instance.SellPermanent(effect))
			{
				Rebuild();
			}
		};
		row.AddChild(sell);

		return row;
	}

	private Control CreateEyeRow()
	{
		HBoxContainer row = NewRow();
		row.AddChild(NewIcon("res://ps/player/eye.png"));
		row.AddChild(NewName("EYE"));

		Button sell = NewSellButton();
		sell.Pressed += () =>
		{
			if (GameState.Instance != null && GameState.Instance.SellEye())
			{
				Rebuild();
			}
		};
		row.AddChild(sell);

		return row;
	}

	private static HBoxContainer NewRow()
	{
		HBoxContainer row = new()
		{
			CustomMinimumSize = new Vector2(0f, 52f),
		};
		row.AddThemeConstantOverride("separation", 12);
		return row;
	}

	private static TextureRect NewIcon(string path)
	{
		return new TextureRect
		{
			CustomMinimumSize = new Vector2(44f, 44f),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = ResourceLoader.Load<Texture2D>(path),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	private static Label NewName(string text)
	{
		Label label = new()
		{
			Text = text,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 22);
		label.AddThemeColorOverride("font_color", new Color(0.0235294f, 0.0196078f, 0.0196078f));
		return label;
	}

	private static Button NewSellButton()
	{
		Button sell = new()
		{
			Text = $"卖出 {GameState.SellPrice}",
			CustomMinimumSize = new Vector2(130f, 44f),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			FocusMode = Control.FocusModeEnum.None,
		};
		sell.AddThemeFontSizeOverride("font_size", 18);
		return sell;
	}

	/// <summary>全部卖掉：一直卖到背包空为止。</summary>
	private void OnSellAllPressed()
	{
		if (GameState.Instance == null)
		{
			return;
		}

		bool sold = true;
		while (sold)
		{
			sold = false;

			List<KeyValuePair<UpgradeEffect, int>> items = GameState.Instance.GetPermanentItems();
			if (items.Count > 0)
			{
				sold = GameState.Instance.SellPermanent(items[0].Key);
			}
			else if (GameState.Instance.EyeUnlocked)
			{
				sold = GameState.Instance.SellEye();
			}
		}

		Rebuild();
	}

	private void GoBack()
	{
		Error error = GetTree().ChangeSceneToFile(BackScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"[HomeBag] 返回失败：{error}（{BackScenePath}）");
		}
	}
}
