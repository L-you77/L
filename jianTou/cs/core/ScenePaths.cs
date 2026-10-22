using Godot;

/// <summary>
/// 场景路径集中管理：以后挪场景文件只改这里。
/// </summary>
public static class ScenePaths
{
	/// <summary>启动主页（开始 → home_2）。</summary>
	public const string Home = "res://node/scene/home.tscn";

	/// <summary>第二层主页：教学 / 主线。</summary>
	public const string Home2 = "res://node/scene/home_2.tscn";

	/// <summary>设置界面。</summary>
	public const string Settings = "res://node/scene/home_settings.tscn";

	/// <summary>触摸按钮的自由布局（设置里的「按钮布局」）。</summary>
	public const string TouchLayoutScene = "res://node/scene/home_touch_layout.tscn";

	/// <summary>成就界面。</summary>
	public const string Achievement = "res://node/scene/home_achievement.tscn";

	/// <summary>商城。</summary>
	public const string Shop = "res://node/scene/home_shop.tscn";

	/// <summary>背包（从商城进入，列出买到的永久道具，可卖出）。</summary>
	public const string Bag = "res://node/scene/home_bag.tscn";

	/// <summary>教学选关界面。</summary>
	public const string TutorialSelect = "res://node/scene/home_jiaoxue.tscn";

	/// <summary>主线选关界面。</summary>
	public const string MainSelect = "res://node/scene/home_zhuxian.tscn";

	/// <summary>主线关卡。</summary>
	public const string Game = "res://node/scene/node_1.tscn";

	/// <summary>无限跑酷（home_2 的"无限"按钮）。</summary>
	public const string Infinite = "res://node/scene/node_wuxian.tscn";

	/// <summary>主线第一关的地下区域（在 node_1 里按住"向下"钻下去进入）。</summary>
	public const string Main1_1 = "res://node/scene/node_1_1.tscn";

	/// <summary>主线第二关：升降井。</summary>
	public const string Main2 = "res://node/scene/node_1_2.tscn";

	/// <summary>主线第三关：通天塔（从底层房间沿梯子往上爬）。</summary>
	public const string Main3 = "res://node/scene/node_2_3.tscn";

	/// <summary>教学第一关：向右。</summary>
	public const string TutorialRight = "res://node/jiaoXue/node_right.tscn";

	/// <summary>教学第二关：向左。</summary>
	public const string TutorialLeft = "res://node/jiaoXue/node_left.tscn";

	/// <summary>教学第三关：向上。</summary>
	public const string TutorialUp = "res://node/jiaoXue/node_up.tscn";

	/// <summary>教学第四关：近战（真随机强化）。</summary>
	public const string TutorialNearHit = "res://node/jiaoXue/node_near_hit.tscn";

	/// <summary>教学第五关：远程（真随机强化）。</summary>
	public const string TutorialFarHit = "res://node/jiaoXue/node_far_hit.tscn";

	/// <summary>教学第六关：向下（爬梯子）。</summary>
	public const string TutorialDown = "res://node/jiaoXue/node_down.tscn";

	/// <summary>全部教学关卡（集齐通关会发一次奖励，见 GameState.RecordTutorialClear）。</summary>
	public static readonly string[] AllTutorialLevels =
	{
		TutorialRight,
		TutorialLeft,
		TutorialUp,
		TutorialNearHit,
		TutorialFarHit,
		TutorialDown,
	};

	/// <summary>
	/// 关卡里点"退出"该回哪一屏：教学关（node/jiaoXue/）→ 教学选关，
	/// 主线关（node_1 / node_1_1 ...）→ 主线选关，认不出来就回主页。
	/// </summary>
	public static string SelectSceneFor(string levelScenePath)
	{
		if (string.IsNullOrEmpty(levelScenePath))
		{
			return Home;
		}

		if (levelScenePath.Contains("/jiaoXue/"))
		{
			return TutorialSelect;
		}

		if (levelScenePath.Contains("/node_1"))
		{
			return MainSelect;
		}

		return Home;
	}

	/// <summary>当前正在运行的关卡 → 它对应的选关界面。</summary>
	public static string CurrentLevelSelect(SceneTree tree)
	{
		string path = tree?.CurrentScene?.SceneFilePath ?? string.Empty;
		return SelectSceneFor(path);
	}
}
