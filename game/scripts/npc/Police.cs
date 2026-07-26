using Godot;
using PostalBen.Systems;

namespace PostalBen.Npc;

/// <summary>
/// A responding officer.
///
/// Behaves in three beats, matching the notoriety level that summoned them: converge on
/// the last known position, search, then engage once Ben is actually seen. They warn
/// before they shoot, because a player who has done nothing since the incident should be
/// able to talk their way down - decay is a real strategy and police that open fire on
/// sight would kill it.
/// </summary>
public partial class Police : CharacterBody3D
{
    private enum Beat { Converging, Searching, Engaging }

    [Export] public float PatrolSpeed { get; set; } = 3.4f;
    [Export] public float ChaseSpeed { get; set; } = 6.2f;
    [Export] public float SightRange { get; set; } = 32f;
    [Export] public float FireRange { get; set; } = 24f;
    [Export] public float Damage { get; set; } = 16f;
    [Export] public float FireInterval { get; set; } = 0.9f;
    [Export] public float MaxHealth { get; set; } = 130f;

    /// <summary>Seconds of visual contact before they stop warning and start shooting.</summary>
    private const float WarningGrace = 1.8f;

    private const float Gravity = 24f;

    public bool IsDead { get; private set; }
    public float Health { get; private set; }

    private Beat _beat = Beat.Converging;
    private Vector3 _destination;
    private float _fireCooldown;
    private float _contactFor;
    private float _barkCooldown;
    private float _gait;
    private ulong _seed;

    private NotorietySystem _notoriety = null!;
    private Audio.VoiceBank _voice = null!;
    private Node3D? _body;
    private Node3D? _armL;
    private Node3D? _armR;
    private Node3D? _legL;
    private Node3D? _legR;

    public override void _Ready()
    {
        CollisionLayer = 1 << 3; // police
        CollisionMask = 1 | (1 << 1) | (1 << 2) | (1 << 3);

        _notoriety = GetNode<NotorietySystem>("/root/Notoriety");
        _voice = GetNode<Audio.VoiceBank>("/root/VoiceBank");
        _seed = (ulong)GetInstanceId();
        Health = MaxHealth;

        _body = GetNodeOrNull<Node3D>("Body");
        _armL = _body?.GetNodeOrNull<Node3D>("ArmL");
        _armR = _body?.GetNodeOrNull<Node3D>("ArmR");
        _legL = _body?.GetNodeOrNull<Node3D>("LegL");
        _legR = _body?.GetNodeOrNull<Node3D>("LegR");

        AddToGroup("police");

        _destination = _notoriety.HasLastKnownPosition
            ? _notoriety.LastKnownPosition
            : GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
            return;

        var dt = (float)delta;
        _fireCooldown -= dt;
        _barkCooldown -= dt;

        var ben = GetTree().GetFirstNodeInGroup("player") as Player.BenController;
        UpdateBeat(ben, dt);

        var velocity = Velocity;
        if (!IsOnFloor())
            velocity.Y -= Gravity * dt;

        var goal = _beat == Beat.Engaging && ben is not null ? ben.GlobalPosition : _destination;
        var toGoal = (goal - GlobalPosition) with { Y = 0f };

        // Hold position at knife-edge range rather than walking into the player.
        var standOff = _beat == Beat.Engaging ? 6f : 1.5f;
        var move = toGoal.Length() > standOff
            ? toGoal.Normalized() * (_beat == Beat.Engaging ? ChaseSpeed : PatrolSpeed)
            : Vector3.Zero;

        velocity.X = Mathf.MoveToward(velocity.X, move.X, 18f * dt);
        velocity.Z = Mathf.MoveToward(velocity.Z, move.Z, 18f * dt);

        Velocity = velocity;
        MoveAndSlide();

        Face(_beat == Beat.Engaging && ben is not null ? ben.GlobalPosition : goal, dt);
        Animate(dt);
    }

    // ---------------------------------------------------------------- behaviour

    private void UpdateBeat(Node3D? ben, float dt)
    {
        if (ben is null)
        {
            _beat = Beat.Searching;
            return;
        }

        var distance = GlobalPosition.DistanceTo(ben.GlobalPosition);
        var visible = distance < SightRange && HasLineOfSight(ben);

        if (visible)
        {
            _contactFor += dt;

            if (_beat != Beat.Engaging)
            {
                _beat = Beat.Engaging;
                Bark("npc.police.halt");
            }

            // Warn first. Only shoot once they have had eyes on for a moment and the
            // situation is genuinely a manhunt.
            if (_contactFor > WarningGrace && _notoriety.Current >= NotorietySystem.Level.Hunted
                && distance < FireRange && _fireCooldown <= 0f)
            {
                Fire(ben);
            }
            else if (_contactFor <= WarningGrace && _barkCooldown <= 0f)
            {
                Bark("npc.police.warning");
            }

            return;
        }

        _contactFor = 0f;

        if (_beat == Beat.Engaging)
        {
            // Lost him. Head for where he was last seen and sweep from there.
            _destination = _notoriety.HasLastKnownPosition
                ? _notoriety.LastKnownPosition
                : ben.GlobalPosition;
            _beat = Beat.Searching;
            Bark("npc.police.pursue");
            return;
        }

        if (GlobalPosition.DistanceTo(_destination) < 3f)
        {
            _destination = _destination + new Vector3(
                (NextFloat() * 2f - 1f) * 18f, 0f, (NextFloat() * 2f - 1f) * 18f);
        }
    }

    private void Fire(Node3D ben)
    {
        _fireCooldown = FireInterval;

        if (ben is Player.BenController controller)
            controller.TakeDamage(Damage);
    }

    private bool HasLineOfSight(Node3D target)
    {
        var space = GetWorld3D().DirectSpaceState;
        var from = GlobalPosition + Vector3.Up * 1.5f;
        var to = target.GlobalPosition + Vector3.Up * 1.2f;

        var query = PhysicsRayQueryParameters3D.Create(from, to, collisionMask: 1);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        return space.IntersectRay(query).Count == 0;
    }

    public void TakeDamage(float amount, Vector3 from)
    {
        if (IsDead || amount <= 0f)
            return;

        Health = Mathf.Max(0f, Health - amount);
        if (Health > 0f)
            return;

        IsDead = true;
        GetNode<GameState>("/root/GameState").RecordKill();
        _notoriety.Report(NotorietySystem.Incident.Kill, GlobalPosition, 4);

        SetPhysicsProcess(false);
        CollisionLayer = 0;
        CollisionMask = 0;

        if (_body is null)
            return;

        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_body, "rotation:x", Mathf.DegToRad(-84f), 0.5f);
        tween.TweenProperty(_body, "position:y", -0.15f, 0.5f);
    }

    private void Bark(string key)
    {
        _voice.Say(key, "police");
        _barkCooldown = 3.5f + NextFloat() * 2f;
    }

    private void Face(Vector3 target, float dt)
    {
        var flat = (target - GlobalPosition) with { Y = 0f };
        if (flat.LengthSquared() < 0.05f)
            return;

        var wanted = Mathf.Atan2(-flat.X, -flat.Z);
        Rotation = Rotation with { Y = Mathf.LerpAngle(Rotation.Y, wanted, 9f * dt) };
    }

    private void Animate(float dt)
    {
        if (_body is null)
            return;

        var speed = new Vector3(Velocity.X, 0f, Velocity.Z).Length();
        var stride = Mathf.Clamp(speed / PatrolSpeed, 0f, 2f);
        _gait += dt * (5f + speed * 1.4f);

        var swing = Mathf.Sin(_gait) * 0.5f * stride;
        var counter = Mathf.Sin(_gait + Mathf.Pi) * 0.5f * stride;

        SetPitch(_legL, swing);
        SetPitch(_legR, counter);

        // Arms stay forward while engaging - they are holding a weapon on him.
        if (_beat == Beat.Engaging)
        {
            SetPitch(_armL, -1.35f);
            SetPitch(_armR, -1.35f);
        }
        else
        {
            SetPitch(_armL, counter * 0.6f);
            SetPitch(_armR, swing * 0.6f);
        }

        _body.Position = _body.Position with { Y = Mathf.Abs(Mathf.Sin(_gait)) * 0.04f * stride };
    }

    private static void SetPitch(Node3D? limb, float radians)
    {
        if (limb is not null)
            limb.Rotation = limb.Rotation with { X = radians };
    }

    private float NextFloat()
    {
        _seed = _seed * 6364136223846793005UL + 1442695040888963407UL;
        return ((_seed >> 33) & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
