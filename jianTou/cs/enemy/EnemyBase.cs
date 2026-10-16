using Godot;

/// <summary>
/// 敌人公共基类：血量、受击、踩踏、死亡表现、碰撞层。
/// 具体移动方式交给子类的 <see cref="MoveBehaviour"/>。
///
/// 后续要做“敌人不可杀”，只需在敌人场景根节点上把 <see cref="CanBeKilled"/> 或
/// <see cref="CanBeStomped"/> 关掉即可，不需要改代码。
/// </summary>
public abstract partial class EnemyBase : CharacterBody2D, IDamageable
{
	[ExportGroup("生命")]

	/// <summary>最大血量。踩踏一次扣 1 点。</summary>
	[Export] public int MaxHealth = 1;

	/// <summary>能否被消灭。关掉后挨打只会闪一下，敌人不死（留作“不可杀敌人”的开关）。</summary>
	[Export] public bool CanBeKilled = true;

	/// <summary>能否被踩踏。关掉后踩踏不生效，玩家会直接撞上去受伤。</summary>
	[Export] public bool CanBeStomped = true;

	/// <summary>接触伤害值（会同步到场景里 HitBox 的 Damage）。</summary>
	[Export] public int ContactDamage = 1;

	[ExportGroup("受击表现")]

	/// <summary>受击闪红时长。</summary>
	[Export] public float HurtFlashTime = 0.12f;

	/// <summary>受击闪烁颜色。</summary>
	[Export] public Color HurtColor = new Color(1f, 0.35f, 0.35f);

	[ExportGroup("死亡表现")]

	/// <summary>死亡闪红的停留时长，闪完就销毁。设 0 表示立刻销毁（看不到红）。</summary>
	[Export] public float DeathFlashTime = 0.08f;

	/// <summary>闪红结束后是否把节点删掉（留开关给以后可能需要留尸体的场合）。</summary>
	[Export] public bool QueueFreeOnDeath = true;

	public int Health { get; private set; }
	public bool IsAlive => Health > 0;

	/// <summary>是否已经进入死亡流程（闪红 → 销毁）。</summary>
	public bool IsDying { get; private set; }

	private float _hurtFlashTimer;
	private float _deathTimer;

	/// <summary>死亡时发出，供关卡逻辑监听。</summary>
	[Signal] public delegate void DiedEventHandler();

	protected AnimatedSprite2D Sprite { get; private set; }
	protected HitBox Hit { get; private set; }
	protected HurtBox Hurt { get; private set; }
	protected CollisionShape2D BodyShape { get; private set; }

	/// <summary>碰撞盒的半宽半高。直接从场景里的 RectangleShape2D 读，改了尺寸不用改代码。</summary>
	protected Vector2 HalfExtents { get; private set; } = new Vector2(3f, 3f);

	public override void _Ready()
	{
		Sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		BodyShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		Hurt = GetNodeOrNull<HurtBox>("HurtBox");
		Hit = GetNodeOrNull<HitBox>("HitBox");

		if (BodyShape?.Shape is RectangleShape2D rect)
		{
			HalfExtents = rect.Size * 0.5f;
		}

		ApplyCollisionLayers();

		if (Hurt != null)
		{
			Hurt.Stompable = CanBeStomped;
		}
		if (Hit != null)
		{
			Hit.Damage = ContactDamage;
		}

		Health = MaxHealth;
	}

	/// <summary>碰撞层兜底，和场景里的设置一致。</summary>
	protected virtual void ApplyCollisionLayers()
	{
		CollisionLayer = GameLayers.EnemyBody;
		CollisionMask = GameLayers.WorldMask;

		if (Hurt != null)
		{
			Hurt.CollisionLayer = GameLayers.EnemyHurt;
			Hurt.CollisionMask = GameLayers.PlayerAttack;
		}

		if (Hit != null)
		{
			Hit.CollisionLayer = GameLayers.EnemyAttack;
			Hit.CollisionMask = GameLayers.PlayerHurt;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (_hurtFlashTimer > 0f)
		{
			_hurtFlashTimer = Mathf.Max(0f, _hurtFlashTimer - dt);
		}
		UpdateSpriteTint();

		if (IsDying)
		{
			// 死亡：已经定格并闪红，倒计时归零就销毁
			_deathTimer -= dt;
			if (QueueFreeOnDeath && _deathTimer <= 0f)
			{
				QueueFree();
			}
			return;
		}

		MoveBehaviour(dt);
	}

	/// <summary>活着时每物理帧的移动逻辑，子类实现。</summary>
	protected abstract void MoveBehaviour(float delta);

	/// <summary>受击。由 HurtBox 自动调用。</summary>
	public void TakeDamage(int amount, HitBox source)
	{
		if (!IsAlive || amount <= 0)
		{
			return;
		}

		if (!CanBeKilled)
		{
			PlayHurtFlash();
			GD.Print($"[{Name}] 免疫本次伤害（CanBeKilled = false）");
			return;
		}

		Health = Mathf.Max(0, Health - amount);
		if (Health <= 0)
		{
			Die();
		}
		else
		{
			PlayHurtFlash();
		}
	}

	/// <summary>受击闪红（把贴图染成 <see cref="HurtColor"/>，持续 <see cref="HurtFlashTime"/>）。</summary>
	protected void PlayHurtFlash()
	{
		if (Sprite == null)
		{
			return;
		}

		_hurtFlashTimer = HurtFlashTime;
		UpdateSpriteTint();
	}

	/// <summary>每帧刷新贴图染色：死亡/受击为红，其余为白。</summary>
	private void UpdateSpriteTint()
	{
		if (Sprite == null)
		{
			return;
		}

		if (!IsAlive || _hurtFlashTimer > 0f)
		{
			Sprite.Modulate = HurtColor;
		}
		else if (Sprite.Modulate != Colors.White)
		{
			Sprite.Modulate = Colors.White;
		}
	}

	/// <summary>播放指定动画（相同动画不重复播放）。</summary>
	protected void PlayAnimation(StringName animation)
	{
		if (Sprite != null && Sprite.Animation != animation)
		{
			Sprite.Play(animation);
		}
	}

	protected virtual void Die()
	{
		// 注意：不能拿 IsAlive 当"是否已经死过"的判断——受击时血量已经先减到 0 了，
		// 这里必须用独立标记，否则会直接 return，闪红和销毁都不会执行。
		if (IsDying)
		{
			return;
		}

		IsDying = true;
		Health = 0;

		// 关掉碰撞：死后不再挡路、不再伤人、不再被打
		if (BodyShape != null)
		{
			BodyShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}
		if (Hit != null)
		{
			Hit.SetDeferred(Area2D.PropertyName.Monitoring, false);
		}
		if (Hurt != null)
		{
			Hurt.SetDeferred(Area2D.PropertyName.Monitorable, false);
		}

		Velocity = Vector2.Zero; // 死亡后定格，不再移动
		UpdateSpriteTint();      // 立刻变红，不再等下一帧
		_deathTimer = DeathFlashTime;

		EmitSignal(SignalName.Died);
		GD.Print($"[{Name}] 被消灭");
	}
}
