using Godot;
using System;

namespace PostalBen.Systems;

/// <summary>
/// Entry point. Exists so autoloads finish their _Ready (locale resolved, mixer built,
/// voice pool allocated) before the first real scene asks anything of them.
/// </summary>
public partial class Boot : Node
{
    [Export] public string FirstScene { get; set; } = "res://scenes/Day1.tscn";

    private const string TestScene = "res://scenes/Test.tscn";

    public override void _Ready()
    {
        var locale = GetNode<LocaleManager>("/root/Locale");
        GD.Print($"PostalBen {ProjectSettings.GetSetting("application/config/version")} - locale '{locale.Current}'");

        CallDeferred(nameof(EnterFirstScene));
    }

    private void EnterFirstScene()
    {
        // `godot --headless -- --run-tests` runs the invariant suite instead of the game.
        // Routing it through Boot means the tests get the same autoloads the game does.
        var target = Array.IndexOf(OS.GetCmdlineUserArgs(), "--run-tests") >= 0
            ? TestScene
            : FirstScene;

        var err = GetTree().ChangeSceneToFile(target);
        if (err != Error.Ok)
            GD.PushError($"[Boot] could not load '{target}': {err}");
    }
}
