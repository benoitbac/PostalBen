using Godot;
using System;
using System.Collections.Generic;
using PostalBen.Systems;
using PostalBen.World;

namespace PostalBen.Test;

/// <summary>
/// Headless assertions over the design invariants that are easy to break silently.
///
/// Run: godot --headless -- --run-tests
/// Exits non-zero on the first failure so CI catches a regression in the pacifist path
/// or the dual-route errand design before it reaches a build.
/// </summary>
public partial class TestRunner : Node3D
{
    private readonly List<string> _failures = new();
    private int _checks;

    public override void _Ready()
    {
        _ = RunAll();
    }

    private async System.Threading.Tasks.Task RunAll()
    {
        GD.Print("=== PostalBen invariant tests ===");

        RunSafely(nameof(PayingCompletesTheMilkErrand), PayingCompletesTheMilkErrand);
        RunSafely(nameof(TheftAlsoCompletesTheMilkErrand), TheftAlsoCompletesTheMilkErrand);
        RunSafely(nameof(CompletingDayOneRequiresNoViolence), CompletingDayOneRequiresNoViolence);
        RunSafely(nameof(TheftCostsNotorietyButNotAManhunt), TheftCostsNotorietyButNotAManhunt);
        RunSafely(nameof(NotorietyDecaysBackToCalm), NotorietyDecaysBackToCalm);
        RunSafely(nameof(OneKillIsNotAManhunt), OneKillIsNotAManhunt);
        RunSafely(nameof(BankRefusesWithoutTheForm), BankRefusesWithoutTheForm);
        RunSafely(nameof(EveryVoLineHasASubtitleInBothLanguages), EveryVoLineHasASubtitleInBothLanguages);
        RunSafely(nameof(FormatPlaceholdersAreDotNetStyle), FormatPlaceholdersAreDotNetStyle);
        RunSafely(nameof(KillingABystanderEndsThePacifistRun), KillingABystanderEndsThePacifistRun);
        RunSafely(nameof(CorpsesStopBehavingLikePeople), CorpsesStopBehavingLikePeople);
        RunSafely(nameof(BeingArrestedEndsTheDayWithoutKillingBen), BeingArrestedEndsTheDayWithoutKillingBen);
        RunSafely(nameof(GoreSettingIsHonoured), GoreSettingIsHonoured);
        RunSafely(nameof(EverySoundEffectExists), EverySoundEffectExists);
        RunSafely(nameof(DistrictLayoutCoversEveryDayOneToken), DistrictLayoutCoversEveryDayOneToken);

        RunSafely(nameof(QueueSlotsAreOrderedAwayFromTheCounter), QueueSlotsAreOrderedAwayFromTheCounter);

        await NoFixtureIsBuriedInSolidGeometry();
        await QueueAlwaysAdvances();

        GD.Print($"=== {_checks - _failures.Count}/{_checks} passed ===");

        if (_failures.Count > 0)
        {
            foreach (var failure in _failures)
                GD.PrintErr($"FAIL  {failure}");
            GetTree().Quit(1);
            return;
        }

        GD.Print("All invariants hold.");
        GetTree().Quit(0);
    }

    // ---------------------------------------------------------------- invariants

    /// <summary>
    /// The honest route, driven through the real Till node rather than by poking
    /// ErrandLog - otherwise this asserts nothing about the shop.
    /// </summary>
    private void PayingCompletesTheMilkErrand()
    {
        var (errands, inventory) = FreshRun();
        errands.Add(MilkErrand());

        var pickup = new Pickup { Item = "milk", CompletionToken = "milk.acquired", Price = 3 };
        var till = new Till { Item = "milk", Price = 3, CompletionToken = "shop.settled" };
        AddChild(pickup);
        AddChild(till);

        errands.Notify("shop.entered");

        Check(till.CurrentPromptKey() == "ui.interact.queue",
            "an empty-handed Ben should see 'wait in line', not 'pay'");

        pickup.Trigger();
        Check(inventory.Has("milk"), "the pickup should put milk in the inventory");
        Check(!inventory.IsPaidFor("milk"), "taking is not paying");
        Check(till.CurrentPromptKey() == "ui.interact.pay",
            "holding unpaid milk should switch the till prompt to 'pay'");

        var before = inventory.Money;
        till.Trigger();

        Check(errands.IsComplete("milk"), "paying at the till should complete the milk errand");
        Check(inventory.Money == before - 3, "paying should cost the price of the milk");
        Check(inventory.IsPaidFor("milk"), "milk should be marked paid");

        pickup.QueueFree();
        till.QueueFree();
    }

    /// <summary>
    /// ADR-006: walking out unpaid must complete the same errand via the same token.
    /// Driven through the real ShopExit volume, so a regression in the theft route
    /// fails here rather than passing because the test emitted the token itself.
    /// </summary>
    private void TheftAlsoCompletesTheMilkErrand()
    {
        var (errands, inventory) = FreshRun();
        var notoriety = FreshNotoriety();
        errands.Add(MilkErrand());

        var pickup = new Pickup { Item = "milk", CompletionToken = "milk.acquired", Price = 3 };
        var exit = new ShopExit { Witnesses = 2 };
        AddChild(pickup);
        AddChild(exit);

        errands.Notify("shop.entered");
        pickup.Trigger();

        var before = inventory.Money;
        var ben = new Player.BenController();
        exit.EmitSignal(Area3D.SignalName.BodyExited, ben);

        Check(errands.IsComplete("milk"), "walking out with the milk should complete the errand");
        Check(inventory.Money == before, "theft should cost no money");
        Check(notoriety.Heat > 0f, "walking out unpaid in front of witnesses should be noticed");

        // Re-crossing the threshold must not re-report the same milk.
        var heatAfterFirst = notoriety.Heat;
        exit.EmitSignal(Area3D.SignalName.BodyExited, ben);
        Check(Mathf.IsEqualApprox(notoriety.Heat, heatAfterFirst),
            "the doorway should not re-report goods it has already settled");

        ben.Free();
        pickup.QueueFree();
        exit.QueueFree();
    }

    /// <summary>
    /// The pacifist claim in one assertion: a full milk run, honest or not, kills nobody.
    /// </summary>
    private void CompletingDayOneRequiresNoViolence()
    {
        var (errands, inventory) = FreshRun();
        var state = GetNode<GameState>("/root/GameState");
        var notoriety = FreshNotoriety();

        errands.Add(MilkErrand());

        var pickup = new Pickup { Item = "milk", CompletionToken = "milk.acquired", Price = 3 };
        var till = new Till { Item = "milk", Price = 3, CompletionToken = "shop.settled" };
        var clerk = new Clerk { RequiresItem = "form", ConsumesItem = "form", Pays = 240 };
        AddChild(pickup);
        AddChild(till);
        AddChild(clerk);

        errands.Notify("shop.entered");
        pickup.Trigger();
        till.Trigger();

        inventory.Take("form", paid: true);
        clerk.Trigger();

        Check(errands.IsComplete("milk"), "the milk errand should be finishable peacefully");
        Check(state.KillsToday == 0, "a peaceful run should record no kills");
        Check(state.PacifistToday, "a peaceful run should count as pacifist");
        Check(notoriety.Current == NotorietySystem.Level.Calm,
            $"paying for everything should leave notoriety Calm (was {notoriety.Current})");

        pickup.QueueFree();
        till.QueueFree();
        clerk.QueueFree();
    }

    private void TheftCostsNotorietyButNotAManhunt()
    {
        var notoriety = FreshNotoriety();
        notoriety.Report(NotorietySystem.Incident.Theft, Vector3.Zero, witnesses: 2);

        Check(notoriety.Heat > 0f, "theft should raise notoriety");
        Check(notoriety.Current < NotorietySystem.Level.Hunted,
            $"stealing milk should not trigger a manhunt (was {notoriety.Current})");
    }

    /// <summary>ADR-007: escalation must not be a one-way door.</summary>
    private void NotorietyDecaysBackToCalm()
    {
        var notoriety = FreshNotoriety();
        notoriety.Report(NotorietySystem.Incident.Assault, Vector3.Zero, witnesses: 1);
        Check(notoriety.Current > NotorietySystem.Level.Calm, "assault should raise the level");

        // 6 s grace + enough time at 2.2/s to bleed 30 heat off.
        for (var i = 0; i < 400; i++)
            notoriety._Process(0.1);

        Check(notoriety.Current == NotorietySystem.Level.Calm,
            $"notoriety should decay back to Calm (was {notoriety.Current}, heat {notoriety.Heat:F1})");
        Check(!notoriety.HasLastKnownPosition, "a cold trail should clear the last known position");
    }

    /// <summary>
    /// GDD 4.2: one loss of temper is survivable, two is a manhunt. Guards the 70-vs-100
    /// gap that the whole central choice hangs on.
    /// </summary>
    private void OneKillIsNotAManhunt()
    {
        var notoriety = FreshNotoriety();
        notoriety.Report(NotorietySystem.Incident.Kill, Vector3.Zero, witnesses: 1);
        Check(notoriety.Current == NotorietySystem.Level.Reported,
            $"a single witnessed kill should be Reported, not {notoriety.Current}");

        notoriety.Report(NotorietySystem.Incident.Kill, Vector3.Zero, witnesses: 1);
        Check(notoriety.Current == NotorietySystem.Level.Hunted,
            "a second kill should trigger the manhunt");
    }

    private void BankRefusesWithoutTheForm()
    {
        var (errands, inventory) = FreshRun();

        var clerk = new Clerk
        {
            RequiresItem = "form",
            ConsumesItem = "form",
            Pays = 240,
            CompletionToken = "cheque.cashed",
        };
        AddChild(clerk);

        var before = inventory.Money;
        clerk.Trigger();
        Check(inventory.Money == before, "clerk should not pay out without the form");

        inventory.Take("form", paid: true);
        clerk.Trigger();
        Check(inventory.Money == before + 240, "clerk should pay out once handed the form");
        Check(!inventory.Has("form"), "the form should be consumed");

        clerk.QueueFree();
    }

    /// <summary>
    /// A voice line with no subtitle is inaudible content in the other language.
    /// Translate() returns the key unchanged when there is no translation.
    /// </summary>
    private void EveryVoLineHasASubtitleInBothLanguages()
    {
        var keys = LoadVoKeys();
        Check(keys.Count > 0, "vo.csv should declare at least one line");

        var original = TranslationServer.GetLocale();
        foreach (var locale in new[] { "en", "fr" })
        {
            TranslationServer.SetLocale(locale);
            foreach (var key in keys)
            {
                if (TranslationServer.Translate(key).ToString() == key)
                    _failures.Add($"'{key}' has no {locale} subtitle");
            }
        }
        TranslationServer.SetLocale(original);
        _checks++;
    }

    /// <summary>
    /// Every token Day 1 waits on must be emitted by something in the district, or the
    /// errand is unfinishable and nothing in the build would say so.
    /// </summary>
    private void DistrictLayoutCoversEveryDayOneToken()
    {
        var required = new[]
        {
            "shop.entered", "milk.acquired", "shop.settled",
            "bank.entered", "bank.form_taken", "cheque.cashed",
        };

        using var file = FileAccess.Open("res://assets/district/day1.json", FileAccess.ModeFlags.Read);
        Check(file is not null, "district layout should be readable");
        if (file is null)
            return;

        var text = file.GetAsText();
        foreach (var token in required)
        {
            // shop.settled is emitted by the shopexit fixture, which carries no token
            // field of its own - its default is the token, so accept either signal.
            var present = text.Contains($"\"{token}\"", StringComparison.Ordinal)
                          || (token == "shop.settled" && text.Contains("\"shopexit\"", StringComparison.Ordinal));
            Check(present, $"no fixture in the district emits '{token}'");
        }
    }

    /// <summary>
    /// Builds the real district and checks that nothing the player has to touch is
    /// sealed inside world geometry.
    ///
    /// This exists because the first version of DistrictBuilder made every building a
    /// solid box, which put the milk and the till inside a massive cube. Everything
    /// else still passed - the errand logic was fine, the tokens were wired, the boot
    /// was clean - and Day 1 was simply impossible to finish. Nothing else in the
    /// pipeline looks at whether the world is walkable.
    /// </summary>
    private async System.Threading.Tasks.Task NoFixtureIsBuriedInSolidGeometry()
    {
        var district = new World.DistrictBuilder();
        AddChild(district);

        // Collision bodies register with the physics server on the next physics step,
        // so queries before this return nothing and the test would pass vacuously.
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var space = GetWorld3D().DirectSpaceState;
        var probed = 0;

        foreach (var node in district.GetChildren())
        {
            if (node is not World.Interactable interactable)
                continue;

            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = new SphereShape3D { Radius = 0.35f },
                Transform = new Transform3D(Basis.Identity, interactable.GlobalPosition),
                CollisionMask = 1, // world geometry only
            };

            var overlaps = space.IntersectShape(query, maxResults: 4);
            probed++;

            Check(overlaps.Count == 0,
                $"'{interactable.Name}' at {interactable.GlobalPosition} is inside solid " +
                $"geometry - the player can never reach it");
        }

        Check(probed > 0, "the district should build at least one interactable to probe");

        // Ben's spawn has to be standing room too.
        var spawnQuery = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = 0.4f },
            Transform = new Transform3D(Basis.Identity, new Vector3(-14f, 1f, 12f)),
            CollisionMask = 1,
        };
        Check(space.IntersectShape(spawnQuery, maxResults: 1).Count == 0,
            "Ben's spawn point is inside solid geometry");

        district.QueueFree();
    }

    /// <summary>
    /// Killing a bystander must cost the pacifist run and summon police - but a single
    /// kill must not immediately become a manhunt. This is the same 70-vs-100 gap as
    /// OneKillIsNotAManhunt, asserted through the actual damage path rather than by
    /// calling Report directly.
    /// </summary>
    private void KillingABystanderEndsThePacifistRun()
    {
        var notoriety = FreshNotoriety();
        var state = GetNode<GameState>("/root/GameState");
        state.StartDay(1);

        Check(state.PacifistToday, "a fresh day should start pacifist");

        var victim = new Npc.Npc();
        Npc.Humanoid.Build(victim, 1.7f, 1f, Colors.White, Colors.Black, Colors.Tan);
        AddChild(victim);

        victim.TakeDamage(1000f, Vector3.Zero);

        Check(victim.IsDead, "enough damage should kill an NPC");
        Check(state.KillsToday == 1, $"the kill should be recorded (was {state.KillsToday})");
        Check(!state.PacifistToday, "a kill should end the pacifist day");
        Check(notoriety.Current == NotorietySystem.Level.Reported,
            $"one killed bystander should be Reported, not {notoriety.Current}");

        victim.QueueFree();
    }

    /// <summary>
    /// A dead NPC must stop being a person: no physics, no collision, no further AI.
    /// A corpse that keeps walking is the kind of thing that only shows up in a video.
    /// </summary>
    private void CorpsesStopBehavingLikePeople()
    {
        var victim = new Npc.Npc();
        Npc.Humanoid.Build(victim, 1.7f, 1f, Colors.White, Colors.Black, Colors.Tan);
        AddChild(victim);

        victim.TakeDamage(1000f, Vector3.Back);

        Check(victim.IsDead, "the NPC should be dead");
        Check(victim.CollisionLayer == 0, "a corpse should leave the collision layers");
        Check(!victim.IsPhysicsProcessing(), "a corpse should stop running its AI");

        victim.QueueFree();
    }

    /// <summary>
    /// Surrender has to work. If arrest were unreachable, the only exit from a manhunt
    /// would be dying, and the whole "you can walk it back" pillar would be a lie.
    /// </summary>
    private void BeingArrestedEndsTheDayWithoutKillingBen()
    {
        var state = GetNode<GameState>("/root/GameState");
        var notoriety = FreshNotoriety();
        state.StartDay(1);
        notoriety.Report(NotorietySystem.Incident.Kill, Vector3.Zero, 3);

        // The real scene, not a bare CharacterBody3D: BenController._Ready expects its
        // Head/Camera3D/collider children, and a hand-built stub throws on every one of
        // them, burying any genuine error in the noise.
        var scene = GD.Load<PackedScene>("res://scenes/Ben.tscn");
        Check(scene is not null, "Ben.tscn should load");
        if (scene is null)
            return;

        var ben = scene.Instantiate<Player.BenController>();
        AddChild(ben);

        Check(!ben.IsDetained, "Ben should not start detained");
        Check(ben.SecondsSinceAttack > 1f, "an idle Ben should read as not resisting");

        ben.NotifyAttacked();
        Check(ben.SecondsSinceAttack < 0.1f, "attacking should reset the resist timer");

        ben.Detain();

        Check(ben.IsDetained, "Detain should take Ben into custody");
        Check(!ben.IsDead, "an arrest is not a death");

        state.EndDay();
        Check(!state.DayRunning, "an arrest should end the day");

        ben.QueueFree();
    }

    /// <summary>
    /// The gore setting is a real setting. Off means off - a player who turns it down
    /// and still gets blood has been ignored.
    /// </summary>
    private void GoreSettingIsHonoured()
    {
        var cfg = new ConfigFile();
        cfg.Load(LocaleManager.ConfigPath);
        var original = cfg.GetValue("ui", "gore", 0);

        foreach (var level in new[] { World.Gore.Level.Full, World.Gore.Level.Reduced, World.Gore.Level.Off })
        {
            cfg.SetValue("ui", "gore", (int)level);
            cfg.Save(LocaleManager.ConfigPath);
            World.Gore.Invalidate();

            Check(World.Gore.Setting == level,
                $"gore setting should read back as {level}, got {World.Gore.Setting}");
        }

        cfg.SetValue("ui", "gore", original);
        cfg.Save(LocaleManager.ConfigPath);
        World.Gore.Invalidate();
    }

    /// <summary>
    /// The queue is the game's central obstruction, so the thing that must never break
    /// is that it *always resolves*. Waiting has to work, or the patient route dies and
    /// with it the whole design.
    /// </summary>
    private async System.Threading.Tasks.Task QueueAlwaysAdvances()
    {
        var queue = new World.ServiceQueue
        {
            ServiceSeconds = 0.05f,
            Spacing = 1.2f,
            Direction = Vector3.Back,
        };
        queue.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3, 3, 9) } });
        AddChild(queue);

        var waiting = new List<Npc.Npc>();
        for (var i = 0; i < 4; i++)
        {
            var npc = new Npc.Npc { Position = queue.SlotPosition(i) };
            Npc.Humanoid.Build(npc, 1.7f, 1f, Colors.White, Colors.Black, Colors.Tan);
            AddChild(npc);
            waiting.Add(npc);
        }

        // Two physics frames for the area to notice the bodies inside it.
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var enrolled = queue.Length;
        Check(enrolled > 0, "walking into the lane should enrol people in the queue");

        // Let it run. Every service tick must remove exactly the person at the front.
        for (var tick = 0; tick < 40 && queue.Length > 0; tick++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Check(queue.Length < enrolled,
            $"the queue must drain over time (started {enrolled}, still {queue.Length})");

        queue.QueueFree();
        foreach (var npc in waiting)
            npc.QueueFree();
    }

    /// <summary>
    /// Slots must march away from the counter in order, or people stack on one spot and
    /// the line reads as a scrum.
    /// </summary>
    private void QueueSlotsAreOrderedAwayFromTheCounter()
    {
        var queue = new World.ServiceQueue { Spacing = 1.5f, Direction = Vector3.Back };
        AddChild(queue);

        var first = queue.SlotPosition(0);
        var second = queue.SlotPosition(1);
        var fifth = queue.SlotPosition(4);

        Check(first.DistanceTo(second) > 1.4f, "consecutive slots should be a stride apart");
        Check(queue.GlobalPosition.DistanceTo(fifth) > queue.GlobalPosition.DistanceTo(second),
            "later slots should be further from the counter");

        queue.QueueFree();
    }

    /// <summary>
    /// Every sound effect the code asks for must exist on disk.
    ///
    /// A missing effect fails silently at runtime - the game just goes quiet in one
    /// place, in a way nobody notices until someone plays with headphones on. The ids
    /// are string literals scattered across the codebase, so this is the only thing
    /// that would catch a typo.
    /// </summary>
    private void EverySoundEffectExists()
    {
        // Kept in step with the PlayAt/Play call sites by hand; there are few enough
        // that a list beats reflection over string literals.
        var required = new[]
        {
            "step_1", "step_2", "step_3", "step_4",
            "gunshot", "swing", "impact", "bodyfall",
            "interact", "till", "ambience",
        };

        foreach (var id in required)
        {
            var path = $"res://assets/audio/sfx/{id}.wav";
            Check(ResourceLoader.Exists(path), $"sound effect '{id}' is missing at {path}");
        }

        // And the loader must actually hand back a stream, not just report the file.
        var probe = GD.Load<AudioStream>("res://assets/audio/sfx/gunshot.wav");
        Check(probe is not null, "gunshot.wav should load as an AudioStream");
    }

    /// <summary>
    /// Placeholders must be .NET style. Godot's own formatting uses %s and %d, and it is
    /// the natural thing to type into a translation CSV - but the C# side formats with
    /// string.Format, which leaves them untouched. The result ships as a HUD reading
    /// literally "%d EUR", in both languages, with no error anywhere.
    /// </summary>
    private void FormatPlaceholdersAreDotNetStyle()
    {
        using var file = FileAccess.Open("res://localization/ui.csv", FileAccess.ModeFlags.Read);
        Check(file is not null, "ui.csv should be readable");
        if (file is null)
            return;

        file.GetCsvLine(); // header
        var checkedRows = 0;

        while (!file.EofReached())
        {
            var row = file.GetCsvLine();
            if (row.Length < 3 || row[0].Length == 0)
                continue;

            checkedRows++;
            for (var col = 1; col < row.Length; col++)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(row[col], "%[sdfx]"))
                {
                    _failures.Add(
                        $"'{row[0]}' uses a GDScript placeholder ('{row[col]}') - " +
                        "string.Format needs {0}, so this would render literally");
                }
            }
        }

        _checks++;
        Check(checkedRows > 0, "ui.csv should contain rows to check");
    }

    // ---------------------------------------------------------------- helpers

    private static ErrandLog.Errand MilkErrand() => new()
    {
        Id = "milk",
        NameKey = "ui.errand.milk.name",
        DescriptionKey = "ui.errand.milk.desc",
        Stages = new[]
        {
            new ErrandLog.Stage { CompletionToken = "shop.entered", HintKey = "ui.errand.milk.desc" },
            new ErrandLog.Stage { CompletionToken = "milk.acquired", HintKey = "ui.interact.pickup" },
            new ErrandLog.Stage { CompletionToken = "shop.settled", HintKey = "ui.interact.pay" },
        },
    };

    private (ErrandLog, Inventory) FreshRun()
    {
        var errands = GetNode<ErrandLog>("/root/ErrandLog");
        var inventory = GetNode<Inventory>("/root/Inventory");
        errands.ClearForNewDay();
        inventory.ClearForNewDay();
        return (errands, inventory);
    }

    private NotorietySystem FreshNotoriety()
    {
        var notoriety = GetNode<NotorietySystem>("/root/Notoriety");
        notoriety.Reset();
        return notoriety;
    }

    private static List<string> LoadVoKeys()
    {
        var keys = new List<string>();
        using var file = FileAccess.Open("res://localization/vo.csv", FileAccess.ModeFlags.Read);
        if (file is null)
            return keys;

        file.GetCsvLine(); // header
        while (!file.EofReached())
        {
            var row = file.GetCsvLine();
            if (row.Length > 0 && row[0].Length > 0)
                keys.Add(row[0]);
        }
        return keys;
    }

    private void RunSafely(string name, Action test)
    {
        try
        {
            test();
        }
        catch (Exception e)
        {
            _checks++;
            _failures.Add($"{name} threw: {e.Message}");
        }
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            _failures.Add(message);
    }
}
