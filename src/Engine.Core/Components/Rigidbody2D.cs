using System.Numerics;

namespace Engine.Core.Components;

public sealed class Rigidbody2D : IComponent
{
    public float Mass { get; set; } = 1f;
    public Vector2 Velocity { get; set; } = Vector2.Zero;
    public bool UseGravity { get; set; } = true;
    public float GravityScale { get; set; } = 1f;
    public float LinearDrag { get; set; } = 0f;
    public float Friction { get; set; } = 0.2f;
}
