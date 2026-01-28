using System.Numerics;
using Engine.Core.Math;
using Engine.Core.Rendering;
using Engine.Runtime.MonoGame.Assets;
using Microsoft.Xna.Framework.Graphics;

// aliases (important)
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;


namespace Engine.Runtime.MonoGame.Rendering;

public sealed class MonoGameRenderer2D : IRenderer2D
{
    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _sb;
    private readonly TextureStore _textures;

    private XnaMatrix _viewProj;
    private bool _begun;

    public MonoGameRenderer2D(GraphicsDevice graphicsDevice, TextureStore textures)
    {
        _gd = graphicsDevice;
        _sb = new SpriteBatch(graphicsDevice);
        _textures = textures;
    }

    public void Begin(Camera2D camera)
    {
        if (_begun) throw new InvalidOperationException("Renderer2D.Begin called twice.");

        camera.ViewportWidth = _gd.Viewport.Width;
        camera.ViewportHeight = _gd.Viewport.Height;

        // IMPORTANT:
        // SpriteBatch already handles projection; only pass a view/world transform here.
        var view = camera.GetViewMatrix();

        _sb.Begin(
            sortMode: SpriteSortMode.BackToFront,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: RasterizerState.CullNone,
            effect: null,
            transformMatrix: view.ToXna());

        _begun = true;
    }


    public void DrawSprite(
        string textureKey,
        Vector3 worldPosition,      // System.Numerics.Vector3
        Vector2 worldScale,         // System.Numerics.Vector2
        float rotationRadians,
        IntRect sourceRect,
        Color4 tint,
        int layer,
        float pixelsPerUnit)

    {
        if (!_begun) throw new InvalidOperationException("Call Begin() before DrawSprite().");

        var tex = _textures.Get(textureKey);

        XnaRectangle? src = null;
        if (!sourceRect.IsEmpty)
            src = new XnaRectangle(sourceRect.X, sourceRect.Y, sourceRect.W, sourceRect.H);

        // Convert world units -> pixels:
        // A sprite rendered at scale 1.0 will be (tex.Width / PPU, tex.Height / PPU) world units.
        var baseScaleX = (tex.Width / pixelsPerUnit) * worldScale.X;
        var baseScaleY = (tex.Height / pixelsPerUnit) * worldScale.Y;

        // SpriteBatch.Draw uses a destination scale relative to texture size if you use origin + scale overload.
        // We'll draw using position in world units, with scale in world units mapped to pixels via PPU:
        // easiest: draw in "world units" directly by scaling by (PPU/texSize) is messy.
        // Instead: treat world units as pixels by using transformMatrix. But we used an ortho in pixel space.
        // So: convert worldPosition (units) to pixels:
        var px = worldPosition.X * pixelsPerUnit;
        var py = worldPosition.Y * pixelsPerUnit;

        var pos = new Vector2(px, py);

        // Scale in pixels relative to texture size:
        var sx = (baseScaleX * pixelsPerUnit) / tex.Width;
        var sy = (baseScaleY * pixelsPerUnit) / tex.Height;

        // Center origin:
        var origin = src.HasValue
            ? new Vector2(src.Value.Width * 0.5f, src.Value.Height * 0.5f)
            : new Vector2(tex.Width * 0.5f, tex.Height * 0.5f);

        // Layer: SpriteBatch expects 0..1 (BackToFront sort). Map int layers.
        // Higher layer = drawn later (front). Clamp.
        float depth = System.Math.Clamp(layer / 1000f, 0f, 1f);

        _sb.Draw(
            texture: tex,
            position: pos,
            sourceRectangle: src,
            color: tint.ToXna(),
            rotation: rotationRadians,
            origin: origin,
            scale: new Vector2(sx, sy),
            effects: SpriteEffects.None,
            layerDepth: depth);
    }

    public void End()
    {
        if (!_begun) return;
        _sb.End();
        _begun = false;
    }
}

