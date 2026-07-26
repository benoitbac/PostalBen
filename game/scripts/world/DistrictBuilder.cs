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

        if (layout.Ground is { } ground)
        {
            AddBox("ground", new Vector3(0f, -0.5f, 0f),
                new Vector3(ground.Size, 1f, ground.Size), Colour(ground.Colour), collides: true);
        }

        foreach (var road in layout.Roads ?? new List<Road>())
            BuildRoad(road);

        foreach (var lot in layout.Lots ?? new List<Lot>())
        {
            // Flat pads: forecourts, parking, the concrete apron in front of a strip
            // mall. Laid slightly proud of the ground so they don't z-fight with it.
            AddBox(lot.Name, new Vector3(lot.Pos[0], 0.06f, lot.Pos[1]),
                new Vector3(lot.Size[0], 0.12f, lot.Size[1]), Colour(lot.Colour), collides: false);

            for (var i = 0; i < lot.Cars; i++)
                AddCar($"{lot.Name}_car{i}", lot, i);
        }

        foreach (var terrace in layout.Terraces ?? new List<Terrace>())
            BuildTerrace(terrace);

        foreach (var building in layout.Buildings ?? new List<Building>())
            BuildBuilding(building);

        foreach (var road in layout.Roads ?? new List<Road>())
            BuildStreetFurniture(road);

        foreach (var fixture in layout.Fixtures ?? new List<Fixture>())
            BuildFixture(fixture);

        GD.Print($"[District] '{layout.Name}' built: {GetChildCount()} nodes - " +
                 $"{layout.Roads?.Count ?? 0} roads, {layout.Terraces?.Count ?? 0} terrace runs, " +
                 $"{layout.Buildings?.Count ?? 0} landmarks, {layout.Fixtures?.Count ?? 0} fixtures");
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

        if (building.Sign is { } sign)
            BuildSign(building.Name, door, size.Y, sign);
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

    /// <summary>
    /// A run of terraced buildings along a street frontage, split into units of varying
    /// width and height.
    ///
    /// This exists because density is what makes a street read as a street. Writing
    /// forty buildings out by hand in the layout would be unmaintainable; a terrace is
    /// four numbers and produces a whole block face.
    /// </summary>
    private void BuildTerrace(Terrace terrace)
    {
        var from = new Vector2(terrace.From[0], terrace.From[1]);
        var to = new Vector2(terrace.To[0], terrace.To[1]);
        var run = from.DistanceTo(to);
        if (run < 1f)
            return;

        var alongX = Mathf.Abs(to.X - from.X) > Mathf.Abs(to.Y - from.Y);
        var units = Mathf.Max(1, Mathf.RoundToInt(run / terrace.UnitWidth));
        var unit = run / units;

        // Deterministic pseudo-random heights: the layout must build identically every
        // run, and Godot's RNG would make the geometry probe flaky.
        var seed = (int)(from.X * 31 + from.Y * 17 + run);

        for (var i = 0; i < units; i++)
        {
            var t = (i + 0.5f) / units;
            var centre = from.Lerp(to, t);

            var wobble = Mathf.Abs(Mathf.Sin((seed + i * 7919) * 0.7f));
            var height = Mathf.Lerp(terrace.MinHeight, terrace.MaxHeight, wobble);

            var width = unit - terrace.Gap;
            var size = alongX
                ? new Vector3(width, height, terrace.Depth)
                : new Vector3(terrace.Depth, height, width);

            var pos = new Vector3(centre.X, height / 2f, centre.Y);
            AddBox($"{terrace.Name}_{i}", pos, size, Colour(terrace.Colour), collides: true);

            // A parapet strip breaks the flat roofline that makes box towns look fake.
            var parapet = alongX
                ? new Vector3(width, 0.6f, terrace.Depth + 0.5f)
                : new Vector3(terrace.Depth + 0.5f, 0.6f, width);
            AddBox($"{terrace.Name}_{i}_cap", pos with { Y = height + 0.3f }, parapet,
                Colour(terrace.CapColour ?? terrace.Colour), collides: false);

            AddWindows($"{terrace.Name}_{i}", centre, width, height, terrace.Depth, alongX);
        }
    }

    /// <summary>
    /// Window bands on the street-facing side, one row per storey.
    ///
    /// A blank textured wall reads as a boundary wall, not a building. Windows are the
    /// cheapest cue that tells the eye "this has floors and people in it", and they set
    /// the storey height that makes the whole street scale correctly.
    /// </summary>
    private void AddWindows(string name, Vector2 centre, float width, float height,
        float depth, bool alongX)
    {
        const float StoreyHeight = 3.2f;
        const float SillHeight = 1.1f;
        const float WindowHeight = 1.5f;
        const float Inset = 0.06f;

        var storeys = Mathf.FloorToInt((height - SillHeight) / StoreyHeight);
        if (storeys < 1)
            return;

        // Two windows per unit, inset from the party walls.
        var half = depth / 2f;

        for (var s = 0; s < storeys; s++)
        {
            var y = SillHeight + s * StoreyHeight + WindowHeight / 2f;
            if (y + WindowHeight / 2f > height - 0.4f)
                break;

            for (var k = -1; k <= 1; k += 2)
            {
                var along = k * width * 0.22f;

                // Both faces, so the street reads from either direction.
                for (var face = -1; face <= 1; face += 2)
                {
                    var pos = alongX
                        ? new Vector3(centre.X + along, y, centre.Y + face * (half - Inset))
                        : new Vector3(centre.X + face * (half - Inset), y, centre.Y + along);

                    var size = alongX
                        ? new Vector3(width * 0.28f, WindowHeight, 0.16f)
                        : new Vector3(0.16f, WindowHeight, width * 0.28f);

                    AddBox($"{name}_win_{s}_{k}_{face}", pos, size, "#1b2026", collides: false);
                }
            }
        }
    }

    /// <summary>
    /// A lit fascia sign over the door. Strip malls are mostly signage, and a saturated
    /// board is what tells the eye which of forty identical boxes is the shop.
    /// </summary>
    private void BuildSign(string name, DoorSpec door, float wallHeight, SignSpec sign)
    {
        var y = Mathf.Min(wallHeight - 1.1f, 4.2f);
        var board = new MeshInstance3D
        {
            Name = $"{name}_sign",
            Mesh = new BoxMesh { Size = new Vector3(sign.Width, 1.6f, 0.35f) },
            Position = new Vector3(door.At[0], y, door.At[1] + 0.25f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(sign.Colour ?? "#c9452f"),
                // Emissive so it reads from across the lot and at dusk.
                EmissionEnabled = true,
                Emission = new Color(sign.Colour ?? "#c9452f"),
                EmissionEnergyMultiplier = 0.6f,
                Roughness = 0.6f,
            },
        };
        AddChild(board);

        AddBox($"{name}_sign_trim",
            new Vector3(door.At[0], y + 0.95f, door.At[1] + 0.25f),
            new Vector3(sign.Width + 0.4f, 0.3f, 0.45f), "#2b2b28", collides: false);
    }

    private static readonly string[] CarPaint =
    {
        "#8d3f33", "#2f4a63", "#6d7a5c", "#8a8f96", "#b8ab8a",
        "#3a3f45", "#7a5a3c", "#546b6b",
    };

    /// <summary>
    /// A parked car: body, cabin, wheels. Not a good car - but a lot with cars in it
    /// reads as a place where people are, and an empty one reads as a car park in a
    /// disaster film.
    /// </summary>
    private void AddCar(string name, Lot lot, int index)
    {
        // Deterministic placement: rows across the lot's long axis, nose-in.
        var alongX = lot.Size[0] >= lot.Size[1];
        var span = alongX ? lot.Size[0] : lot.Size[1];
        var slots = Mathf.Max(1, Mathf.FloorToInt(span / 3.2f));
        var slot = index % slots;

        var offset = (slot + 0.5f) / slots * span - span / 2f;
        var depth = ((index / slots) % 2 == 0 ? -1f : 1f) * (alongX ? lot.Size[1] : lot.Size[0]) * 0.22f;

        var centre = alongX
            ? new Vector3(lot.Pos[0] + offset, 0f, lot.Pos[1] + depth)
            : new Vector3(lot.Pos[0] + depth, 0f, lot.Pos[1] + offset);

        var paint = Material(CarPaint[(index * 3 + (int)Mathf.Abs(lot.Pos[0])) % CarPaint.Length]);
        var glass = Material("#25303a");
        var rubber = Material("#1b1b1c");

        var body = new StaticBody3D { Name = name, Position = centre, CollisionLayer = 1, CollisionMask = 0 };

        var length = alongX ? 2.0f : 4.4f;
        var width = alongX ? 4.4f : 2.0f;

        void Part(Vector3 offsetPos, Vector3 size, StandardMaterial3D material)
        {
            body.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                Position = offsetPos,
                MaterialOverride = material,
            });
        }

        Part(new Vector3(0f, 0.7f, 0f), new Vector3(width, 0.85f, length), paint);
        Part(new Vector3(0f, 1.35f, 0f),
            new Vector3(width * 0.72f, 0.62f, length * 0.72f), glass);

        for (var wx = -1; wx <= 1; wx += 2)
        {
            for (var wz = -1; wz <= 1; wz += 2)
            {
                Part(new Vector3(wx * width * 0.42f, 0.33f, wz * length * 0.34f),
                    new Vector3(alongX ? 0.28f : 0.7f, 0.62f, alongX ? 0.7f : 0.28f), rubber);
            }
        }

        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(width, 1.5f, length) },
            Position = new Vector3(0f, 0.75f, 0f),
        });

        AddChild(body);
    }

    /// <summary>
    /// Lamp posts down both pavements. Cheap, and the single biggest cue that a grey
    /// corridor is a street rather than a canyon.
    /// </summary>
    private void BuildStreetFurniture(Road road)
    {
        if (!road.Furniture)
            return;

        var from = new Vector3(road.From[0], 0f, road.From[1]);
        var to = new Vector3(road.To[0], 0f, road.To[1]);
        var length = from.DistanceTo(to);
        var horizontal = Mathf.Abs(to.X - from.X) > Mathf.Abs(to.Z - from.Z);
        var offset = road.Width / 2f + PavementWidth * 0.6f;

        var spacing = 18f;
        var count = Mathf.Max(1, Mathf.FloorToInt(length / spacing));

        for (var i = 0; i <= count; i++)
        {
            var point = from.Lerp(to, (float)i / count);

            for (var side = -1; side <= 1; side += 2)
            {
                var basePos = horizontal
                    ? point with { X = point.X, Z = point.Z + side * offset }
                    : point with { X = point.X + side * offset, Z = point.Z };

                AddBox($"lamp_{i}_{side}_{(int)point.X}_{(int)point.Z}",
                    basePos with { Y = 2.4f },
                    new Vector3(0.18f, 4.8f, 0.18f), "#3f4038", collides: false);

                AddBox($"lamphead_{i}_{side}_{(int)point.X}_{(int)point.Z}",
                    basePos with { Y = 4.9f },
                    new Vector3(0.7f, 0.22f, 0.35f), "#c9c2a8", collides: false);
            }
        }
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

    /// <summary>
    /// Resolves a surface name to a material. A name matching a folder under
    /// assets/textures gets the real CC0 texture set; anything else falls back to a
    /// flat colour, so a typo shows up as an obviously wrong surface rather than a
    /// crash.
    /// </summary>
    private StandardMaterial3D Material(string surface)
    {
        if (_materials.TryGetValue(surface, out var cached))
            return cached;

        var material = surface.StartsWith('#')
            ? FlatMaterial(surface)
            : TexturedMaterial(surface) ?? FlatMaterial("#8a8a80");

        _materials[surface] = material;
        return material;
    }

    private static StandardMaterial3D FlatMaterial(string hex) => new()
    {
        AlbedoColor = new Color(hex),
        Roughness = 0.92f,
        MetallicSpecular = 0.1f,
    };

    /// <summary>
    /// Builds a triplanar material from the fetched maps.
    ///
    /// Triplanar because the district is boxes of wildly different sizes generated at
    /// runtime - there are no UVs to unwrap, and world-space projection makes a 24 m
    /// wall and a 2 m door post share the same brick scale for free.
    /// </summary>
    private static StandardMaterial3D? TexturedMaterial(string name)
    {
        var diffuse = LoadMap(name, "diff");
        if (diffuse is null)
            return null;

        var material = new StandardMaterial3D
        {
            AlbedoTexture = diffuse,
            // Albedo multiplies the texture. Poly Haven scans are neutral-grey studio
            // captures; the town is meant to be sun-bleached and dusty, so everything
            // gets warmed here rather than by grading the whole frame.
            AlbedoColor = TintFor(name),
            Uv1Triplanar = true,
            Uv1Scale = Vector3.One * TextureScaleFor(name),
            Roughness = 1f,
            MetallicSpecular = 0.12f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };

        if (LoadMap(name, "nor_gl") is { } normal)
        {
            material.NormalEnabled = true;
            material.NormalTexture = normal;
            material.NormalScale = 0.8f;
        }

        if (LoadMap(name, "rough") is { } rough)
        {
            material.RoughnessTexture = rough;
            material.RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Red;
        }

        return material;
    }

    /// <summary>
    /// Tiles per metre. Triplanar UVs are world-space, so this is literally "how many
    /// times the image repeats across a metre" - a brick course has to land near 7 cm
    /// or the whole district reads as a doll's house built from giant blocks.
    /// </summary>
    private static float TextureScaleFor(string name) => name switch
    {
        "brick_wall_006" => 1.0f,
        "red_brick_03" => 1.0f,
        "painted_plaster_wall" => 0.35f,
        "concrete_layers_02" => 0.3f,
        "concrete_wall_008" => 0.45f,
        "corrugated_iron_02" => 0.5f,
        "asphalt_02" => 0.35f,
        "asphalt_04" => 0.3f,
        "concrete_floor_worn_001" => 0.5f,
        "concrete_pavers_02" => 0.35f,
        "pavement_02" => 0.5f,
        "dry_ground_rocks" => 0.6f,
        "sand_02" => 0.8f,
        "grass_medium_01" => 2.0f,
        "wood_planks_grey" => 1.2f,
        _ => 0.6f,
    };

    /// <summary>
    /// Warm/desaturate multiplier per surface. Stucco goes sandy, concrete goes bone,
    /// asphalt stays cool so the roads read against the dirt.
    /// </summary>
    private static Color TintFor(string name) => name switch
    {
        "painted_plaster_wall" => new Color(1.0f, 0.88f, 0.7f),
        "concrete_layers_02" => new Color(0.98f, 0.92f, 0.82f),
        "concrete_pavers_02" => new Color(0.95f, 0.88f, 0.76f),
        "concrete_floor_worn_001" => new Color(0.96f, 0.9f, 0.78f),
        "corrugated_iron_02" => new Color(0.92f, 0.86f, 0.78f),
        "red_brick_03" => new Color(1.0f, 0.86f, 0.72f),
        "dry_ground_rocks" => new Color(1.0f, 0.82f, 0.58f),
        "sand_02" => new Color(1.0f, 0.9f, 0.68f),
        "asphalt_04" => new Color(0.86f, 0.84f, 0.82f),
        "asphalt_02" => new Color(0.86f, 0.84f, 0.82f),
        "wood_planks_grey" => new Color(1.0f, 0.9f, 0.76f),
        _ => Colors.White,
    };

    private static Texture2D? LoadMap(string name, string suffix)
    {
        var path = $"res://assets/textures/{name}/{name}_{suffix}_1k.jpg";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    // ------------------------------------------------------------------ schema

    private sealed class Layout
    {
        public string? Name { get; set; }
        public Dictionary<string, string>? Palette { get; set; }
        public GroundSpec? Ground { get; set; }
        public List<Road>? Roads { get; set; }
        public List<Lot>? Lots { get; set; }
        public List<Terrace>? Terraces { get; set; }
        public List<Building>? Buildings { get; set; }
        public List<Fixture>? Fixtures { get; set; }
    }

    private sealed class Road
    {
        public float[] From { get; set; } = { 0, 0 };
        public float[] To { get; set; } = { 0, 0 };
        public float Width { get; set; } = 10f;
        public bool Furniture { get; set; } = true;
    }

    private sealed class Lot
    {
        public string Name { get; set; } = "lot";
        public float[] Pos { get; set; } = { 0, 0 };
        public float[] Size { get; set; } = { 10, 10 };
        public string? Colour { get; set; }
        public int Cars { get; set; }
    }

    private sealed class GroundSpec
    {
        public float Size { get; set; } = 200f;
        public string? Colour { get; set; }
    }

    private sealed class Terrace
    {
        public string Name { get; set; } = "terrace";
        public float[] From { get; set; } = { 0, 0 };
        public float[] To { get; set; } = { 0, 0 };
        public float Depth { get; set; } = 12f;
        public float UnitWidth { get; set; } = 9f;
        public float Gap { get; set; } = 0.6f;
        public float MinHeight { get; set; } = 7f;
        public float MaxHeight { get; set; } = 14f;
        public string? Colour { get; set; }
        public string? CapColour { get; set; }
    }

    private sealed class Building
    {
        public string Name { get; set; } = "building";
        public float[] Pos { get; set; } = { 0, 0 };
        public float[] Size { get; set; } = { 10, 5, 10 };
        public string? Colour { get; set; }
        public DoorSpec? Door { get; set; }
        public SignSpec? Sign { get; set; }
    }

    private sealed class SignSpec
    {
        public float Width { get; set; } = 8f;
        public string? Colour { get; set; }
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
