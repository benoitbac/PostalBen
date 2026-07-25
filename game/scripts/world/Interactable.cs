using Godot;

namespace PostalBen.World;

/// <summary>
/// Anything Ben can look at and press E on.
///
/// Subclasses supply the verb and the effect. The prompt is a translation key, never
/// text - the interactable decides *what* it offers, the UI decides how that reads in
/// the player's language.
/// </summary>
public abstract partial class Interactable : StaticBody3D
{
    /// <summary>Translation key for the verb shown in the prompt, e.g. "ui.interact.door".</summary>
    [Export] public string PromptKey { get; set; } = "ui.interact.talk";

    /// <summary>When false the object is visible but offers no prompt.</summary>
    [Export] public bool Enabled { get; set; } = true;

    [Signal]
    public delegate void InteractedEventHandler();

    public override void _Ready()
    {
        // Layer 6 is what Interactor raycasts against. Setting it here rather than in
        // every scene means a new interactable cannot be invisible to the player by
        // forgetting a checkbox.
        CollisionLayer = 1 << 5;
    }

    /// <summary>
    /// Whether the prompt should show right now. Override to gate on state - a till
    /// with nothing to ring up shouldn't offer "Pay".
    /// </summary>
    public virtual bool CanInteract() => Enabled;

    /// <summary>
    /// Lets an object change its verb with its state without the Interactor knowing
    /// anything about tills, doors or clerks.
    /// </summary>
    public virtual string CurrentPromptKey() => PromptKey;

    public void Trigger()
    {
        if (!CanInteract())
            return;

        OnInteract();
        EmitSignal(SignalName.Interacted);
    }

    protected abstract void OnInteract();

    protected static Systems.ErrandLog Errands(Node node) =>
        node.GetNode<Systems.ErrandLog>("/root/ErrandLog");

    protected static Audio.VoiceBank Voice(Node node) =>
        node.GetNode<Audio.VoiceBank>("/root/VoiceBank");
}
