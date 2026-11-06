using Godot;
using System.Linq;
using PostalBen.Systems;

namespace PostalBen.World;

/// <summary>
/// The shop doorway, watching what leaves through it.
///
/// This is the second route to <c>shop.settled</c>. Walking out with unpaid goods
/// completes the errand exactly as paying does - it just costs notoriety instead of
/// money, and only if somebody sees him. No branching quest script; see ADR-006.
/// </summary>
public partial class ShopExit : Area3D
{
    [Export] public string CompletionToken { get; set; } = "shop.settled";

    /// <summary>Staff and customers who would notice. Drives the notoriety hit.</summary>
    [Export] public int Witnesses { get; set; } = 1;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1 << 1;
        BodyExited += OnBodyExited;
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is not Player.BenController)
            return;

        var inventory = GetNode<Inventory>("/root/Inventory");
        var stolen = inventory.Unpaid.ToList();
        if (stolen.Count == 0)
            return;

        GetNode<NotorietySystem>("/root/Notoriety")
            .Report(NotorietySystem.Incident.Theft, GlobalPosition, Witnesses);

        // The goods are his now, paid for or not. Marking them settled stops the
        // doorway re-reporting the same milk every time he walks past.
        foreach (var item in stolen)
            inventory.MarkPaid(item);

        GetNode<ErrandLog>("/root/ErrandLog").Notify(CompletionToken);
    }
}
