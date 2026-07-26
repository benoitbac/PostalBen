using Godot;
using PostalBen.Systems;

namespace PostalBen.Player;

/// <summary>
/// Ben. First-person, physics-driven, deliberately a little heavy - he is a man doing
/// errands, not a soldier. Sprint is limited by stamina so chases have a shape.
/// </summary>
public partial class BenController : CharacterBody3D
{
    [ExportGroup("Movement")]
    [Export] public float WalkSpeed { get; set; } = 4.2f;
    [Export] public float SprintSpeed { get; set; } = 7.4f;
    [Export] public float CrouchSpeed { get; set; } = 2.1f;
    [Export] public float Acceleration { get; set; } = 12f;
    [Export] public float AirControl { get; set; } = 2.5f;
    [Export] public float JumpVelocity { get; set; } = 7.2f;

    [ExportGroup("Look")]
    [Export] public float MouseSensitivity { get; set; } = 0.0025f;
    [Export] public bool InvertY { get; set; }

    [ExportGroup("Stamina")]
    [Export] public float MaxStamina { get; set; } = 100f;
    [Export] public float SprintDrain { get; set; } = 22f;
    [Export] public float StaminaRegen { get; set; } = 14f;

    /// <summary>Stamina must recover past this before sprinting is allowed again.</summary>
    private const float SprintUnlockThreshold = 18f;

    [ExportGroup("Health")]
    [Export] public float MaxHealth { get; set; } = 100f;

    [Signal]
    public delegate void HealthChangedEventHandler(float current, float max);

    [Signal]
    public delegate void StaminaChangedEventHandler(float current, float max);

    [Signal]
    public delegate void DiedEventHandler();

    public float Health { get; private set; }
    public float Stamina { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsCrouching { get; private set; }

    /// <summary>Taken into custody. Movement is over for the day.</summary>
    public bool IsDetained { get; private set; }

    /// <summary>
    /// Time since Ben last swung or fired. Police read this to tell the difference
    /// between someone resisting and someone who has stopped.
    /// </summary>
    public float SecondsSinceAttack { get; private set; } = 999f;

    public void NotifyAttacked() => SecondsSinceAttack = 0f;

    public void Detain()
    {
        if (IsDetained)
            return;

        IsDetained = true;
        Velocity = Vector3.Zero;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private Node3D _head = null!;
    private Camera3D _camera = null!;
    private CollisionShape3D _collider = null!;
    private bool _sprintLocked;
    private float _standHeight;

    /// <summary>Mouse-motion events discarded on startup. See _UnhandledInput.</summary>
    private int _settleFrames = 3;

    private const float CrouchHeight = 1.1f;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _collider = GetNode<CollisionShape3D>("CollisionShape3D");

        _standHeight = _collider.Shape is CapsuleShape3D capsule ? capsule.Height : 1.8f;

        Health = MaxHealth;
        Stamina = MaxStamina;

        Input.MouseMode = Input.MouseModeEnum.Captured;
        LoadSettings();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsDead)
            return;

        // The first motion event after the window grabs the cursor carries the whole
        // distance from wherever the pointer happened to be, which snaps the view to a
        // random direction on startup. Swallow input until the cursor has settled.
        if (_settleFrames > 0)
        {
            if (@event is InputEventMouseMotion)
                _settleFrames--;
            return;
        }

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * MouseSensitivity);

            var pitch = motion.Relative.Y * MouseSensitivity * (InvertY ? 1f : -1f);
            _head.Rotation = _head.Rotation with
            {
                X = Mathf.Clamp(_head.Rotation.X + pitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f)),
            };
        }

        if (@event.IsActionPressed("pause"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        SecondsSinceAttack += (float)delta;

        if (IsDead || IsDetained)
            return;

        var dt = (float)delta;
        var velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= (float)ProjectSettings.GetSetting("physics/3d/default_gravity") * dt;
        else if (Input.IsActionJustPressed("jump"))
            velocity.Y = JumpVelocity;

        UpdateCrouch();

        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var direction = (Transform.Basis * new Vector3(input.X, 0f, input.Y)).Normalized();

        var wantsSprint = Input.IsActionPressed("sprint") && !IsCrouching && input.Y < 0f;
        var sprinting = wantsSprint && !_sprintLocked && Stamina > 0f;
        UpdateStamina(sprinting, dt);

        var speed = IsCrouching ? CrouchSpeed : sprinting ? SprintSpeed : WalkSpeed;
        var accel = (IsOnFloor() ? Acceleration : AirControl) * dt;

        var target = direction * speed;
        velocity.X = Mathf.MoveToward(velocity.X, target.X, accel * speed);
        velocity.Z = Mathf.MoveToward(velocity.Z, target.Z, accel * speed);

        Velocity = velocity;
        MoveAndSlide();
    }

    private void UpdateCrouch()
    {
        var wantsCrouch = Input.IsActionPressed("crouch");

        // Refuse to stand up under a low ceiling rather than clipping through it.
        if (IsCrouching && !wantsCrouch && IsBlockedAbove())
            return;

        if (wantsCrouch == IsCrouching)
            return;

        IsCrouching = wantsCrouch;
        if (_collider.Shape is CapsuleShape3D capsule)
            capsule.Height = IsCrouching ? CrouchHeight : _standHeight;
        _head.Position = _head.Position with { Y = IsCrouching ? 0.7f : 1.6f };
    }

    private bool IsBlockedAbove()
    {
        var space = GetWorld3D().DirectSpaceState;
        var from = GlobalPosition + Vector3.Up * CrouchHeight;
        var to = GlobalPosition + Vector3.Up * _standHeight;
        var query = PhysicsRayQueryParameters3D.Create(from, to, collisionMask: 1);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        return space.IntersectRay(query).Count > 0;
    }

    private void UpdateStamina(bool sprinting, float dt)
    {
        var before = Stamina;

        if (sprinting)
        {
            Stamina = Mathf.Max(0f, Stamina - SprintDrain * dt);
            if (Stamina <= 0f)
                _sprintLocked = true;
        }
        else
        {
            Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegen * dt);
            if (_sprintLocked && Stamina >= SprintUnlockThreshold)
                _sprintLocked = false;
        }

        if (!Mathf.IsEqualApprox(before, Stamina))
            EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        Health = Mathf.Max(0f, Health - amount);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);

        var voice = GetNode<Audio.VoiceBank>("/root/VoiceBank");
        if (Health <= 0f)
        {
            IsDead = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            voice.Say("ben.death");
            EmitSignal(SignalName.Died);
        }
        else
        {
            voice.Say(amount >= 20f ? "ben.hurt.heavy" : "ben.hurt.light");
        }
    }

    public void Heal(float amount)
    {
        if (IsDead)
            return;
        Health = Mathf.Min(MaxHealth, Health + amount);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
    }

    private void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(LocaleManager.ConfigPath) != Error.Ok)
            return;
        MouseSensitivity = (float)cfg.GetValue("controls", "sensitivity", MouseSensitivity);
        InvertY = (bool)cfg.GetValue("controls", "invert_y", false);
    }
}
