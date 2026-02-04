using System.Numerics;

namespace Engine.Core.Components;

public sealed class BoxCollider2D : IComponent
{
    public Vector2 Size { get; set; } = new(1f, 1f);
    public Vector2 Offset { get; set; } = Vector2.Zero;
    public bool IsTrigger { get; set; } = false;
}
