using Godot;

namespace PostalBen.World;

/// <summary>
/// A volume that reports an errand token when Ben walks into it. Used for the
/// "arrive somewhere" stages that need no button press.
/// </summary>
public partial class TokenTrigger : Area3D
{
    [Export] public string CompletionToken { get; set; } = string.Empty;

    /// <summary>Fire on leaving rather than entering.</summary>
    [Export] public bool OnExit { get; set; }

    /// <summary>Fire only the first time. False for volumes that re-arm, like a doorway.</summary>
    [Export] public bool Once { get; set; } = true;

    private bool _fired;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1 << 1; // player only

        if (OnExit)
            BodyExited += OnBody;
        else
            BodyEntered += OnBody;
    }

    private void OnBody(Node3D body)
    {
        if (body is not Player.BenController)
            return;
        if (Once && _fired)
            return;

        _fired = true;
        if (CompletionToken.Length > 0)
            GetNode<Systems.ErrandLog>("/root/ErrandLog").Notify(CompletionToken);
    }
}
