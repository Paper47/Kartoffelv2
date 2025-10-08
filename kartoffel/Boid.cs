using Godot;
using System;

public partial class Boid : Node2D
{
	[Export] public float MaxSpeed = 220f;       // units/sec
	[Export] public float MaxForce = 260f;       // steering cap (units/sec^2)
	[Export] public float Perception = 70f;      // neighbor radius
	[Export] public float SeparationRadius = 35f;

	public Vector2 Velocity;
	private Vector2 _accel = Vector2.Zero;
	private static readonly Color _bodyColor = Colors.White;

	public override void _Ready()
	{
		// Random initial velocity
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		float angle = rng.Randf() * Mathf.Tau;
		Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (MaxSpeed * 0.5f);

		// Make sure we draw at least once
		QueueRedraw();
	}

	public override void _Draw()
	{
		// A tiny triangle pointing +X; we rotate the Node to face velocity
		Vector2[] pts =
		{
			new Vector2(10, 0),
			new Vector2(-8, 5),
			new Vector2(-8, -5),
		};
		DrawPolygon(pts, new Color[] { _bodyColor, _bodyColor, _bodyColor });
	}

	public void Integrate(float dt, Vector2 steering)
	{
		_accel += steering;

		// Euler integration
		Velocity += _accel * dt;

		// Cap speed
		float s = Velocity.Length();
		if (s > MaxSpeed)
			Velocity = Velocity * (MaxSpeed / s);

		GlobalPosition += Velocity * dt;

		// Face movement direction if we are moving
		if (Velocity.LengthSquared() > 0.0001f)
			Rotation = Velocity.Angle();

		_accel = Vector2.Zero;
	}
}
