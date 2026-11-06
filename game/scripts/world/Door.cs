using Godot;

namespace PostalBen.World;

/// <summary>
/// A door that swings. Cosmetic for now - it moves its own mesh and collision out of
/// the way rather than being a gate on progress, because a locked door with no key is
/// exactly the kind of dead end this game is not supposed to have.
/// </summary>
public partial class Door : Interactable
{
    [Export] public float OpenAngleDegrees { get; set; } = 95f;
    [Export] public float SwingSeconds { get; set; } = 0.45f;

    /// <summary>Shop hours. Outside them the door refuses, which is the joke.</summary>
    [Export] public int OpensAtHour { get; set; } = -1;
    [Export] public int ClosesAtHour { get; set; } = -1;

    /// <summary>Line Ben mutters at a closed door.</summary>
    [Export] public string ClosedVoiceKey { get; set; } = string.Empty;

    public bool IsOpen { get; private set; }

    private Node3D _pivot = null!;
    private CollisionShape3D? _blocker;
    private float _closedYaw;

    public override void _Ready()
    {
        base._Ready();
        PromptKey = "ui.interact.door";
        _pivot = GetNodeOrNull<Node3D>("Pivot") ?? this;
        _blocker = GetNodeOrNull<CollisionShape3D>("Blocker");
        _closedYaw = _pivot.Rotation.Y;
    }

    protected override void OnInteract()
    {
        if (!WithinOpeningHours())
        {
            if (ClosedVoiceKey.Length > 0)
                Voice(this).Say(ClosedVoiceKey);
            return;
        }

        IsOpen = !IsOpen;
        var target = _closedYaw + (IsOpen ? Mathf.DegToRad(OpenAngleDegrees) : 0f);

        // Clear the doorway the instant it starts opening, and only seal it once the
        // leaf has finished closing - otherwise Ben gets shoved by his own front door.
        if (IsOpen && _blocker is not null)
            _blocker.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_pivot, "rotation:y", target, SwingSeconds);

        if (!IsOpen && _blocker is not null)
            tween.TweenCallback(Callable.From(() =>
                _blocker.SetDeferred(CollisionShape3D.PropertyName.Disabled, false)));
    }

    private bool WithinOpeningHours()
    {
        if (OpensAtHour < 0 || ClosesAtHour < 0)
            return true;

        var hour = GetNode<Systems.GameState>("/root/GameState").Hour;
        return hour >= OpensAtHour && hour < ClosesAtHour;
    }
}
