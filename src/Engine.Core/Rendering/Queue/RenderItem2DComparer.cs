namespace Engine.Core.Rendering.Queue;

public sealed class RenderItem2DComparer : IComparer<RenderItem2D>
{
    public static readonly RenderItem2DComparer Instance = new();

    public int Compare(RenderItem2D a, RenderItem2D b)
    {
        // Primary: layer (low -> back, high -> front)
        int c = a.Layer.CompareTo(b.Layer);
        if (c != 0) return c;

        // Secondary: textureKey (helps batching later)
        c = string.CompareOrdinal(a.TextureKey, b.TextureKey);
        if (c != 0) return c;

        // Tertiary: stable-ish ordering by Z then Y then X
        c = a.WorldPosition.Z.CompareTo(b.WorldPosition.Z);
        if (c != 0) return c;

        c = a.WorldPosition.Y.CompareTo(b.WorldPosition.Y);
        if (c != 0) return c;

        return a.WorldPosition.X.CompareTo(b.WorldPosition.X);
    }
}
