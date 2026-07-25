using Godot;
using PostalBen.Systems;

namespace PostalBen.World;

/// <summary>
/// An object Ben can take off a shelf. Taking it is never the same as paying for it -
/// that distinction is the whole shoplifting route.
/// </summary>
public partial class Pickup : Interactable
{
    /// <summary>Inventory id, e.g. "milk".</summary>
    [Export] public string Item { get; set; } = "milk";

    /// <summary>Token emitted on pickup. Blank means the pickup advances no errand.</summary>
    [Export] public string CompletionToken { get; set; } = string.Empty;

    /// <summary>Cost if Ben decides to be a customer about it.</summary>
    [Export] public int Price { get; set; } = 3;

    /// <summary>Removed from the world once taken. False for an infinite shelf.</summary>
    [Export] public bool ConsumeOnTake { get; set; } = true;

    public override void _Ready()
    {
        base._Ready();
        PromptKey = "ui.interact.pickup";
    }

    public override bool CanInteract() =>
        base.CanInteract() && !GetNode<Inventory>("/root/Inventory").Has(Item);

    protected override void OnInteract()
    {
        GetNode<Inventory>("/root/Inventory").Take(Item, paid: false);

        if (CompletionToken.Length > 0)
            Errands(this).Notify(CompletionToken);

        if (ConsumeOnTake)
            Visible = false;

        SetDeferred(PropertyName.Enabled, false);
    }
}
