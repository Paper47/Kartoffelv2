using Godot;
using System;
using System.Collections.Generic;

public partial class BoidManager : Node2D
{
	[Export] public PackedScene BoidScene;
	[Export] public int Count = 200;

	// Simulation area (for soft bounds & initial spawn)
	[Export] public Vector2 AreaSize = new Vector2(1280, 720);

	// Behavior weights
	[Export] public float AlignWeight = 1.0f;
	[Export] public float CohesionWeight = 1.0f;
	[Export] public float SeparationWeight = 1.6f;
	[Export] public float BoundsWeight = 2.0f;

	// Bounds behavior
	[Export] public bool Wrap = false;     // true = wrap edges; false = steer back in
	[Export] public float BoundsMargin = 40f; // how far from edge before responding
	// --- Target (Player) chasing ---
	[Export] public NodePath PlayerPath;   // assign your Player in the Inspector (optional)
	[Export] public float TargetWeight = 1.8f;    // how strongly to chase
	[Export] public float LeadSeconds = 0.35f;    // how far ahead to lead the target
	[Export] public float KeepDistance = 24f;     // don't ram straight into the player
	[Export] public bool PursuitOnly = false;     // if true, ignore flocking and only chase

	private Node2D _player;        // cached player node
	private Vector2 _playerLastPos; // for velocity estimate if the Player doesn't expose one
	private Vector2 _playerVel;     // estimated or read velocity

	// Spatial hash
	[Export] public float CellSize = 64f;

	private readonly List<Boid> _boids = new();
	private readonly Dictionary<Vector2I, List<Boid>> _grid = new();

	private Rect2 _simRectGlobal;

	public override void _Ready()
	{
		_simRectGlobal = new Rect2(GlobalPosition - AreaSize * 0.5f, AreaSize);
		SpawnBoids();
		
		// Try explicit path first
		if (PlayerPath != null && !PlayerPath.IsEmpty)
			_player = GetNodeOrNull<Node2D>(PlayerPath);

		// Fallback: first node in "player" group
		if (_player == null)
		{
		var players = GetTree().GetNodesInGroup("player");
		if (players.Count > 0)
			_player = players[0] as Node2D;
		}

		if (_player == null)
			GD.PushWarning("BoidManager: No Player found. Assign PlayerPath or add your player to group \"player\".");
		else
			_playerLastPos = _player.Position;
	}

	private void SpawnBoids()
	{
		if (BoidScene == null)
		{
			GD.PushError("BoidScene is not assigned.");
			return;
		}

		var rng = new RandomNumberGenerator();
		rng.Randomize();

		for (int i = 0; i < Count; i++)
		{
			var b = BoidScene.Instantiate<Boid>();
			AddChild(b);

			// Random start position within the simulation rect
			float x = rng.RandfRange(_simRectGlobal.Position.X, _simRectGlobal.End.X);
			float y = rng.RandfRange(_simRectGlobal.Position.Y, _simRectGlobal.End.Y);
			b.GlobalPosition = new Vector2(x, y);

			_boids.Add(b);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		
		// --- Update player velocity estimate (or read from a known node type) ---
		if (_player != null)
		{
			// If your player is CharacterBody2D, prefer its Velocity property:
			if (_player is CharacterBody2D cb)
				_playerVel = cb.Velocity;
			else
			{
				// Fallback: estimate from position delta
				Vector2 cur = _player.Position;
				_playerVel = (cur - _playerLastPos) / Mathf.Max(dt, 0.00001f);
				_playerLastPos = cur;
			}
		}

		// 1) Rebuild spatial hash
		RebuildGrid();

		// 2) For each boid, compute steering from neighbors, add bounds steering, integrate
		foreach (var boid in _boids)
		{
			Vector2 steering = ComputeSteering(boid);
			steering += BoundsSteering(boid) * BoundsWeight;

			// Limit steering to boid.MaxForce
			steering += TargetSteering(boid, dt) * TargetWeight;

			steering = Limit(steering, boid.MaxForce);
			boid.Integrate(dt, steering);

			HandleWrappingOrClamp(boid);
		}
	}

	// ----------------- Spatial Hash -----------------
	private Vector2I CellFor(Vector2 pos)
	{
		return new Vector2I(
			Mathf.FloorToInt(pos.X / CellSize),
			Mathf.FloorToInt(pos.Y / CellSize)
		);
	}

	private void RebuildGrid()
	{
		_grid.Clear();
		foreach (var b in _boids)
		{
			var cell = CellFor(b.GlobalPosition);
			if (!_grid.TryGetValue(cell, out var list))
			{
				list = new List<Boid>();
				_grid[cell] = list;
			}
			list.Add(b);
		}
	}

	private void GetNeighbors(Boid boid, float radius, List<Boid> outList)
	{
		outList.Clear();

		int range = Mathf.CeilToInt(radius / CellSize);
		var baseCell = CellFor(boid.GlobalPosition);

		for (int dy = -range; dy <= range; dy++)
		{
			for (int dx = -range; dx <= range; dx++)
			{
				var c = new Vector2I(baseCell.X + dx, baseCell.Y + dy);
				if (_grid.TryGetValue(c, out var bucket))
				{
					foreach (var other in bucket)
					{
						if (other == boid) continue;
						if (boid.GlobalPosition.DistanceSquaredTo(other.GlobalPosition) <= radius * radius)
							outList.Add(other);
					}
				}
			}
		}
	}

	// ----------------- Steering -----------------
	private readonly List<Boid> _neighbors = new();

	private Vector2 ComputeSteering(Boid boid)
	{
		GetNeighbors(boid, boid.Perception, _neighbors);

		if (_neighbors.Count == 0)
			return Vector2.Zero;

		Vector2 sumVel = Vector2.Zero;     // alignment
		Vector2 sumPos = Vector2.Zero;     // cohesion center
		Vector2 sumSep = Vector2.Zero;     // separation

		int alignCount = 0;
		int cohesionCount = 0;
		int sepCount = 0;

		float sepRadiusSq = boid.SeparationRadius * boid.SeparationRadius;

		foreach (var other in _neighbors)
		{
			// Alignment: average neighbor velocities
			sumVel += other.Velocity;
			alignCount++;

			// Cohesion: average neighbor positions
			sumPos += other.Position;
			cohesionCount++;

			// Separation: push away from too-close neighbors
			float dSq = boid.GlobalPosition.DistanceSquaredTo(other.Position);
			if (dSq < sepRadiusSq && dSq > 0.0001f)
			{
				Vector2 away = (boid.GlobalPosition - other.Position) / Mathf.Sqrt(dSq);
				sumSep += away;
				sepCount++;
			}
		}

		Vector2 align = Vector2.Zero;
		Vector2 cohesion = Vector2.Zero;
		Vector2 separation = Vector2.Zero;

		if (alignCount > 0)
		{
			Vector2 desired = (sumVel / alignCount);
			if (desired.LengthSquared() > 0.0001f)
				desired = desired.Normalized() * boid.MaxSpeed;
			align = desired - boid.Velocity;
		}

		if (cohesionCount > 0)
		{
			Vector2 center = sumPos / cohesionCount;
			Vector2 desired = (center - boid.GlobalPosition);
			if (desired.LengthSquared() > 0.0001f)
				desired = desired.Normalized() * boid.MaxSpeed;
			cohesion = desired - boid.Velocity;
		}

		if (sepCount > 0)
		{
			Vector2 desired = sumSep / sepCount;
			if (desired.LengthSquared() > 0.0001f)
				desired = desired.Normalized() * boid.MaxSpeed;
			separation = desired - boid.Velocity;
		}

		// Weight the three classic forces
		Vector2 steering =
			AlignWeight * align +
			CohesionWeight * cohesion +
			SeparationWeight * separation;

		return steering;
	}
	
	private Vector2 TargetSteering(Boid boid, float dt)
	{
		if (_player == null)
			return Vector2.Zero;

		Vector2 playerPos = _player.GlobalPosition;
		Vector2 predicted = playerPos + _playerVel * Mathf.Clamp(LeadSeconds, 0f, 2f);

		// Keep a little buffer so they orbit rather than “needle”
		Vector2 toPredicted = predicted - boid.GlobalPosition;
		float dist = toPredicted.Length();

		if (dist < 0.0001f)
			return Vector2.Zero;

		// If too close, bias away a bit to avoid piling onto the exact point
		if (dist < KeepDistance)
			toPredicted = toPredicted.Normalized() * (KeepDistance - dist) * -1f;

		Vector2 desired = toPredicted.Normalized() * boid.MaxSpeed;
		Vector2 steer = desired - boid.Velocity;
		return steer;
	}

	private Vector2 BoundsSteering(Boid boid)
	{
		if (Wrap)
			return Vector2.Zero;

		Vector2 steer = Vector2.Zero;
		Vector2 p = boid.GlobalPosition;

		// Left
		if (p.X < _simRectGlobal.Position.X + BoundsMargin)
			steer.X += boid.MaxSpeed;
		// Right
		if (p.X > _simRectGlobal.End.X - BoundsMargin)
			steer.X -= boid.MaxSpeed;
		// Top
		if (p.Y < _simRectGlobal.Position.Y + BoundsMargin)
			steer.Y += boid.MaxSpeed;
		// Bottom
		if (p.Y > _simRectGlobal.End.Y - BoundsMargin)
			steer.Y -= boid.MaxSpeed;

		if (steer.LengthSquared() > 0.0001f)
			steer = steer.Normalized() * boid.MaxSpeed - boid.Velocity;

		return steer;
	}

	private void HandleWrappingOrClamp(Boid boid)
	{
		if (Wrap)
		{
			Vector2 p = boid.GlobalPosition;
			if (p.X < _simRectGlobal.Position.X) p.X = _simRectGlobal.End.X;
			else if (p.X > _simRectGlobal.End.X) p.X = _simRectGlobal.Position.X;

			if (p.Y < _simRectGlobal.Position.Y) p.Y = _simRectGlobal.End.Y;
			else if (p.Y > _simRectGlobal.End.Y) p.Y = _simRectGlobal.Position.Y;

			boid.GlobalPosition = p;
		}
		else
		{
			// Keep boids inside the sim rect (softly corrected by BoundsSteering)
			Vector2 p = boid.GlobalPosition;
			p.X = Mathf.Clamp(p.X, _simRectGlobal.Position.X - 4, _simRectGlobal.End.X + 4);
			p.Y = Mathf.Clamp(p.Y, _simRectGlobal.Position.Y - 4, _simRectGlobal.End.Y + 4);
			boid.GlobalPosition = p;
		}
	}

	private static Vector2 Limit(Vector2 v, float max)
	{
		float l2 = v.LengthSquared();
		if (l2 > max * max)
			return v * (max / Mathf.Sqrt(l2));
		return v;
	}

	// Optional: visualize the simulation bounds in editor
	public override void _Draw()
	{
		DrawRect(_simRectGlobal, new Color(1,1,1,0.05f), filled: false);
		DrawRect(_simRectGlobal, new Color(1,1,1,0.2f), filled: false, width: 1.5f);
	}

	public override void _Process(double delta)
	{
		_simRectGlobal = new Rect2(GlobalPosition - AreaSize * 0.5f, AreaSize);
		QueueRedraw();
	}
}
