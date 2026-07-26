using Godot;

namespace PostalBen.Npc;

/// <summary>
/// Builds the blocky placeholder body shared by civilians and police, and hands back
/// the limb pivots the walk cycle drives.
///
/// Separated from the spawners because the day a rigged model arrives, this is the only
/// file that changes.
/// </summary>
public static class Humanoid
{
    public readonly record struct Rig(
        Node3D Body, Node3D ArmL, Node3D ArmR, Node3D LegL, Node3D LegR, MeshInstance3D Torso);

    public static Rig Build(Node3D owner, float height, float build,
        Color shirt, Color trousers, Color skin)
    {
        var shirtMat = Flat(shirt);
        var trouserMat = Flat(trousers);
        var skinMat = Flat(skin);

        owner.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.3f, Height = height },
            Position = new Vector3(0f, height / 2f, 0f),
        });

        // Body pivot: the walk cycle bobs and leans this, so the collider stays upright.
        var body = new Node3D { Name = "Body" };
        owner.AddChild(body);

        var shoulderY = height * 0.72f;
        var hipY = height * 0.5f;
        var halfWidth = 0.21f * build;

        var torso = Part("Torso", new Vector3(0.42f * build, height * 0.30f, 0.24f * build),
            new Vector3(0f, shoulderY - height * 0.15f, 0f), shirtMat);
        body.AddChild(torso);

        body.AddChild(Part("Head", new Vector3(0.21f, 0.24f, 0.2f),
            new Vector3(0f, height * 0.93f, 0f), skinMat));
        body.AddChild(Part("Neck", new Vector3(0.12f, 0.07f, 0.12f),
            new Vector3(0f, height * 0.81f, 0f), skinMat));

        Node3D? armL = null, armR = null, legL = null, legR = null;

        foreach (var side in new[] { -1f, 1f })
        {
            var arm = new Node3D
            {
                Name = side < 0 ? "ArmL" : "ArmR",
                Position = new Vector3(side * (halfWidth + 0.09f), shoulderY, 0f),
            };
            arm.AddChild(Part("Limb", new Vector3(0.11f, height * 0.32f, 0.13f),
                new Vector3(0f, -height * 0.16f, 0f), shirtMat));
            body.AddChild(arm);

            var leg = new Node3D
            {
                Name = side < 0 ? "LegL" : "LegR",
                Position = new Vector3(side * 0.11f, hipY, 0f),
            };
            leg.AddChild(Part("Limb", new Vector3(0.15f, height * 0.46f, 0.17f),
                new Vector3(0f, -height * 0.23f, 0f), trouserMat));
            body.AddChild(leg);

            if (side < 0) { armL = arm; legL = leg; } else { armR = arm; legR = leg; }
        }

        return new Rig(body, armL!, armR!, legL!, legR!, torso);
    }

    private static MeshInstance3D Part(string name, Vector3 size, Vector3 pos, StandardMaterial3D material) => new()
    {
        Name = name,
        Mesh = new BoxMesh { Size = size },
        Position = pos,
        MaterialOverride = material,
    };

    private static StandardMaterial3D Flat(Color colour) => new()
    {
        AlbedoColor = colour,
        Roughness = 0.88f,
    };
}
