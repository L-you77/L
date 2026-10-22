using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// 触摸按钮的自由布局（设置里的「按钮布局」页用，autoload）。
///
///   · 存的是每个按钮**中心的归一化屏幕坐标**（0~1）+ 缩放，
///     所以换分辨率 / 改窗口大小都不会跑位；
///   · 数据存在 <c>user://jiantou_touch.cfg</c>，关掉游戏再开还在；
///   · <see cref="IsCustom"/> 为 false（玩家没调过）时，TutorialUi 完全不动场景里的原始布局 ——
///     这样默认手感永远是场景作者摆的那个，不会因为存档被改坏。
///
/// 默认布局是运行时从 <see cref="ButtonScenePath"/> 实例化一份量出来的（见 <see cref="EnsureDefaultsAsync"/>），
/// 所以改了 tutorial_ui.tscn 的按钮位置，这里的"重置为默认"也会跟着变。
/// </summary>
public partial class TouchLayout : Node
{
	/// <summary>全局唯一实例（autoload）。</summary>
	public static TouchLayout Instance { get; private set; }

	/// <summary>按钮模板场景（默认布局和贴图都从这里量）。</summary>
	public const string ButtonScenePath = "res://node/ui/tutorial_ui.tscn";

	private const string SavePath = "user://jiantou_touch.cfg";
	private const string SectionMeta = "layout";
	private const string KeyCustom = "custom";

	/// <summary>按钮缩放下限 / 上限（设置页滑条也用这两个值）。</summary>
	public const float MinScale = 0.5f;
	public const float MaxScale = 2.0f;

	/// <summary>场景里原始布局的缩放（用于"重置为默认"的显示）。</summary>
	public const float DefaultScale = 0.9f;

	/// <summary>单个按钮的布局数据。</summary>
	public class Entry
	{
		/// <summary>按钮中心的归一化屏幕坐标（0~1）。</summary>
		public Vector2 Norm;

		/// <summary>缩放（1 = 贴图原尺寸）。</summary>
		public float Scale = DefaultScale;

		public Entry Clone() => new() { Norm = Norm, Scale = Scale };
	}

	/// <summary>玩家是否自定义过布局。</summary>
	public bool IsCustom { get; private set; }

	/// <summary>玩家自定义的布局（按钮名 → 数据）。</summary>
	public IReadOnlyDictionary<string, Entry> Items => _items;

	/// <summary>测量默认布局时给临时实例打的标记（TutorialUi 见到它就不套自定义布局）。</summary>
	public const string ProbeMeta = "touch_layout_probe";

	/// <summary>场景原始布局（测量一次后缓存）。</summary>
	public static IReadOnlyDictionary<string, Entry> Defaults => _defaults;

	private readonly Dictionary<string, Entry> _items = new();
	private static Dictionary<string, Entry> _defaults;
	private static readonly Dictionary<string, Texture2D> Textures = new();

	private bool _dirty;
	private float _saveTimer;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		Load();
	}

	// ------------------------------------------------------------------
	// 应用 / 读写
	// ------------------------------------------------------------------

	/// <summary>把玩家自定义的布局套到一份 tutorial_ui 实例上（没自定义过就什么都不做）。</summary>
	public void ApplyTo(CanvasLayer ui)
	{
		if (!IsCustom || _items.Count == 0 || ui == null)
		{
			return;
		}

		Vector2 viewport = ui.GetViewport().GetVisibleRect().Size;
		if (viewport.X <= 0f || viewport.Y <= 0f)
		{
			return;
		}

		foreach (Node node in ui.FindChildren("*", "TouchScreenButton", true, false))
		{
			if (node is not TouchScreenButton button)
			{
				continue;
			}

			string name = button.Name.ToString();
			if (!_items.TryGetValue(name, out Entry entry))
			{
				continue;
			}

			Vector2 size = TextureSize(button) * entry.Scale;
			Vector2 position = entry.Norm * viewport - size * 0.5f;

			// 再夹一次：按钮放大以后中心点合法也可能有一半露到屏幕外
			position.X = Mathf.Clamp(position.X, 0f, Mathf.Max(0f, viewport.X - size.X));
			position.Y = Mathf.Clamp(position.Y, 0f, Mathf.Max(0f, viewport.Y - size.Y));

			button.Scale = Vector2.One * entry.Scale;
			button.GlobalPosition = position;
		}
	}

	/// <summary>取某个按钮的当前布局（没自定义过就是场景默认值）。</summary>
	public Entry Get(string name)
	{
		if (_items.TryGetValue(name, out Entry entry))
		{
			return entry.Clone();
		}

		if (_defaults != null && _defaults.TryGetValue(name, out Entry fallback))
		{
			return fallback.Clone();
		}

		return new Entry { Norm = new Vector2(0.5f, 0.87f), Scale = DefaultScale };
	}

	/// <summary>改一个按钮的位置/大小（自动标记为"自定义"并延时存盘）。</summary>
	public void Set(string name, Vector2 norm, float scale)
	{
		if (string.IsNullOrEmpty(name))
		{
			return;
		}

		_items[name] = new Entry
		{
			Norm = new Vector2(
				Mathf.Clamp(norm.X, 0.02f, 0.98f),
				Mathf.Clamp(norm.Y, 0.02f, 0.98f)),
			Scale = Mathf.Clamp(scale, MinScale, MaxScale),
		};

		IsCustom = true;
		MarkDirty();
	}

	/// <summary>把还没写盘的改动立刻存下去（离开布局页时调用）。</summary>
	public void Flush()
	{
		if (!_dirty)
		{
			return;
		}

		_dirty = false;
		Save();
	}

	/// <summary>恢复成场景里的原始布局。</summary>
	public void ResetAll()
	{
		_items.Clear();
		IsCustom = false;
		MarkDirty();
		GD.Print("[TouchLayout] 已恢复默认按钮布局");
	}

	/// <summary>取按钮贴图（设置页画预览用）。</summary>
	public static Texture2D TextureFor(string name)
	{
		return Textures.TryGetValue(name, out Texture2D texture) ? texture : null;
	}

	/// <summary>TouchScreenButton 的贴图尺寸（拿不到就给个 100×100 兜底）。</summary>
	public static Vector2 TextureSize(TouchScreenButton button)
	{
		Texture2D texture = button.TextureNormal;
		return texture?.GetSize() ?? new Vector2(100f, 100f);
	}

	// ------------------------------------------------------------------
	// 默认布局测量
	// ------------------------------------------------------------------

	/// <summary>
	/// 实例化一份 tutorial_ui.tscn 摆一帧，量出每个按钮的默认中心坐标和贴图。
	/// 只在第一次调用时做事，之后直接用缓存。
	/// </summary>
	public static async Task EnsureDefaultsAsync(Node host)
	{
		if (_defaults != null)
		{
			return;
		}

		if (host == null || !host.IsInsideTree())
		{
			return;
		}

		PackedScene scene = ResourceLoader.Load<PackedScene>(ButtonScenePath);
		if (scene == null)
		{
			GD.PushError($"[TouchLayout] 加载按钮场景失败：{ButtonScenePath}");
			return;
		}

		CanvasLayer probe = scene.Instantiate<CanvasLayer>();
		probe.SetMeta(ProbeMeta, true);

		// 调成全透明：既能正常解析 anchors/offset，又不会在画面上闪一下
		if (probe.GetNodeOrNull<Control>("Root") is Control root)
		{
			root.Modulate = new Color(1f, 1f, 1f, 0f);
		}

		host.AddChild(probe);

		// 等一帧让 Control 的布局解析完
		await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

		if (!GodotObject.IsInstanceValid(probe))
		{
			return;
		}

		Vector2 viewport = probe.GetViewport().GetVisibleRect().Size;
		Dictionary<string, Entry> map = new();

		if (viewport.X > 0f && viewport.Y > 0f)
		{
			foreach (Node node in probe.FindChildren("*", "TouchScreenButton", true, false))
			{
				if (node is not TouchScreenButton button)
				{
					continue;
				}

				Vector2 size = TextureSize(button) * button.Scale.X;
				Vector2 center = button.GlobalPosition + size * 0.5f;

				map[button.Name.ToString()] = new Entry
				{
					Norm = center / viewport,
					Scale = button.Scale.X,
				};

				if (button.TextureNormal != null)
				{
					Textures[button.Name.ToString()] = button.TextureNormal;
				}
			}
		}

		probe.QueueFree();

		if (map.Count > 0)
		{
			_defaults = map;
		}
	}

	// ------------------------------------------------------------------
	// 存盘（拖动时会连着改很多次，所以延时写）
	// ------------------------------------------------------------------

	private void MarkDirty()
	{
		_dirty = true;
		_saveTimer = 0.3f;
	}

	public override void _Process(double delta)
	{
		if (!_dirty)
		{
			return;
		}

		_saveTimer -= (float)delta;
		if (_saveTimer <= 0f)
		{
			_dirty = false;
			Save();
		}
	}

	private void Save()
	{
		ConfigFile config = new();
		config.SetValue(SectionMeta, KeyCustom, IsCustom);

		foreach (KeyValuePair<string, Entry> pair in _items)
		{
			config.SetValue(pair.Key, "norm", pair.Value.Norm);
			config.SetValue(pair.Key, "scale", pair.Value.Scale);
		}

		Error error = config.Save(SavePath);
		if (error != Error.Ok)
		{
			GD.PushWarning($"[TouchLayout] 保存布局失败：{error}");
		}
	}

	private void Load()
	{
		ConfigFile config = new();
		if (config.Load(SavePath) != Error.Ok)
		{
			return; // 第一次玩，没有存档
		}

		IsCustom = config.GetValue(SectionMeta, KeyCustom, false).AsBool();
		if (!IsCustom)
		{
			return;
		}

		foreach (string name in config.GetSections())
		{
			if (name == SectionMeta)
			{
				continue;
			}

			if (!config.HasSectionKey(name, "norm"))
			{
				continue;
			}

			_items[name] = new Entry
			{
				Norm = config.GetValue(name, "norm", new Vector2(0.5f, 0.87f)).AsVector2(),
				Scale = Mathf.Clamp((float)config.GetValue(name, "scale", DefaultScale).AsDouble(), MinScale, MaxScale),
			};
		}
	}
}
