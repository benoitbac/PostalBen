using Godot;
using System.Collections.Generic;

namespace PostalBen.Npc;

/// <summary>
/// Populates the streets.
///
/// An empty town is the single biggest reason a blockout reads as a model rather than a
/// place. This spawns bodies along the road grid so there is always someone in view,
/// someone to be obstructed by, and someone to witness what Ben does.
/// </summary>
public partial class Crowd : Node3D
{
    [Export] public int Count { get; set; } = 56;

    /// <summary>
    /// Half-extent of the populated area. Tight on purpose - spreading the same crowd
    /// over the whole map puts nobody in view, and a street with nobody on it is the
    /// thing this class exists to prevent.
    /// </summary>
    [Export] public float Spread { get; set; } = 58f;

    /// <summary>Roads sit on multiples of this in both axes.</summary>
    private const float BlockSize = 40f;

    private static readonly string[] ShirtColours =
    {
        "#b6553f", "#4f6b8a", "#7a8f5c", "#8a7f6b", "#6b5a7a",
        "#a89060", "#5c7a78", "#94566b", "#77797f", "#c0a86e",
    };

    private static readonly string[] TrouserColours =
    {
        "#3b3f47", "#4a4438", "#2f3a45", "#54483c", "#3f4a3a",
    };

    private ulong _seed = 0x9E3779B97F4A7C15UL;

    public override void _Ready()
    {
        var built = new List<Npc>();

        for (var i = 0; i < Count; i++)
        {
            var npc = BuildOne(i);
            AddChild(npc);
            built.Add(npc);
        }

        GD.Print($"[Crowd] {built.Count} residents on the streets");
    }

    private Npc BuildOne(int index)
    {
        var npc = new Npc
        {
            Name = $"resident_{index}",
            Position = StreetPosition(),
            WalkSpeed = 1.7f + NextFloat() * 0.9f,
        };

        var height = 1.66f + NextFloat() * 0.2f;
        var build = 0.95f + NextFloat() * 0.2f;

        Humanoid.Build(npc, height, build,
            new Color(ShirtColours[(int)(NextFloat() * ShirtColours.Length) % ShirtColours.Length]),
            new Color(TrouserColours[(int)(NextFloat() * TrouserColours.Length) % TrouserColours.Length]),
            new Color(NextFloat() > 0.5f ? "#c69a76" : "#8d6748"));

        return npc;
    }

    /// <summary>
    /// Drops someone onto a pavement rather than in the middle of a block, so the crowd
    /// starts where a crowd would actually be.
    /// </summary>
    private Vector3 StreetPosition()
    {
        var alongX = NextFloat() > 0.5f;
        var laneIndex = Mathf.Round(NextFloat() * 4f) - 2f;
        var lane = laneIndex * BlockSize + (NextFloat() * 9f - 4.5f);
        var travel = (NextFloat() * 2f - 1f) * Spread;

        return alongX
            ? new Vector3(travel, 0.2f, lane)
            : new Vector3(lane, 0.2f, travel);
    }

    private float NextFloat()
    {
        _seed = _seed * 6364136223846793005UL + 1442695040888963407UL;
        return ((_seed >> 33) & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
