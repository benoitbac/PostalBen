using Godot;
using PostalBen.Systems;

namespace PostalBen.Npc;

/// <summary>
/// A resident of Paradise Heights, going about their day and reacting to Ben.
///
/// Deliberately simple: no navmesh, no schedule, no dialogue tree. What makes a street
/// feel inhabited is that people are *moving and noticing*, and that is almost entirely
/// what this does. Schedules and queues come with the errand-obstruction work.
/// </summary>
public partial class Npc : CharacterBody3D
{
    public enum Mood
    {
        /// <summary>Walking somewhere, indifferent.</summary>
        Neutral,
        /// <summary>Ben is in their personal space. They stop and stare.</summary>
        Annoyed,
        /// <summary>Something is wrong. They back away, still watching.</summary>
        Alarmed,
        /// <summary>Running, and not looking back.</summary>
        Fleeing,
    }

    [Export] public float WalkSpeed { get; set; } = 2.1f;
    [Export] public float FleeSpeed { get; set; } = 6.4f;
    [Export] public float PersonalSpace { get; set; } = 2.4f;
    [Export] public float AlarmRange { get; set; } = 14f;

    /// <summary>Seconds of no progress before assuming we are wedged and repicking.</summary>
    private const float StuckTimeout = 1.6f;

    private const float Gravity = 24f;

    [Export] public float MaxHealth { get; set; } = 100f;

    /// <summary>Anyone this close counts as having seen it happen.</summary>
    private const float WitnessRange = 22f;

    public Mood Current { get; private set; } = Mood.Neutral;
    public float Health { get; private set; }
    public bool IsDead { get; private set; }

    private Vector3 _target;
    private float _stuckFor;
    private float _barkCooldown;
    private float _repathIn;
    private readonly float _wanderRadius = 46f;
    private ulong _seed;

    private NotorietySystem _notoriety = null!;
    private Audio.VoiceBank _voice = null!;

    private Node3D? _body;
    private Node3D? _armL;
    private Node3D? _armR;
    private Node3D? _legL;
    private Node3D? _legR;
    private float _gait;

    public override void _Ready()
    {
        CollisionLayer = 1 << 2; // npc
        CollisionMask = 1 | (1 << 1) | (1 << 2);

        _notoriety = GetNode<NotorietySystem>("/root/Notoriety");
        _voice = GetNode<Audio.VoiceBank>("/root/VoiceBank");

        _body = GetNodeOrNull<Node3D>("Body");
        _armL = _body?.GetNodeOrNull<Node3D>("ArmL");
        _armR = _body?.GetNodeOrNull<Node3D>("ArmR");
        _legL = _body?.GetNodeOrNull<Node3D>("LegL");
        _legR = _body?.GetNodeOrNull<Node3D>("LegR");

        _seed = (ulong)GetInstanceId();
        _gait = NextFloat() * Mathf.Tau; // desynchronise the crowd's footfalls
        Health = MaxHealth;
        AddToGroup("npc");
        PickNewTarget();
    }

    /// <summary>
    /// Takes a hit. Anything that reduces health also alarms the neighbours, because a
    /// wounded person shouting is exactly how a street learns something is wrong.
    /// </summary>
    public void TakeDamage(float amount, Vector3 from)
    {
        if (IsDead || amount <= 0f)
            return;

        Health = Mathf.Max(0f, Health - amount);

        var impact = GlobalPosition + Vector3.Up * 1.1f;
        World.Gore.Splatter(this, impact, (impact - from).Normalized() with { Y = 0.4f });

        if (Health <= 0f)
        {
            Die(from);
            return;
        }

        Current = Mood.Fleeing;
        if (_barkCooldown <= 0f)
        {
            _voice.Say("npc.civilian.scared", "civilian");
            _barkCooldown = 3f;
        }

        _notoriety.Report(NotorietySystem.Incident.Assault, GlobalPosition, WitnessesNearby());
    }

    private void Die(Vector3 from)
    {
        IsDead = true;

        GetNode<GameState>("/root/GameState").RecordKill();
        _notoriety.Report(NotorietySystem.Incident.Kill, GlobalPosition, WitnessesNearby());

        // Fall away from whatever hit them, and stop being a person: no AI, no
        // collision against the player, but the body stays as scenery. Ben has to walk
        // past what he did.
        var away = (GlobalPosition - from) with { Y = 0f };
        if (away.LengthSquared() < 0.01f)
            away = Vector3.Forward;

        SetPhysicsProcess(false);
        CollisionLayer = 0;
        CollisionMask = 0;
        Velocity = Vector3.Zero;

        World.Gore.Pool(this);

        if (_body is null)
            return;

        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_body, "rotation:x", Mathf.DegToRad(-84f), 0.5f)
            .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_body, "position:y", -0.15f, 0.5f);
        tween.TweenProperty(_body, "rotation:z", (NextFloat() - 0.5f) * 0.8f, 0.5f);
    }

    /// <summary>How many other residents are close enough to have seen it.</summary>
    private int WitnessesNearby()
    {
        var seen = 0;
        foreach (var node in GetTree().GetNodesInGroup("npc"))
        {
            if (node is Npc other && !other.IsDead && other != this &&
                other.GlobalPosition.DistanceTo(GlobalPosition) < WitnessRange)
            {
                seen++;
            }
        }
        return Mathf.Max(1, seen);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
            return;

        var dt = (float)delta;
        _barkCooldown -= dt;
        _repathIn -= dt;

        var player = FindBen();
        UpdateMood(player, dt);

        var velocity = Velocity;
        if (!IsOnFloor())
            velocity.Y -= Gravity * dt;

        var desired = Current switch
        {
            Mood.Fleeing when player is not null => (GlobalPosition - player.GlobalPosition),
            Mood.Alarmed when player is not null => (GlobalPosition - player.GlobalPosition) * 0.4f,
            Mood.Annoyed => Vector3.Zero,
            _ => _target - GlobalPosition,
        };

        desired.Y = 0f;

        var speed = Current == Mood.Fleeing ? FleeSpeed : WalkSpeed;
        var move = desired.LengthSquared() > 0.04f ? desired.Normalized() * speed : Vector3.Zero;

        velocity.X = Mathf.MoveToward(velocity.X, move.X, 14f * dt);
        velocity.Z = Mathf.MoveToward(velocity.Z, move.Z, 14f * dt);

        Velocity = velocity;
        MoveAndSlide();

        FaceTravel(dt);
        Animate(dt);
        UpdateWandering(dt);
    }

    /// <summary>
    /// A four-limb walk cycle driven straight off ground speed.
    ///
    /// No animation player, no imported clips - just counter-swinging limbs and a bob.
    /// It is crude, but a figure that moves its legs reads as a person and a figure that
    /// slides reads as furniture, and that difference is most of what makes a street
    /// feel alive.
    /// </summary>
    private void Animate(float dt)
    {
        if (_body is null)
            return;

        var speed = new Vector3(Velocity.X, 0f, Velocity.Z).Length();
        var stride = Mathf.Clamp(speed / WalkSpeed, 0f, 2.2f);

        _gait += dt * (4.6f + speed * 1.5f);

        var swing = Mathf.Sin(_gait) * 0.55f * stride;
        var counter = Mathf.Sin(_gait + Mathf.Pi) * 0.55f * stride;

        SetPitch(_legL, swing);
        SetPitch(_legR, counter);
        SetPitch(_armL, counter * 0.7f);
        SetPitch(_armR, swing * 0.7f);

        // Vertical bob at twice stride frequency, plus a forward lean when running.
        var bob = Mathf.Abs(Mathf.Sin(_gait)) * 0.045f * stride;
        var lean = Current == Mood.Fleeing ? 0.16f : 0.03f * stride;

        _body.Position = _body.Position with { Y = bob };
        _body.Rotation = _body.Rotation with { X = Mathf.Lerp(_body.Rotation.X, lean, 6f * dt) };
    }

    private static void SetPitch(Node3D? limb, float radians)
    {
        if (limb is not null)
            limb.Rotation = limb.Rotation with { X = radians };
    }

    // ---------------------------------------------------------------- behaviour

    private void UpdateMood(Node3D? player, float dt)
    {
        var previous = Current;

        if (player is null)
        {
            Current = Mood.Neutral;
            return;
        }

        var distance = GlobalPosition.DistanceTo(player.GlobalPosition);
        var heat = _notoriety.Current;

        Current = heat switch
        {
            NotorietySystem.Level.Hunted when distance < AlarmRange * 1.6f => Mood.Fleeing,
            NotorietySystem.Level.Reported when distance < AlarmRange => Mood.Fleeing,
            NotorietySystem.Level.Noticed when distance < AlarmRange => Mood.Alarmed,
            _ when distance < PersonalSpace => Mood.Annoyed,
            _ => Mood.Neutral,
        };

        if (Current != previous)
            OnMoodChanged(previous);
    }

    private void OnMoodChanged(Mood previous)
    {
        if (_barkCooldown > 0f)
            return;

        var key = Current switch
        {
            Mood.Annoyed => "npc.civilian.annoyed",
            Mood.Alarmed => "npc.civilian.scared",
            Mood.Fleeing => "npc.civilian.flee",
            Mood.Neutral when previous == Mood.Annoyed => "npc.civilian.greet",
            _ => null,
        };

        if (key is null)
            return;

        _voice.Say(key, "civilian");
        _barkCooldown = 4f + NextFloat() * 6f;
    }

    private void UpdateWandering(float dt)
    {
        if (Current is Mood.Fleeing or Mood.Alarmed or Mood.Annoyed)
            return;

        // Repick on arrival, on a timer, or when wedged against geometry.
        var flat = new Vector3(Velocity.X, 0f, Velocity.Z);
        _stuckFor = flat.LengthSquared() < 0.15f ? _stuckFor + dt : 0f;

        var arrived = GlobalPosition.DistanceTo(_target) < 2f;
        if (arrived || _stuckFor > StuckTimeout || _repathIn <= 0f)
            PickNewTarget();
    }

    private void PickNewTarget()
    {
        // Bias toward the road grid so people walk along streets rather than milling
        // in the middle of blocks. Roads sit on multiples of 40 in both axes.
        var alongX = NextFloat() > 0.5f;
        var lane = (Mathf.Round((NextFloat() * 4f) - 2f)) * 40f;
        var travel = (NextFloat() * 2f - 1f) * _wanderRadius;

        _target = alongX
            ? new Vector3(travel, GlobalPosition.Y, lane + (NextFloat() * 8f - 4f))
            : new Vector3(lane + (NextFloat() * 8f - 4f), GlobalPosition.Y, travel);

        _stuckFor = 0f;
        _repathIn = 8f + NextFloat() * 10f;
    }

    private void FaceTravel(float dt)
    {
        var flat = new Vector3(Velocity.X, 0f, Velocity.Z);
        if (flat.LengthSquared() < 0.05f)
            return;

        var wanted = Mathf.Atan2(-flat.X, -flat.Z);
        Rotation = Rotation with { Y = Mathf.LerpAngle(Rotation.Y, wanted, 8f * dt) };
    }

    private Node3D? FindBen() =>
        GetTree().GetFirstNodeInGroup("player") as Node3D;

    /// <summary>
    /// Deterministic per-instance randomness. Math.Random would desynchronise the crowd
    /// between runs, which makes a reported bug impossible to reproduce.
    /// </summary>
    private float NextFloat()
    {
        _seed = _seed * 6364136223846793005UL + 1442695040888963407UL;
        return ((_seed >> 33) & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
