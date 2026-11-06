using Godot;
using PostalBen.Systems;

namespace PostalBen.World;

/// <summary>
/// A person behind a counter who wants something before they will do anything.
///
/// Deliberately generic: the bank's "you need the other form first" and its
/// "here is your money" are the same object configured differently. Bureaucracy is
/// content in this game, so it needs to be cheap to author.
/// </summary>
public partial class Clerk : Interactable
{
    /// <summary>Item Ben must be carrying. Blank means no prerequisite.</summary>
    [Export] public string RequiresItem { get; set; } = string.Empty;

    /// <summary>Item handed over. Blank means none.</summary>
    [Export] public string GivesItem { get; set; } = string.Empty;

    /// <summary>Item consumed on success, e.g. the cheque being cashed.</summary>
    [Export] public string ConsumesItem { get; set; } = string.Empty;

    /// <summary>Money paid to Ben.</summary>
    [Export] public int Pays { get; set; }

    [Export] public string CompletionToken { get; set; } = string.Empty;

    /// <summary>Line spoken on a successful interaction.</summary>
    [Export] public string VoiceKey { get; set; } = string.Empty;

    /// <summary>Line spoken when Ben lacks the prerequisite. The refusal is the joke.</summary>
    [Export] public string RefusalVoiceKey { get; set; } = "npc.clerk.form";

    /// <summary>Once served, the clerk stops offering. False for a repeatable counter.</summary>
    [Export] public bool Once { get; set; } = true;

    private bool _served;
    private Inventory Inv => GetNode<Inventory>("/root/Inventory");

    public override void _Ready()
    {
        base._Ready();
        if (PromptKey == "ui.interact.talk" && RequiresItem.Length > 0)
            PromptKey = "ui.interact.queue";
    }

    public override bool CanInteract() => base.CanInteract() && !(Once && _served);

    protected override void OnInteract()
    {
        if (RequiresItem.Length > 0 && !Inv.Has(RequiresItem))
        {
            // Not a failure state - he simply has to go and get the other form.
            // The prompt stays, so the player is never stuck, only delayed.
            Voice(this).Say(RefusalVoiceKey, "clerk");
            return;
        }

        if (ConsumesItem.Length > 0)
            Inv.Drop(ConsumesItem);

        if (GivesItem.Length > 0)
            Inv.Take(GivesItem, paid: true);

        if (Pays > 0)
            Inv.Earn(Pays);

        _served = true;

        if (VoiceKey.Length > 0)
            Voice(this).Say(VoiceKey, "clerk");

        if (CompletionToken.Length > 0)
            Errands(this).Notify(CompletionToken);
    }
}
