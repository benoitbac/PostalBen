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

    /// <summary>Wall thickness for buildings that have an interior.</summary>
    private const float WallThickness = 0.4f;

    /// <summary>Clear width of a doorway opening.</summary>
    private const float DoorGap = 3f;

    private void BuildBuilding(Building building)
    {
        var size = new Vector3(building.Size[0], building.Size[1], building.Size[2]);
        var centre = new Vector2(building.Pos[0], building.Pos[1]);

        // A building with a door is somewhere Ben goes inside, so it is built as four
        // walls around an empty interior. A building without one is scenery and stays a
        // solid mass - cheaper, and nothing needs to be reachable in there.
        if (building.Door is not { } door)
        {
            AddBox(building.Name, new Vector3(centre.X, size.Y / 2f, centre.Y),
                size, Colour(building.Colour), collides: true);
            return;
        }

        BuildWalls(building.Name, centre, size, Colour(building.Colour), door);
        BuildDoor(building.Name, door);
    }

    /// <summary>
    /// Four walls with a gap in whichever face the door sits on.
    ///
    /// Deliberately roofless: a closed box has no interior lighting, so the shop would
    /// be a black void the moment Ben stepped inside. Roofs arrive with the lighting
    /// pass in the art sprint.
    /// </summary>
    private void BuildWalls(string name, Vector2 centre, Vector3 size, string colour, DoorSpec door)
    {
        var halfW = size.X / 2f;
        var halfD = size.Z / 2f;
        var y = size.Y / 2f;

        var doorPos = new Vector2(door.At[0], door.At[1]);

        // Which face is the door in? Compare its distance to each of the four planes.
        var toNorth = Mathf.Abs(doorPos.Y - (centre.Y - halfD));
        var toSouth = Mathf.Abs(doorPos.Y - (centre.Y + halfD));
        var toWest = Mathf.Abs(doorPos.X - (centre.X - halfW));
        var toEast = Mathf.Abs(doorPos.X - (centre.X + halfW));
        var nearest = Mathf.Min(Mathf.Min(toNorth, toSouth), Mathf.Min(toWest, toEast));

        // North / south walls run along X.
        BuildWallRun($"{name}_n", isAlongX: true, fixedCoord: centre.Y - halfD,
            from: centre.X - halfW, to: centre.X + halfW, y: y, height: size.Y,
            colour: colour, gapAt: nearest == toNorth ? doorPos.X : null);

        BuildWallRun($"{name}_s", isAlongX: true, fixedCoord: centre.Y + halfD,
            from: centre.X - halfW, to: centre.X + halfW, y: y, height: size.Y,
            colour: colour, gapAt: nearest == toSouth ? doorPos.X : null);

        // East / west walls run along Z, inset so they don't overlap the others.
        BuildWallRun($"{name}_w", isAlongX: false, fixedCoord: centre.X - halfW,
            from: centre.Y - halfD + WallThickness, to: centre.Y + halfD - WallThickness,
            y: y, height: size.Y, colour: colour,
            gapAt: nearest == toWest ? doorPos.Y : null);

        BuildWallRun($"{name}_e", isAlongX: false, fixedCoord: centre.X + halfW,
            from: centre.Y - halfD + WallThickness, to: centre.Y + halfD - WallThickness,
            y: y, height: size.Y, colour: colour,
            gapAt: nearest == toEast ? doorPos.Y : null);
    }

    /// <summary>
    /// One wall, optionally split into two segments around a doorway. A gap that falls
    /// outside the run is ignored, which keeps a mis-placed door in the layout from
    /// silently deleting a whole wall.
    /// </summary>
    private void BuildWallRun(string name, bool isAlongX, float fixedCoord,
        float from, float to, float y, float height, string colour, float? gapAt)
    {
        if (gapAt is not { } gap || gap - DoorGap / 2f <= from || gap + DoorGap / 2f >= to)
        {
            AddWallSegment(name, isAlongX, fixedCoord, from, to, y, height, colour);
            return;
        }

        AddWallSegment($"{name}_a", isAlongX, fixedCoord, from, gap - DoorGap / 2f, y, height, colour);
        AddWallSegment($"{name}_b", isAlongX, fixedCoord, gap + DoorGap / 2f, to, y, height, colour);
    }

    private void AddWallSegment(string name, bool isAlongX, float fixedCoord,
        float from, float to, float y, float height, string colour)
    {
        var length = to - from;
        if (length <= 0.05f)
            return;

        var mid = (from + to) / 2f;
        var pos = isAlongX
            ? new Vector3(mid, y, fixedCoord)
            : new Vector3(fixedCoord, y, mid);

        var size = isAlongX
            ? new Vector3(length, height, WallThickness)
            : new Vector3(WallThickness, height, length);

        AddBox(name, pos, size, colour, collides: true);
    }

    private void BuildDoor(string name, DoorSpec door)
    {
        var doorNode = new Door
        {
            Name = $"{name}_door",
            OpensAtHour = door.OpensAt ?? -1,
            ClosesAtHour = door.ClosesAt ?? -1,
            ClosedVoiceKey = door.OpensAt.HasValue ? "ben.errand.milk.arrive" : string.Empty,
            Position = new Vector3(door.At[0], 1.05f, door.At[1]),
            RotationDegrees = new Vector3(0f, door.Facing, 0f),
        };

        // Pivot on the hinge edge so the leaf swings instead of spinning about its centre.
        var pivot = new Node3D { Name = "Pivot" };
        doorNode.AddChild(pivot);

        pivot.AddChild(new MeshInstance3D
        {
            Name = "Leaf",
            Mesh = new BoxMesh { Size = new Vector3(DoorGap, 2.1f, 0.12f) },
            Position = new Vector3(DoorGap / 2f, 0f, 0f),
            MaterialOverride = Material("#3a3226"),
        });

        // Direct child of the body: Godot only registers a CollisionShape3D parented
        // straight to a CollisionObject3D, so hanging it off the pivot would have left
        // the door with no collision at all. The shape is disabled while open instead
        // of swinging with the leaf.
        doorNode.AddChild(new CollisionShape3D
        {
            Name = "Blocker",
            Shape = new BoxShape3D { Size = new Vector3(DoorGap, 2.1f, 0.3f) },
            Position = new Vector3(DoorGap / 2f, 0f, 0f),
        });

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
