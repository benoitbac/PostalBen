using Godot;

namespace PostalBen.Systems;

/// <summary>
/// Entry point. Exists so autoloads finish their _Ready (locale resolved, mixer built,
/// voice pool allocated) before the first real scene asks anything of them.
/// </summary>
public partial class Boot : Node
{
    [Export] public string FirstScene { get; set; } = "res://scenes/Day1.tscn";

    public override void _Ready()
    {
        var locale = GetNode<LocaleManager>("/root/Locale");
        GD.Print($"PostalBen {ProjectSettings.GetSetting("application/config/version")} - locale '{locale.Current}'");

        CallDeferred(nameof(EnterFirstScene));
    }

    private void EnterFirstScene()
    {
        var err = GetTree().ChangeSceneToFile(FirstScene);
        if (err != Error.Ok)
            GD.PushError($"[Boot] could not load '{FirstScene}': {err}");
    }
}
