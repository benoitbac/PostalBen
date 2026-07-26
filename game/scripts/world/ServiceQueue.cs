using Godot;
using System.Collections.Generic;
using System.Linq;

namespace PostalBen.World;

/// <summary>
/// A line of people waiting to be served.
///
/// This is the obstruction the whole game is built around. Everything else - the errand
/// tokens, the notoriety, the weapons - exists so that the player has options about
/// *this*: wait your turn, push in, or make the queue stop being a problem.
///
/// The queue never refuses to serve Ben. It only ever makes him wait, which is what
/// keeps the patient route always viable.
/// </summary>
public partial class ServiceQueue : Area3D
{
    [Export] public int Capacity { get; set; } = 6;

    /// <summary>Metres between people in the line.</summary>
    [Export] public float Spacing { get; set; } = 1.15f;

    /// <summary>How long the person at the front is held before being served.</summary>
    [Export] public float ServiceSeconds { get; set; } = 6.5f;

    /// <summary>
    /// Direction the line extends *away* from the counter, in local space. The counter
    /// is at the queue's own origin.
    /// </summary>
    [Export] public Vector3 Direction { get; set; } = Vector3.Back;

    [Signal]
    public delegate void ServedEventHandler(Node3D who);

    private readonly List<Node3D> _line = new();
    private float _serviceTimer;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = (1 << 1) | (1 << 2); // player and npc

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        _line.RemoveAll(n => !IsInstanceValid(n));

        if (_line.Count == 0)
        {
            _serviceTimer = 0f;
            return;
        }

        _serviceTimer += (float)delta;
        if (_serviceTimer < ServiceSeconds)
            return;

        _serviceTimer = 0f;
        var front = _line[0];

        // Ben is never auto-served: being at the front is permission to use the till,
        // not the transaction itself. NPCs are served and walk away, which is what
        // makes the line advance.
        if (front is Player.BenController)
            return;

        _line.RemoveAt(0);
        EmitSignal(SignalName.Served, front);

        if (front is Npc.Npc npc)
            npc.LeaveQueue();
    }

    // ---------------------------------------------------------------- membership

    private void OnBodyEntered(Node3D body)
    {
        if (_line.Contains(body) || _line.Count >= Capacity)
            return;
        if (body is not Player.BenController && body is not Npc.Npc)
            return;

        _line.Add(body);

        if (body is Npc.Npc npc)
            npc.JoinQueue(this);
    }

    private void OnBodyExited(Node3D body)
    {
        if (!_line.Remove(body))
            return;

        if (body is Npc.Npc npc)
            npc.LeaveQueue();
    }

    // ---------------------------------------------------------------- queries

    /// <summary>Zero-based position in the line, or -1 if not queueing.</summary>
    public int PositionOf(Node3D who) => _line.IndexOf(who);

    public bool IsAtFront(Node3D who) => _line.Count > 0 && _line[0] == who;

    public int Length => _line.Count;

    /// <summary>Where the nth person in the line should stand, in world space.</summary>
    public Vector3 SlotPosition(int index) =>
        GlobalPosition + (GlobalTransform.Basis * Direction).Normalized() * (index * Spacing);

    /// <summary>
    /// Someone shoved past. Everyone still waiting notices, which is the cost of
    /// pushing in - cheap, but it is notoriety the patient player never pays.
    /// </summary>
    public void ReportQueueJumped(Node3D by)
    {
        if (_line.Count <= 1)
            return;

        GetNode<Systems.NotorietySystem>("/root/Notoriety")
            .Report(Systems.NotorietySystem.Incident.Rude, GlobalPosition, _line.Count);

        foreach (var waiting in _line.OfType<Npc.Npc>())
            waiting.Complain();
    }
}
