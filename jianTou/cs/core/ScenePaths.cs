/// <summary>
/// 场景路径集中管理：以后挪场景文件只改这里。
/// </summary>
public static class ScenePaths
{
	/// <summary>启动主页（开始 → home_2）。</summary>
	public const string Home = "res://node/scene/home.tscn";

	/// <summary>第二层主页：教学 / 主线。</summary>
	public const string Home2 = "res://node/scene/home_2.tscn";

	/// <summary>教学选关界面。</summary>
	public const string TutorialSelect = "res://node/scene/home_jiaoxue.tscn";

	/// <summary>主线选关界面。</summary>
	public const string MainSelect = "res://node/scene/home_zhuxian.tscn";

	/// <summary>主线关卡。</summary>
	public const string Game = "res://node/scene/node_1.tscn";

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
}
