using System.Collections.Generic;
using Godot;

/// <summary>
/// 全局音频管理（autoload，node/core/audio_manager.tscn）。
///
///   · 音效：8 个 AudioStreamPlayer 轮转播放（连击时不会互相打断），文件在 audio/sfx/；
///   · BGM：一个播放器 + 淡入淡出切歌，文件在 audio/bgm/，按当前场景自动选曲；
///   · 事件接线：靠 SceneTree.NodeAdded 自动挂——玩家/金币/敌人/机关/通关点/强化面板/按钮
///     一旦进树就接上对应音效，不用在每个场景里手动配。
///
/// 分两条总线（Music / SFX），音量各自可调；暂停时（ProcessMode = Always）BGM 继续播。
/// </summary>
public partial class AudioManager : Node
{
	public static AudioManager Instance { get; private set; }

	[ExportGroup("音量")]
	/// <summary>BGM 音量（dB）。0 = 原始音量，越小越轻。</summary>
	[Export] public float MusicVolumeDb = -8f;

	/// <summary>音效音量（dB）。</summary>
	[Export] public float SfxVolumeDb = -3f;

	[ExportGroup("播放")]
	/// <summary>同时能响几个音效。</summary>
	[Export] public int SfxVoices = 8;

	/// <summary>切歌淡入淡出时长（秒）。</summary>
	[Export] public float MusicFadeTime = 0.7f;

	private const string SfxDir = "res://audio/sfx/";
	private const string BgmMenu = "res://audio/bgm/menu.wav";
	private const string BgmLevel = "res://audio/bgm/level.wav";
	private const string BgmRunner = "res://audio/bgm/runner.wav";
	private const string HookedMeta = "sfx_hooked";

	private readonly Dictionary<string, AudioStream> _sfxCache = new();
	private readonly List<AudioStreamPlayer> _voices = new();

	private AudioStreamPlayer _music;
	private Tween _fade;
	private int _nextVoice;
	private string _currentPath = string.Empty;
	private string _currentScenePath = string.Empty;

	public override void _Ready()
	{
		Instance = this;

		// 暂停时 BGM 和音效都要继续
		ProcessMode = ProcessModeEnum.Always;

		_music = new AudioStreamPlayer
		{
			Bus = "Music",
			ProcessMode = ProcessModeEnum.Always,
		};
		AddChild(_music);

		for (int i = 0; i < SfxVoices; i++)
		{
			AudioStreamPlayer voice = new()
			{
				Bus = "SFX",
				ProcessMode = ProcessModeEnum.Always,
			};
			AddChild(voice);
			_voices.Add(voice);
		}

		ApplyBusVolumes();
		GetTree().NodeAdded += OnNodeAdded;
	}

	public override void _Process(double delta)
	{
		UpdateMusicForScene();
	}

	// ------------------------------------------------------------------
	// 对外接口
	// ------------------------------------------------------------------

	/// <summary>播一个音效（名字就是 audio/sfx/ 下的文件名，不带扩展名）。</summary>
	public void PlaySfx(string name, float volumeDb = 0f)
	{
		if (string.IsNullOrEmpty(name) || _voices.Count == 0)
		{
			return;
		}

		AudioStream stream = LoadSfx(name);
		if (stream == null)
		{
			return;
		}

		AudioStreamPlayer voice = _voices[_nextVoice];
		_nextVoice = (_nextVoice + 1) % _voices.Count;

		voice.Stop();
		voice.Stream = stream;
		voice.VolumeDb = volumeDb;
		voice.Play();
	}

	/// <summary>切 BGM（同一个文件不会重头开始）。</summary>
	public void PlayMusic(string path)
	{
		if (string.IsNullOrEmpty(path) || path == _currentPath)
		{
			return;
		}

		AudioStream stream = ResourceLoader.Load<AudioStream>(path);
		if (stream == null)
		{
			GD.PushWarning($"[AudioManager] BGM 加载失败：{path}");
			return;
		}

		MakeLoop(stream);
		_currentPath = path;

		_fade?.Kill();

		if (_music.Playing)
		{
			_fade = CreateTween();
			_fade.TweenProperty(_music, "volume_db", -40f, MusicFadeTime * 0.5f);
			_fade.TweenCallback(Callable.From(() => StartMusic(stream)));
			_fade.TweenProperty(_music, "volume_db", 0f, MusicFadeTime * 0.5f);
			return;
		}

		StartMusic(stream);
	}

	/// <summary>停掉 BGM（淡出）。</summary>
	public void StopMusic()
	{
		_currentPath = string.Empty;
		_fade?.Kill();
		_fade = CreateTween();
		_fade.TweenProperty(_music, "volume_db", -40f, MusicFadeTime * 0.5f);
		_fade.TweenCallback(Callable.From(() => _music.Stop()));
	}

	/// <summary>设置 BGM 音量（dB），会立即生效。</summary>
	public void SetMusicVolumeDb(float db)
	{
		MusicVolumeDb = db;
		ApplyBusVolumes();
	}

	/// <summary>设置音效音量（dB）。</summary>
	public void SetSfxVolumeDb(float db)
	{
		SfxVolumeDb = db;
		ApplyBusVolumes();
	}

	// ------------------------------------------------------------------
	// 内部
	// ------------------------------------------------------------------

	private void StartMusic(AudioStream stream)
	{
		// 音量统一由 Music 总线控制，播放器本身保持 0dB（否则会叠两次衰减）
		_music.Stream = stream;
		_music.VolumeDb = 0f;
		_music.Play();
	}

	private void ApplyBusVolumes()
	{
		int musicBus = AudioServer.GetBusIndex("Music");
		if (musicBus >= 0)
		{
			AudioServer.SetBusVolumeDb(musicBus, MusicVolumeDb);
		}

		int sfxBus = AudioServer.GetBusIndex("SFX");
		if (sfxBus >= 0)
		{
			AudioServer.SetBusVolumeDb(sfxBus, SfxVolumeDb);
		}
	}

	/// <summary>wav 的循环要在运行时打开（导入设置里默认是关的）。</summary>
	private static void MakeLoop(AudioStream stream)
	{
		if (stream is not AudioStreamWav wav)
		{
			return;
		}

		int frames = Mathf.RoundToInt(wav.GetLength() * wav.MixRate);
		wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
		wav.LoopBegin = 0;
		wav.LoopEnd = frames;
	}

	private AudioStream LoadSfx(string name)
	{
		if (_sfxCache.TryGetValue(name, out AudioStream cached))
		{
			return cached;
		}

		AudioStream stream = ResourceLoader.Load<AudioStream>($"{SfxDir}{name}.wav");
		if (stream == null)
		{
			GD.PushWarning($"[AudioManager] 音效不存在：{name}");
		}

		_sfxCache[name] = stream;
		return stream;
	}

	/// <summary>按当前场景决定放哪首 BGM。</summary>
	private void UpdateMusicForScene()
	{
		string path = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		if (path == _currentScenePath)
		{
			return;
		}

		_currentScenePath = path;
		PlayMusic(BgmForScene(path));
	}

	private static string BgmForScene(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return BgmMenu;
		}

		if (path.Contains("node_wuxian"))
		{
			return BgmRunner;
		}

		if (path.Contains("/jiaoXue/") || path.Contains("node_1") || path.Contains("node_2_3"))
		{
			return BgmLevel;
		}

		return BgmMenu;
	}

	// ------------------------------------------------------------------
	// 自动接线
	// ------------------------------------------------------------------

	private void OnNodeAdded(Node node)
	{
		switch (node)
		{
			case Player player when MarkOnce(node):
				player.Jumped += () => PlaySfx("jump", -5f);
				player.Landed += () => PlaySfx("land", -9f);
				player.Died += () => PlaySfx("death");
				break;

			case Coin coin when MarkOnce(node):
				coin.Collected += (_, _) => PlaySfx("coin", -4f);
				break;

			case CoinSpe spe when MarkOnce(node):
				spe.Collected += _ => PlaySfx("coin_spe", -2f);
				break;

			case EnemyBase enemy when MarkOnce(node):
				enemy.Died += () => PlaySfx("kill", -3f);
				break;

			case SwitchGate gate when MarkOnce(node):
				gate.Activated += () => PlaySfx("switch");
				break;

			case TongGuan goal when MarkOnce(node):
				goal.Cleared += () => PlaySfx("clear");
				break;

			case UpgradePanel panel when MarkOnce(node):
				panel.Chosen += _ => PlaySfx("upgrade", -2f);
				break;

			case FarHitBullet bullet when MarkOnce(node):
				// 玩家弹体进树 = 开火
				PlaySfx("shoot", -6f);
				break;

			case HurtBox hurt when hurt.GetParent() is Player && MarkOnce(node):
				hurt.Hurt += (_, _) => PlaySfx("hurt", -2f);
				break;

			case BaseButton button when MarkOnce(node):
				// 三选一的卡片自己已经有 upgrade 音，别再叠一个"嗒"
				if (!button.Name.ToString().StartsWith("Card"))
				{
					button.Pressed += () => PlaySfx("ui", -8f);
				}
				break;
		}
	}

	/// <summary>同一次生命周期里只挂一次（节点可能被重复加入树）。</summary>
	private static bool MarkOnce(Node node)
	{
		if (node.HasMeta(HookedMeta))
		{
			return false;
		}

		node.SetMeta(HookedMeta, true);
		return true;
	}
}
