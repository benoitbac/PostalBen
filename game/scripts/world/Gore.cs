using Godot;
using PostalBen.Systems;

namespace PostalBen.World;

/// <summary>
/// Blood. Honours the player's gore setting, which is a real setting and not a menu
/// decoration - Reduced keeps the feedback and drops the mess, Off removes it entirely.
///
/// The violence in this game is meant to be unglamorous, so this is deliberately dark,
/// matte and slow rather than bright arterial spray.
/// </summary>
public static class Gore
{
    public enum Level { Full, Reduced, Off }

    private static Level? _cached;

    public static Level Setting
    {
        get
        {
            if (_cached is { } value)
                return value;

            var cfg = new ConfigFile();
            var level = Level.Full;
            if (cfg.Load(LocaleManager.ConfigPath) == Error.Ok)
                level = (Level)(int)cfg.GetValue("ui", "gore", (int)Level.Full);

            _cached = level;
            return level;
        }
    }

    /// <summary>Call after changing the setting so the next hit picks it up.</summary>
    public static void Invalidate() => _cached = null;

    private static readonly Color Blood = new("#5e0f0f");

    /// <summary>
    /// A short burst at the point of impact. On Reduced this is a handful of specks so
    /// the player still gets hit confirmation without the spectacle.
    /// </summary>
    public static void Splatter(Node3D context, Vector3 at, Vector3 direction)
    {
        if (Setting == Level.Off)
            return;

        var count = Setting == Level.Full ? 26 : 6;

        var particles = new GpuParticles3D
        {
            Name = "Splatter",
            Amount = count,
            OneShot = true,
            Emitting = true,
            Lifetime = 1.1,
            Explosiveness = 0.95f,
            Position = at,
            DrawPass1 = new BoxMesh { Size = Vector3.One * 0.05f },
        };

        var material = new ParticleProcessMaterial
        {
            Direction = direction.Normalized(),
            Spread = 38f,
            InitialVelocityMin = 2.4f,
            InitialVelocityMax = 6.5f,
            Gravity = new Vector3(0f, -14f, 0f),
            ScaleMin = 0.5f,
            ScaleMax = 1.5f,
            Color = Blood,
        };
        particles.ProcessMaterial = material;
        particles.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = Blood,
            Roughness = 0.35f,
        };

        context.GetTree().CurrentScene?.AddChild(particles);

        // Particles clean themselves up; nothing else holds a reference to them.
        var timer = context.GetTree().CreateTimer(2.5);
        timer.Timeout += () => { if (GodotObject.IsInstanceValid(particles)) particles.QueueFree(); };
    }

    /// <summary>
    /// The pool that spreads under a body. Full only - on Reduced a corpse is a corpse
    /// without the floor turning into a crime scene photo.
    /// </summary>
    public static void Pool(Node3D corpse, float radius = 0.9f)
    {
        if (Setting != Level.Full)
            return;

        var pool = new MeshInstance3D
        {
            Name = "Pool",
            Mesh = new PlaneMesh { Size = new Vector2(0.2f, 0.2f) },
            Position = new Vector3(0f, 0.03f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = Blood with { A = 0.92f },
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.25f,
                // Sits flush on the ground without fighting it for depth.
                NoDepthTest = false,
                RenderPriority = 1,
            },
        };

        corpse.AddChild(pool);

        // Spreads over a few seconds rather than appearing whole, which is both less
        // cartoonish and easier to read as "this just happened".
        var tween = corpse.CreateTween();
        tween.TweenProperty(pool, "scale", Vector3.One * (radius * 5f), 4.5f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    }
}
