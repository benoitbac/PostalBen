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

        // Blocky humanoid: head, torso, two arms, two legs. Stacked capsules read as
        // skittles from any distance; separated limbs read as a person even at 40 m,
        // and they give the walk cycle something to swing.
        var height = 1.66f + NextFloat() * 0.2f;
        var build = 0.95f + NextFloat() * 0.2f;

        var shirt = Material(ShirtColours[(int)(NextFloat() * ShirtColours.Length) % ShirtColours.Length]);
        var trousers = Material(TrouserColours[(int)(NextFloat() * TrouserColours.Length) % TrouserColours.Length]);
        var skin = Material(NextFloat() > 0.5f ? "#c69a76" : "#8d6748");

        npc.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.3f, Height = height },
            Position = new Vector3(0f, height / 2f, 0f),
        });

        // Body pivot: the walk cycle bobs and leans this, so the collider stays put.
        var body = new Node3D { Name = "Body" };
        npc.AddChild(body);

        var shoulderY = height * 0.72f;
        var hipY = height * 0.5f;
        var halfWidth = 0.21f * build;

        body.AddChild(Part("Torso", new Vector3(0.42f * build, height * 0.30f, 0.24f * build),
            new Vector3(0f, shoulderY - height * 0.15f, 0f), shirt));

        body.AddChild(Part("Head", new Vector3(0.21f, 0.24f, 0.2f),
            new Vector3(0f, height * 0.93f, 0f), skin));

        body.AddChild(Part("Neck", new Vector3(0.12f, 0.07f, 0.12f),
            new Vector3(0f, height * 0.81f, 0f), skin));

        foreach (var side in new[] { -1f, 1f })
        {
            var arm = new Node3D
            {
                Name = side < 0 ? "ArmL" : "ArmR",
                Position = new Vector3(side * (halfWidth + 0.09f), shoulderY, 0f),
            };
            // Pivot at the shoulder so rotating the node swings the whole arm.
            arm.AddChild(Part("Limb", new Vector3(0.11f, height * 0.32f, 0.13f),
                new Vector3(0f, -height * 0.16f, 0f), shirt));
            body.AddChild(arm);

            var leg = new Node3D
            {
                Name = side < 0 ? "LegL" : "LegR",
                Position = new Vector3(side * 0.11f, hipY, 0f),
            };
            leg.AddChild(Part("Limb", new Vector3(0.15f, height * 0.46f, 0.17f),
                new Vector3(0f, -height * 0.23f, 0f), trousers));
            body.AddChild(leg);
        }

        return npc;
    }

    private static MeshInstance3D Part(string name, Vector3 size, Vector3 pos, StandardMaterial3D material)
    {
        return new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            Position = pos,
            MaterialOverride = material,
        };
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

    private static StandardMaterial3D Material(string hex) => new()
    {
        AlbedoColor = new Color(hex),
        Roughness = 0.88f,
    };

    private float NextFloat()
    {
        _seed = _seed * 6364136223846793005UL + 1442695040888963407UL;
        return ((_seed >> 33) & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
