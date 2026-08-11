using Godot;
using System.Collections.Generic;
using System.Linq;

namespace PostalBen.Systems;

/// <summary>
/// What Ben is carrying, and whether he paid for it.
///
/// Paid-for state is tracked per item rather than as a global "stole something" flag,
/// because the shop only cares about the milk that walked out of the shop. It is what
/// lets the theft route work without a bespoke script.
/// </summary>
public partial class Inventory : Node
{
    [Signal]
    public delegate void ChangedEventHandler();

    private readonly Dictionary<string, bool> _items = new();

    public int Money { get; private set; } = 12;

    public bool Has(string item) => _items.ContainsKey(item);

    public bool IsPaidFor(string item) => _items.TryGetValue(item, out var paid) && paid;

    /// <summary>Items held but not paid for. Empty means Ben is walking out clean.</summary>
    public IEnumerable<string> Unpaid => _items.Where(kv => !kv.Value).Select(kv => kv.Key);

    public void Take(string item, bool paid = false)
    {
        _items[item] = paid;
        EmitSignal(SignalName.Changed);
    }

    public void MarkPaid(string item)
    {
        if (!_items.ContainsKey(item))
            return;
        _items[item] = true;
        EmitSignal(SignalName.Changed);
    }

    public bool Drop(string item)
    {
        var removed = _items.Remove(item);
        if (removed)
            EmitSignal(SignalName.Changed);
        return removed;
    }

    public bool TrySpend(int amount)
    {
        if (amount > Money)
            return false;
        Money -= amount;
        EmitSignal(SignalName.Changed);
        return true;
    }

    public void Earn(int amount)
    {
        Money += amount;
        EmitSignal(SignalName.Changed);
    }

    public void ClearForNewDay()
    {
        _items.Clear();
        EmitSignal(SignalName.Changed);
    }
}
