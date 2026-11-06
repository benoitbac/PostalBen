using Godot;
using PostalBen.World;

namespace PostalBen.Player;

/// <summary>
/// Ben's "what am I looking at" sense. Raycasts forward from the camera onto the
/// interactable layer and reports the current target so the HUD can prompt for it.
/// </summary>
public partial class Interactor : Node3D
{
    /// <summary>Arm's length. Short on purpose - Ben has to actually walk up to things.</summary>
    [Export] public float Reach { get; set; } = 2.6f;

    [Signal]
    public delegate void TargetChangedEventHandler(string promptKey);

    public Interactable? Target { get; private set; }

    private Camera3D _camera = null!;

    public override void _Ready()
    {
        _camera = GetParent().GetNode<Camera3D>("Camera3D");
    }

    public override void _PhysicsProcess(double delta)
    {
        var found = Probe();

        // Report on identity *or* prompt change: a till that flips from "Wait in line"
        // to "Pay" is the same object with a different offer, and the HUD must follow.
        var promptKey = found?.CurrentPromptKey();
        var changed = found != Target || promptKey != _lastPromptKey;

        Target = found;
        _lastPromptKey = promptKey;

        if (changed)
            EmitSignal(SignalName.TargetChanged, promptKey ?? string.Empty);
    }

    private string? _lastPromptKey;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("interact"))
            Target?.Trigger();
    }

    private Interactable? Probe()
    {
        var space = GetWorld3D().DirectSpaceState;
        var from = _camera.GlobalPosition;
        var to = from - _camera.GlobalTransform.Basis.Z * Reach;

        // Mask 1 | 32: world geometry and interactables. Including the world means a
        // till behind a wall is correctly not reachable through the wall.
        var query = PhysicsRayQueryParameters3D.Create(from, to, collisionMask: 1 | (1 << 5));
        query.Exclude = new Godot.Collections.Array<Rid> { GetOwnerRid() };

        var hit = space.IntersectRay(query);
        if (hit.Count == 0)
            return null;

        var collider = hit["collider"].As<Node>();
        return collider as Interactable is { } interactable && interactable.CanInteract()
            ? interactable
            : null;
    }

    private Rid GetOwnerRid() =>
        GetParent().GetParent() is CollisionObject3D body ? body.GetRid() : default;
}
