using Godot;
using PostalBen.Systems;

namespace PostalBen.World;

/// <summary>
/// A counter Ben can pay at. The honest route through a shop.
///
/// Emits the same token the theft route emits - see ADR-006. The till neither knows nor
/// cares that there is another way out; it just settles what it is handed.
/// </summary>
public partial class Till : Interactable
{
    /// <summary>Item this till settles. Must be in the inventory, unpaid.</summary>
    [Export] public string Item { get; set; } = "milk";

    [Export] public int Price { get; set; } = 3;

    /// <summary>Token emitted once paid.</summary>
    [Export] public string CompletionToken { get; set; } = "shop.settled";

    /// <summary>Spoken when Ben has nothing to settle and prods the clerk anyway.</summary>
    [Export] public string IdleVoiceKey { get; set; } = string.Empty;

    /// <summary>
    /// The line this counter serves. Set by the district builder; a till with no queue
    /// serves immediately, which keeps the class usable for counters that never get busy.
    /// </summary>
    public ServiceQueue? Queue { get; set; }

    private Inventory Inv => GetNode<Inventory>("/root/Inventory");

    /// <summary>
    /// Reads as "Wait in line" whenever Ben cannot be served yet - because he is holding
    /// nothing, or because there are three people in front of him. It becomes "Pay" the
    /// moment he is genuinely next.
    /// </summary>
    public override string CurrentPromptKey() =>
        HasSomethingToSettle && IsBensTurn ? "ui.interact.pay" : "ui.interact.queue";

    private bool HasSomethingToSettle => Inv.Has(Item) && !Inv.IsPaidFor(Item);

    /// <summary>
    /// Whether the queue would have him. No queue means no waiting; otherwise he has to
    /// be at the front. Note this is never "no" forever - the line always advances.
    /// </summary>
    private bool IsBensTurn
    {
        get
        {
            if (Queue is null || !IsInstanceValid(Queue))
                return true;

            var ben = GetTree().GetFirstNodeInGroup("player") as Node3D;
            return ben is not null && (Queue.PositionOf(ben) < 0 || Queue.IsAtFront(ben));
        }
    }

    protected override void OnInteract()
    {
        if (!HasSomethingToSettle)
        {
            if (IdleVoiceKey.Length > 0)
                Voice(this).Say(IdleVoiceKey);
            return;
        }

        if (!IsBensTurn)
        {
            // Refusing here is the entire game in one interaction: he can wait, or he
            // can do something about the people in front of him.
            Voice(this).Say("ben.queue.wait_long");

            if (GetTree().GetFirstNodeInGroup("player") is Node3D jumper)
                Queue!.ReportQueueJumped(jumper);
            return;
        }

        if (!Inv.TrySpend(Price))
        {
            // Broke. Not a dead end: the milk is already in his hands and the door
            // is right there. That is the point.
            Voice(this).Say("ben.queue.wait_long");
            return;
        }

        Inv.MarkPaid(Item);
        GetNode<Audio.Sfx>("/root/Sfx").PlayAt("till", GlobalPosition, -4f);
        Errands(this).Notify(CompletionToken);
    }
}
