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

    private Inventory Inv => GetNode<Inventory>("/root/Inventory");

    public override bool CanInteract() => base.CanInteract();

    /// <summary>
    /// Reads as "Wait in line" until Ben is actually holding something unpaid, then
    /// becomes "Pay". One object, two honest offers.
    /// </summary>
    public override string CurrentPromptKey() =>
        HasSomethingToSettle ? "ui.interact.pay" : "ui.interact.queue";

    private bool HasSomethingToSettle => Inv.Has(Item) && !Inv.IsPaidFor(Item);

    protected override void OnInteract()
    {
        if (!HasSomethingToSettle)
        {
            if (IdleVoiceKey.Length > 0)
                Voice(this).Say(IdleVoiceKey);
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
        Errands(this).Notify(CompletionToken);
    }
}
