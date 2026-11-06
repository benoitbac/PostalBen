using Godot;
using System.Collections.Generic;
using System.Linq;
using PostalBen.Systems;

namespace PostalBen.Npc;

/// <summary>
/// Sends officers when the town calls them, and stands them down when it stops caring.
///
/// Dispatch is driven entirely by notoriety level, so the escalation the player feels is
/// the same number the design document talks about. Officers spawn off-screen at the
/// edge of the district and drive inward - never in front of the player, which would
/// read as cheating.
/// </summary>
public partial class PoliceDispatch : Node3D
{
    /// <summary>Officers on the street per notoriety level, indexed by the enum.</summary>
    private static readonly int[] StrengthFor = { 0, 0, 2, 5 };

    /// <summary>Distance from the district centre that responders arrive from.</summary>
    private const float ApproachRadius = 92f;

    /// <summary>Seconds between dispatch decisions - police do not teleport in packs.</summary>
    private const float DispatchInterval = 4f;

    private readonly List<Police> _onDuty = new();
    private float _nextDispatch;
    private ulong _seed = 0xD1CE5EEDUL;

    private NotorietySystem _notoriety = null!;

    public override void _Ready()
    {
        _notoriety = GetNode<NotorietySystem>("/root/Notoriety");
    }

    public override void _Process(double delta)
    {
        _onDuty.RemoveAll(p => !IsInstanceValid(p) || p.IsDead);

        _nextDispatch -= (float)delta;
        if (_nextDispatch > 0f)
            return;

        _nextDispatch = DispatchInterval;

        var wanted = StrengthFor[(int)_notoriety.Current];

        if (_onDuty.Count < wanted)
        {
            Deploy();
            return;
        }

        // Stood down: send one home per interval rather than vanishing the whole squad.
        if (_onDuty.Count > wanted && _onDuty.Count > 0)
        {
            var leaving = _onDuty[0];
            _onDuty.RemoveAt(0);
            leaving.QueueFree();
        }
    }

    private void Deploy()
    {
        var angle = NextFloat() * Mathf.Tau;
        var spawn = new Vector3(
            Mathf.Cos(angle) * ApproachRadius,
            0.2f,
            Mathf.Sin(angle) * ApproachRadius);

        var officer = new Police
        {
            Name = $"officer_{_onDuty.Count}_{(int)(NextFloat() * 1000)}",
            Position = spawn,
        };

        Humanoid.Build(officer, 1.78f, 1.08f,
            shirt: new Color("#1f2a44"),
            trousers: new Color("#161b2b"),
            skin: new Color(NextFloat() > 0.5f ? "#c69a76" : "#8d6748"));

        AddChild(officer);
        _onDuty.Add(officer);

        GD.Print($"[Dispatch] officer responding ({_onDuty.Count} on duty, level {_notoriety.Current})");
    }

    /// <summary>Officers currently on the street. Used by the HUD and the day report.</summary>
    public int OnDuty => _onDuty.Count(p => IsInstanceValid(p) && !p.IsDead);

    private float NextFloat()
    {
        _seed = _seed * 6364136223846793005UL + 1442695040888963407UL;
        return ((_seed >> 33) & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
