using Godot;
using System.Collections.Generic;
using PostalBen.Player;
using PostalBen.Systems;

namespace PostalBen.UI;

/// <summary>
/// The in-world HUD: interaction prompt, clock, money, notoriety, and the errand list.
///
/// Every string here goes through Tr(). The panel is rebuilt on locale change rather
/// than caching rendered text, so switching language mid-game updates it immediately.
/// </summary>
public partial class Hud : CanvasLayer
{
    private Label _prompt = null!;
    private Label _status = null!;
    private Label _vitals = null!;
    private Label _weapon = null!;
    private Label _ammo = null!;
    private PanelContainer _logPanel = null!;
    private VBoxContainer _logList = null!;

    private BenController? _ben;
    private bool _isFirearm;

    private ErrandLog _errands = null!;
    private GameState _state = null!;
    private NotorietySystem _notoriety = null!;
    private Inventory _inventory = null!;
    private LocaleManager _locale = null!;

    public override void _Ready()
    {
        Layer = 50;

        _locale = GetNode<LocaleManager>("/root/Locale");
        _errands = GetNode<ErrandLog>("/root/ErrandLog");
        _state = GetNode<GameState>("/root/GameState");
        _notoriety = GetNode<NotorietySystem>("/root/Notoriety");
        _inventory = GetNode<Inventory>("/root/Inventory");

        BuildStatus();
        BuildPrompt();
        BuildLoadout();
        BuildErrandPanel();

        _errands.ErrandAdded += _ => RefreshLog();
        _errands.StageAdvanced += (_, _) => RefreshLog();
        _errands.ErrandCompleted += _ => RefreshLog();
        _locale.LocaleChanged += _ => RefreshLog();

        var player = GetTree().GetFirstNodeInGroup("player");
        _ben = player as BenController;

        var interactor = player?.GetNodeOrNull<Interactor>("Head/Interactor");
        if (interactor is not null)
            interactor.TargetChanged += OnTargetChanged;
        else
            GD.PushWarning("[HUD] no Interactor found - the interaction prompt will stay hidden");

        var weapons = player?.GetNodeOrNull<WeaponSystem>("Head/Camera3D/WeaponSystem");
        if (weapons is not null)
        {
            weapons.WeaponChanged += OnWeaponChanged;
            weapons.AmmoChanged += OnAmmoChanged;
            OnWeaponChanged(weapons.Held.Id);
        }
        else
        {
            GD.PushWarning("[HUD] no WeaponSystem found - the loadout readout will stay blank");
        }

        RefreshLog();
    }

    public override void _Process(double delta)
    {
        _status.Text = string.Join("   ",
            _state.ClockText,
            string.Format(_locale.Culture, Tr("ui.hud.money"), _inventory.Money),
            Tr(_notoriety.CurrentLabelKey));

        if (_ben is not null)
            _vitals.Text = $"{Mathf.CeilToInt(_ben.Health)} / {Mathf.CeilToInt(_ben.MaxHealth)}";
    }

    private void OnWeaponChanged(string id)
    {
        _weapon.Text = Tr(id switch
        {
            "shovel" => "ui.weapon.shovel",
            "pistol" => "ui.weapon.pistol",
            _ => "ui.hud.no_weapon",
        });
        _isFirearm = id == "pistol";
        _ammo.Visible = _isFirearm;
    }

    private void OnAmmoChanged(int loaded, int spare)
    {
        if (!_isFirearm)
            return;
        _ammo.Text = string.Format(_locale.Culture, Tr("ui.hud.ammo_count"), loaded, spare);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("errand_log"))
            _logPanel.Visible = !_logPanel.Visible;
    }

    // ------------------------------------------------------------------ build

    private void BuildStatus()
    {
        _status = new Label { Name = "Status" };
        _status.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _status.GrowHorizontal = Control.GrowDirection.Begin;
        _status.Position = new Vector2(-340f, 22f);
        _status.CustomMinimumSize = new Vector2(300f, 0f);
        _status.HorizontalAlignment = HorizontalAlignment.Right;
        _status.AddThemeFontSizeOverride("font_size", 20);
        _status.AddThemeColorOverride("font_color", new Color("#e8e6dc"));
        AddChild(_status);
    }

    private void BuildPrompt()
    {
        _prompt = new Label { Name = "Prompt", Visible = false };
        _prompt.SetAnchorsPreset(Control.LayoutPreset.Center);
        _prompt.GrowHorizontal = Control.GrowDirection.Both;
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt.Position = new Vector2(-200f, 70f);
        _prompt.CustomMinimumSize = new Vector2(400f, 0f);
        _prompt.AddThemeFontSizeOverride("font_size", 22);
        _prompt.AddThemeColorOverride("font_color", new Color("#ffe9a3"));
        AddChild(_prompt);
    }

    /// <summary>
    /// Bottom-left block: health, held weapon, ammo. Postal's readout lives down there
    /// and it keeps the centre of the screen clear for the thing you are about to hit.
    /// </summary>
    private void BuildLoadout()
    {
        var column = new VBoxContainer { Name = "Loadout" };
        column.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        column.GrowVertical = Control.GrowDirection.Begin;
        column.Position = new Vector2(38f, -132f);
        column.AddThemeConstantOverride("separation", 2);
        AddChild(column);

        _vitals = Line(30, "#e8b0a0");
        _weapon = Line(21, "#e8e6dc");
        _ammo = Line(19, "#c9a227");
        _ammo.Visible = false;

        column.AddChild(_vitals);
        column.AddChild(_weapon);
        column.AddChild(_ammo);
    }

    private static Label Line(int size, string colour)
    {
        var label = new Label();
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(colour));
        return label;
    }

    private void BuildErrandPanel()
    {
        _logPanel = new PanelContainer { Name = "ErrandLog", Visible = false };
        _logPanel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _logPanel.Position = new Vector2(36f, 36f);
        _logPanel.CustomMinimumSize = new Vector2(420f, 0f);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.06f, 0.88f),
            BorderColor = new Color("#34382a"),
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        };
        style.SetBorderWidthAll(1);
        _logPanel.AddThemeStyleboxOverride("panel", style);

        _logList = new VBoxContainer();
        _logList.AddThemeConstantOverride("separation", 10);
        _logPanel.AddChild(_logList);
        AddChild(_logPanel);
    }

    // ------------------------------------------------------------------ update

    private void OnTargetChanged(string promptKey)
    {
        if (string.IsNullOrEmpty(promptKey))
        {
            _prompt.Visible = false;
            return;
        }

        // ui.hud.interact_prompt is "[E] %s" so the key binding stays outside the
        // translated verb and translators only ever see the verb.
        _prompt.Text = string.Format(_locale.Culture, Tr("ui.hud.interact_prompt"), Tr(promptKey));
        _prompt.Visible = true;
    }

    private void RefreshLog()
    {
        foreach (var child in _logList.GetChildren())
            child.QueueFree();

        _logList.AddChild(Heading(Tr("ui.errand.log_title")));

        if (_errands.Active.Count == 0)
        {
            _logList.AddChild(Body(Tr("ui.errand.empty"), muted: true));
            return;
        }

        foreach (var errand in _errands.Active)
        {
            _logList.AddChild(Body(Tr(errand.NameKey)));

            if (errand.CurrentStage is { } stage)
                _logList.AddChild(Body("   " + Tr(stage.HintKey), muted: true, size: 15));
        }
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", new Color("#c9a227"));
        return label;
    }

    private static Label Body(string text, bool muted = false, int size = 17)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380f, 0f),
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color",
            muted ? new Color("#9b9c8c") : new Color("#e8e6dc"));
        return label;
    }
}
