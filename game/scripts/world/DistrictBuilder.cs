using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace PostalBen.World;

/// <summary>
/// Builds the grey-box district from a JSON layout at load time.
///
/// A blockout hand-authored as .tscn is ~40 nodes of transforms that nobody can read in
/// a diff and nobody can tune without the editor open. As data it stays legible, and
/// the fixtures list - which is the part with actual game logic in it - survives the
/// art pass unchanged when the boxes are replaced by real geometry.
/// </summary>
public partial class DistrictBuilder : Node3D
{
    [Export] public string LayoutPath { get; set; } = "res://assets/district/day1.json";

    /// <summary>Pavement strip either side of a road.</summary>
    private const float PavementWidth = 3f;
    private const float RoadHeight = 0.04f;
    private const float PavementHeight = 0.12f;

    private readonly Dictionary<string, StandardMaterial3D> _materials = new();
    private Dictionary<string, string> _palette = new();

    public override void _Ready()
    {
        using var file = FileAccess.Open(LayoutPath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushError($"[District] cannot open '{LayoutPath}': {FileAccess.GetOpenError()}");
            return;
        }

        Layout layout;
        try
        {
            layout = JsonSerializer.Deserialize<Layout>(file.GetAsText(), JsonOpts)
                     ?? throw new JsonException("layout deserialized to null");
        }
        catch (JsonException e)
        {
            GD.PushError($"[District] malformed layout: {e.Message}");
            return;
        }

        _palette = layout.Palette ?? new Dictionary<string, string>();

        foreach (var road in layout.Roads ?? new List<Road>())
            BuildRoad(road);

        foreach (var building in layout.Buildings ?? new List<Building>())
            BuildBuilding(building);

        foreach (var fixture in layout.Fixtures ?? new List<Fixture>())
            BuildFixture(fixture);

        GD.Print($"[District] '{layout.Name}' built: " +
                 $"{layout.Roads?.Count ?? 0} roads, {layout.Buildings?.Count ?? 0} buildings, " +
                 $"{layout.Fixtures?.Count ?? 0} fixtures");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ------------------------------------------------------------------ geometry

    private void BuildRoad(Road road)
    {
        var from = new Vector3(road.From[0], 0f, road.From[1]);
        var to = new Vector3(road.To[0], 0f, road.To[1]);
        var mid = (from + to) / 2f;
        var length = from.DistanceTo(to);
        var horizontal = Mathf.Abs(to.X - from.X) > Mathf.Abs(to.Z - from.Z);

        var roadSize = horizontal
            ? new Vector3(length, RoadHeight, road.Width)
            : new Vector3(road.Width, RoadHeight, length);

        AddBox($"road_{road.From[0]}_{road.From[1]}", mid with { Y = RoadHeight / 2f },
            roadSize, Colour("road"), collides: false);

        // Pavement either side, slightly proud of the road so the kerb reads.
        var offset = road.Width / 2f + PavementWidth / 2f;
        for (var side = -1; side <= 1; side += 2)
        {
            var pavementPos = horizontal
                ? mid with { Y = PavementHeight / 2f, Z = mid.Z + side * offset }
                : mid with { Y = PavementHeight / 2f, X = mid.X + side * offset };

            var pavementSize = horizontal
                ? new Vector3(length, PavementHeight, PavementWidth)
                : new Vector3(PavementWidth, PavementHeight, length);

            AddBox($"pavement_{road.From[0]}_{road.From[1]}_{side}", pavementPos,
                pavementSize, Colour("pavement"), collides: false);
        }
    }

    private void BuildBuilding(Building building)
    {
        var size = new Vector3(building.Size[0], building.Size[1], building.Size[2]);
        var pos = new Vector3(building.Pos[0], size.Y / 2f, building.Pos[1]);

        AddBox(building.Name, pos, size, Colour(building.Colour), collides: true);

        if (building.Door is not { } door)
            return;

        var doorNode = new Door
        {
            Name = $"{building.Name}_door",
            OpensAtHour = door.OpensAt ?? -1,
            ClosesAtHour = door.ClosesAt ?? -1,
            ClosedVoiceKey = door.OpensAt.HasValue ? "ben.errand.milk.arrive" : string.Empty,
            Position = new Vector3(door.At[0], 1.05f, door.At[1]),
            RotationDegrees = new Vector3(0f, door.Facing, 0f),
        };

        // Pivot on the hinge edge so the leaf swings instead of spinning about its centre.
        var pivot = new Node3D { Name = "Pivot" };
        doorNode.AddChild(pivot);

        var leaf = new MeshInstance3D
        {
            Name = "Leaf",
            Mesh = new BoxMesh { Size = new Vector3(1.8f, 2.1f, 0.12f) },
            Position = new Vector3(0.9f, 0f, 0f),
            MaterialOverride = Material("#3a3226"),
        };
        pivot.AddChild(leaf);

        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.8f, 2.1f, 0.3f) },
            Position = new Vector3(0.9f, 0f, 0f),
        };
        pivot.AddChild(shape);

        AddChild(doorNode);
    }

    // ------------------------------------------------------------------ fixtures

    private void BuildFixture(Fixture f)
    {
        var pos = new Vector3(f.Pos[0], f.Pos[1], f.Pos[2]);
        var size = f.Size is { Length: 3 }
            ? new Vector3(f.Size[0], f.Size[1], f.Size[2])
            : Vector3.One;

        switch (f.Type)
        {
            case "trigger":
                AddArea(new TokenTrigger { CompletionToken = f.Token ?? string.Empty },
                    $"trigger_{f.Token}", pos, size);
                break;

            case "shopexit":
                AddArea(new ShopExit { Witnesses = f.Witnesses ?? 1 }, "shop_exit", pos, size);
                break;

            case "pickup":
                AddInteractable(new Pickup
                {
                    Item = f.Item ?? "item",
                    CompletionToken = f.Token ?? string.Empty,
                    Price = f.Price ?? 0,
                }, $"pickup_{f.Item}", pos, size, f.Colour);
                break;

            case "till":
                AddInteractable(new Till
                {
                    Item = f.Item ?? "item",
                    Price = f.Price ?? 0,
                    CompletionToken = f.Token ?? "shop.settled",
                    IdleVoiceKey = f.IdleVoice ?? string.Empty,
                }, $"till_{f.Item}", pos, size, f.Colour);
                break;

            case "clerk":
                AddInteractable(new Clerk
                {
                    RequiresItem = f.Requires ?? string.Empty,
                    GivesItem = f.Gives ?? string.Empty,
                    ConsumesItem = f.Consumes ?? string.Empty,
                    Pays = f.Pays ?? 0,
                    CompletionToken = f.Token ?? string.Empty,
                    VoiceKey = f.Voice ?? string.Empty,
                    RefusalVoiceKey = f.Refusal ?? "npc.clerk.form",
                    PromptKey = f.Prompt ?? "ui.interact.talk",
                }, $"clerk_{f.Token}", pos, size, f.Colour);
                break;

            default:
                GD.PushWarning($"[District] unknown fixture type '{f.Type}'");
                break;
        }
    }

    private void AddArea(Area3D area, string name, Vector3 pos, Vector3 size)
    {
        area.Name = name;
        area.Position = pos;
        area.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(area);
    }

    private void AddInteractable(Interactable node, string name, Vector3 pos, Vector3 size, string? colour)
    {
        node.Name = name;
        node.Position = pos;
        node.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        node.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Material(colour ?? "#c9a227"),
        });
        AddChild(node);
    }

    private void AddBox(string name, Vector3 pos, Vector3 size, string colour, bool collides)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            Position = pos,
            MaterialOverride = Material(colour),
        };

        if (!collides)
        {
            AddChild(mesh);
            return;
        }

        var body = new StaticBody3D { Name = name, Position = pos, CollisionLayer = 1, CollisionMask = 0 };
        mesh.Position = Vector3.Zero;
        body.AddChild(mesh);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }

    // ------------------------------------------------------------------ materials

    private string Colour(string? key) =>
        key is not null && _palette.TryGetValue(key, out var hex) ? hex : key ?? "#808080";

    /// <summary>Materials are shared per colour - a blockout has ~8 distinct surfaces.</summary>
    private StandardMaterial3D Material(string hex)
    {
        if (_materials.TryGetValue(hex, out var cached))
            return cached;

        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(hex),
            Roughness = 0.92f,
            MetallicSpecular = 0.1f,
        };
        _materials[hex] = material;
        return material;
    }

    // ------------------------------------------------------------------ schema

    private sealed class Layout
    {
        public string? Name { get; set; }
        public Dictionary<string, string>? Palette { get; set; }
        public List<Road>? Roads { get; set; }
        public List<Building>? Buildings { get; set; }
        public List<Fixture>? Fixtures { get; set; }
    }

    private sealed class Road
    {
        public float[] From { get; set; } = { 0, 0 };
        public float[] To { get; set; } = { 0, 0 };
        public float Width { get; set; } = 10f;
    }

    private sealed class Building
    {
        public string Name { get; set; } = "building";
        public float[] Pos { get; set; } = { 0, 0 };
        public float[] Size { get; set; } = { 10, 5, 10 };
        public string? Colour { get; set; }
        public DoorSpec? Door { get; set; }
    }

    private sealed class DoorSpec
    {
        public float[] At { get; set; } = { 0, 0 };
        public float Facing { get; set; }
        public int? OpensAt { get; set; }
        public int? ClosesAt { get; set; }
    }

    private sealed class Fixture
    {
        public string Type { get; set; } = string.Empty;
        public float[] Pos { get; set; } = { 0, 0, 0 };
        public float[]? Size { get; set; }
        public string? Token { get; set; }
        public string? Item { get; set; }
        public string? Requires { get; set; }
        public string? Gives { get; set; }
        public string? Consumes { get; set; }
        public string? Voice { get; set; }
        public string? Refusal { get; set; }
        public string? IdleVoice { get; set; }
        public string? Prompt { get; set; }
        public string? Colour { get; set; }
        public int? Price { get; set; }
        public int? Pays { get; set; }
        public int? Witnesses { get; set; }
    }
}
