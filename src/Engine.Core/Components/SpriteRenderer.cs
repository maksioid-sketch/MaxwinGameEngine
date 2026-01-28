using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Engine.Core.Math;

namespace Engine.Core.Components;

public sealed class SpriteRenderer : IComponent
{
    // Content key, e.g. "player"
    public string TextureKey { get; set; } = string.Empty;

    // Optional source rect for atlases; empty means full texture
    public IntRect SourceRect { get; set; } = new(0, 0, 0, 0);

    public Color4 Tint { get; set; } = Color4.White;

    // For 2D layering (draw order). Later you can replace with a real render queue.
    public int Layer { get; set; } = 0;

    // Pixels per world unit (simple scaling control)
    public float PixelsPerUnit { get; set; } = 100f;
}

