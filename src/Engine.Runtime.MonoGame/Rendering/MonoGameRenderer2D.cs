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
    private Engine.Core.Rendering.Camera2D? _camera;


    private bool _begun;

    public MonoGameRenderer2D(GraphicsDevice graphicsDevice, TextureStore textures)
    {
        _gd = graphicsDevice;
        _sb = new SpriteBatch(graphicsDevice);
        _textures = textures;
    }

    public void Begin(Engine.Core.Rendering.Camera2D camera)
    {
        if (_begun) throw new InvalidOperationException("Renderer2D.Begin called twice.");

        camera.ViewportWidth = _gd.Viewport.Width;
        camera.ViewportHeight = _gd.Viewport.Height;

        _camera = camera;

        _sb.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp, // pixel-art friendly
            depthStencilState: DepthStencilState.None,
            rasterizerState: RasterizerState.CullNone);

        _begun = true;
    }



    public void DrawSprite(
    string textureKey,
    Vector3 worldPos,
    Vector2 worldScale,
    float rotationRadians,
    IntRect sourceRect,
    Color4 tint,
    int layer,
    float spritePixelsPerUnit,
    Vector2 originPixels,
    Engine.Core.Rendering.SpriteFlip flip
)

    {
        if (!_begun) throw new InvalidOperationException("DrawSprite called before Begin.");
        if (_camera is null) throw new InvalidOperationException("Camera not set.");
        if (string.IsNullOrWhiteSpace(textureKey)) return;

        var tex = _textures.Get(textureKey);
        if (tex is null)
            throw new InvalidOperationException($"TextureStore returned null for key '{textureKey}'.");

        // Source rect: [0,0,0,0] means full texture
        Microsoft.Xna.Framework.Rectangle? src = null;
        if (!(sourceRect.X == 0 && sourceRect.Y == 0 && sourceRect.W == 0 && sourceRect.H == 0))
            src = new Microsoft.Xna.Framework.Rectangle(sourceRect.X, sourceRect.Y, sourceRect.W, sourceRect.H);

        int srcW = src?.Width ?? tex.Width;
        int srcH = src?.Height ?? tex.Height;

        // Convert sprite PPU to screen scaling using camera global PPU:
        float ppuRatio = _camera.PixelsPerUnit / System.MathF.Max(0.0001f, spritePixelsPerUnit);

        var scale = new Microsoft.Xna.Framework.Vector2(
            worldScale.X * ppuRatio * _camera.Zoom,
            worldScale.Y * ppuRatio * _camera.Zoom);

        // World -> screen pixels
        var screenPos = _camera.WorldToScreen(new System.Numerics.Vector2(worldPos.X, worldPos.Y));
        var posXna = new Microsoft.Xna.Framework.Vector2(screenPos.X, screenPos.Y);

        // Origin in source pixels (center)
        var origin = new Microsoft.Xna.Framework.Vector2(originPixels.X, originPixels.Y);


        // Sprite rotation relative to camera
        float finalRot = rotationRadians - _camera.Rotation;

        var effects = SpriteEffects.None;
        if ((flip & Engine.Core.Rendering.SpriteFlip.X) != 0) effects |= SpriteEffects.FlipHorizontally;
        if ((flip & Engine.Core.Rendering.SpriteFlip.Y) != 0) effects |= SpriteEffects.FlipVertically;


        _sb.Draw(
            tex,
            posXna,
            src,
            new Microsoft.Xna.Framework.Color(tint.R, tint.G, tint.B, tint.A),
            finalRot,
            origin,
            scale,
            effects,
            0f);
    }


    public void End()
    {
        if (!_begun) throw new InvalidOperationException("Renderer2D.End called before Begin.");

        _sb.End();
        _begun = false;
        _camera = null;
    }

}

