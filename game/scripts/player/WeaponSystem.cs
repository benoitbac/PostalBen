using Godot;
using System.Collections.Generic;
using PostalBen.Systems;

namespace PostalBen.Player;

/// <summary>
/// What Ben is holding, and what happens when he uses it.
///
/// The escalation ladder is deliberate: bare hands hurt but rarely kill, the shovel
/// kills in two swings, the pistol kills at range and is heard by the whole street.
/// Drawing anything at all costs notoriety on its own - the design pillar is that
/// escalation is visible and chosen, not stumbled into.
/// </summary>
public partial class WeaponSystem : Node3D
{
    public enum Kind { Melee, Firearm }

    public sealed class Weapon
    {
        public required string Id { get; init; }
        public required string NameKey { get; init; }
        public required Kind Kind { get; init; }
        public required float Damage { get; init; }
        public required float Cooldown { get; init; }
        public float Range { get; init; } = 2.2f;
        public int MagazineSize { get; init; }
        public int Reserve { get; init; }
        public bool RaisesAlarmOnDraw { get; init; }

        public int Loaded { get; set; }
        public int Spare { get; set; }
    }

    [Signal]
    public delegate void WeaponChangedEventHandler(string id);

    [Signal]
    public delegate void AmmoChangedEventHandler(int loaded, int spare);

    private readonly List<Weapon> _weapons = new()
    {
        new Weapon
        {
            Id = "fists", NameKey = "ui.hud.no_weapon", Kind = Kind.Melee,
            Damage = 14f, Cooldown = 0.42f, Range = 1.9f,
        },
        new Weapon
        {
            Id = "shovel", NameKey = "ui.weapon.shovel", Kind = Kind.Melee,
            Damage = 58f, Cooldown = 0.72f, Range = 2.4f, RaisesAlarmOnDraw = true,
        },
        new Weapon
        {
            Id = "pistol", NameKey = "ui.weapon.pistol", Kind = Kind.Firearm,
            Damage = 46f, Cooldown = 0.24f, Range = 90f,
            MagazineSize = 12, Reserve = 48, RaisesAlarmOnDraw = true,
        },
    };

    /// <summary>
    /// Where the held object sits relative to the camera: right hand, low, close.
    /// Postal's whole read is "this man is carrying something", so it has to be
    /// unmistakably in frame rather than tucked at the edge.
    /// </summary>
    private static readonly Vector3 ViewmodelRest = new(0.42f, -0.46f, -0.78f);

    private int _index;
    private float _cooldown;
    private bool _drawnThisLife;

    private Camera3D _camera = null!;
    private BenController? _ben;
    private Node3D _viewmodel = null!;
    private MeshInstance3D _model = null!;
    private OmniLight3D _muzzleFlash = null!;
    private NotorietySystem _notoriety = null!;
    private Audio.VoiceBank _voice = null!;

    private float _kick;

    public Weapon Held => _weapons[_index];

    public override void _Ready()
    {
        _camera = GetParent<Camera3D>();
        _ben = GetTree().GetFirstNodeInGroup("player") as BenController;
        _notoriety = GetNode<NotorietySystem>("/root/Notoriety");
        _voice = GetNode<Audio.VoiceBank>("/root/VoiceBank");

        foreach (var weapon in _weapons)
        {
            weapon.Loaded = weapon.MagazineSize;
            weapon.Spare = weapon.Reserve;
        }

        BuildViewmodel();
        Equip(0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("weapon_next"))
            Equip((_index + 1) % _weapons.Count);
        else if (@event.IsActionPressed("weapon_prev"))
            Equip((_index - 1 + _weapons.Count) % _weapons.Count);
        else if (@event.IsActionPressed("holster"))
            Equip(0);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _cooldown = Mathf.Max(0f, _cooldown - dt);

        // Recoil/swing kick decays back to rest.
        _kick = Mathf.MoveToward(_kick, 0f, dt * 6f);
        _viewmodel.Position = ViewmodelRest with { Z = ViewmodelRest.Z + _kick * 0.16f };
        _viewmodel.Rotation = _viewmodel.Rotation with { X = _kick * 0.9f };

        if (_ben is { IsDetained: false, IsDead: false } &&
            Input.IsActionPressed("attack_primary") && _cooldown <= 0f)
        {
            Attack();
        }
    }

    // ---------------------------------------------------------------- actions

    private void Equip(int index)
    {
        _index = Mathf.Clamp(index, 0, _weapons.Count - 1);
        _cooldown = 0.25f;
        StyleViewmodel();

        // Pulling a weapon out in public is itself an incident, before anyone is hurt.
        if (Held.RaisesAlarmOnDraw && !_drawnThisLife)
        {
            _drawnThisLife = true;
            _notoriety.Report(NotorietySystem.Incident.WeaponDrawn, GlobalPosition, WitnessCount());
            _voice.Say("ben.combat.gun_draw");
        }

        EmitSignal(SignalName.WeaponChanged, Held.Id);
        EmitSignal(SignalName.AmmoChanged, Held.Loaded, Held.Spare);
    }

    private void Attack()
    {
        _cooldown = Held.Cooldown;
        _kick = Held.Kind == Kind.Firearm ? 1f : 0.7f;

        // Police read this to tell resisting apart from surrendering.
        _ben?.NotifyAttacked();

        if (Held.Kind == Kind.Firearm)
        {
            if (Held.Loaded <= 0)
            {
                Reload();
                return;
            }

            Held.Loaded--;
            EmitSignal(SignalName.AmmoChanged, Held.Loaded, Held.Spare);
            Flash();

            // A gunshot is heard whether or not it hits anything.
            _notoriety.Report(NotorietySystem.Incident.GunshotFired, GlobalPosition, WitnessCount());
        }

        var hit = Probe();
        if (hit is null)
            return;

        var wasAlive = !hit.IsDead;
        hit.TakeDamage(Held.Damage, GlobalPosition);

        if (wasAlive && hit.IsDead)
            OnKill();
        else if (Held.Kind == Kind.Melee)
            _voice.Say("ben.combat.melee_hit");
    }

    private void Reload()
    {
        if (Held.Spare <= 0 || Held.Loaded == Held.MagazineSize)
            return;

        var wanted = Held.MagazineSize - Held.Loaded;
        var taken = Mathf.Min(wanted, Held.Spare);
        Held.Loaded += taken;
        Held.Spare -= taken;

        _cooldown = 1.1f;
        _voice.Say("ben.combat.reload");
        EmitSignal(SignalName.AmmoChanged, Held.Loaded, Held.Spare);
    }

    private void OnKill()
    {
        var state = GetNode<GameState>("/root/GameState");

        // The first one is the tonal pivot of the whole game. It is played straight.
        _voice.Say(state.KillsTotal <= 1 ? "ben.combat.first_blood" : "ben.combat.kill_regret");
    }

    // ---------------------------------------------------------------- helpers

    private Npc.Npc? Probe()
    {
        var space = GetWorld3D().DirectSpaceState;
        var from = _camera.GlobalPosition;
        var to = from - _camera.GlobalTransform.Basis.Z * Held.Range;

        // World + npc layers: a wall between Ben and a bystander stops both the shovel
        // and the bullet.
        var query = PhysicsRayQueryParameters3D.Create(from, to, collisionMask: 1 | (1 << 2));
        var hit = space.IntersectRay(query);

        return hit.Count > 0 ? hit["collider"].As<Node>() as Npc.Npc : null;
    }

    private int WitnessCount()
    {
        var seen = 0;
        foreach (var node in GetTree().GetNodesInGroup("npc"))
        {
            if (node is Npc.Npc npc && !npc.IsDead &&
                npc.GlobalPosition.DistanceTo(GlobalPosition) < 24f)
            {
                seen++;
            }
        }
        return Mathf.Max(1, seen);
    }

    private void Flash()
    {
        _muzzleFlash.Visible = true;
        var tween = CreateTween();
        tween.TweenInterval(0.045);
        tween.TweenCallback(Callable.From(() => _muzzleFlash.Visible = false));
    }

    private void BuildViewmodel()
    {
        // Parented to this node rather than to the camera: WeaponSystem is already a
        // child of Camera3D, so the transform is identical, and add_child on the parent
        // fails outright while that parent is still setting up its own children.
        _viewmodel = new Node3D { Name = "Viewmodel", Position = ViewmodelRest };
        AddChild(_viewmodel);

        _model = new MeshInstance3D
        {
            Name = "Model",
            // Never culled or shadowed: a held object sits inside the camera's near
            // range and would otherwise flicker in and out against nearby geometry.
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 16384f,
        };
        _viewmodel.AddChild(_model);

        _muzzleFlash = new OmniLight3D
        {
            Name = "MuzzleFlash",
            Visible = false,
            LightColor = new Color("#ffd28a"),
            LightEnergy = 6f,
            OmniRange = 7f,
            Position = new Vector3(0f, 0f, -0.35f),
        };
        _viewmodel.AddChild(_muzzleFlash);
    }

    /// <summary>Crude held-object shapes. Replaced by real models in the art pass.</summary>
    private void StyleViewmodel()
    {
        var (size, colour) = Held.Id switch
        {
            "shovel" => (new Vector3(0.07f, 0.07f, 0.85f), "#8a7048"),
            "pistol" => (new Vector3(0.06f, 0.11f, 0.24f), "#3a3d44"),
            _ => (new Vector3(0.11f, 0.1f, 0.2f), "#c69a76"),
        };

        _model.Mesh = new BoxMesh { Size = size };
        _model.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(colour),
            Roughness = 0.65f,
            // Held objects are usually in the player's own shadow. A little self-lighting
            // keeps them readable without lighting the scene.
            EmissionEnabled = true,
            Emission = new Color(colour),
            EmissionEnergyMultiplier = 0.12f,
        };
        _model.Position = new Vector3(0f, 0f, -size.Z / 2f);
    }
}
