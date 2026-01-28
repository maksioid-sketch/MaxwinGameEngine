using Engine.Core.Components;
using Engine.Core.Scene;
using Engine.Core.Rendering;
using Engine.Runtime.MonoGame.Assets;
using Engine.Runtime.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SandboxGame;

public sealed class GameApp : Game
{
    private readonly GraphicsDeviceManager _gdm;

    private Scene _scene = null!;
    private Camera2D _camera = null!;

    private TextureStore _textures = null!;
    private MonoGameRenderer2D _renderer2D = null!;

    private Entity _player = null!;

    public GameApp()
    {
        _gdm = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Optional initial window size:
        _gdm.PreferredBackBufferWidth = 1280;
        _gdm.PreferredBackBufferHeight = 720;
    }

    protected override void LoadContent()
    {
        _textures = new TextureStore(Content);
        _renderer2D = new MonoGameRenderer2D(GraphicsDevice, _textures);

        _camera = new Camera2D();
        _scene = new Scene();

        _player = _scene.CreateEntity("Player");
        _player.Transform.Position = new System.Numerics.Vector3(4f, 3f, 0f); // world units
        _player.Transform.Scale = new System.Numerics.Vector3(1f, 1f, 1f);

        _player.Add(new SpriteRenderer
        {
            TextureKey = "Sprites/player",     // Content key (no extension)
            Layer = 100,
            PixelsPerUnit = 100f
        });
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Simple movement test:
        var k = Keyboard.GetState();
        var move = System.Numerics.Vector2.Zero;
        if (k.IsKeyDown(Keys.A)) move.X -= 1f;
        if (k.IsKeyDown(Keys.D)) move.X += 1f;
        if (k.IsKeyDown(Keys.W)) move.Y -= 1f;
        if (k.IsKeyDown(Keys.S)) move.Y += 1f;

        if (move != System.Numerics.Vector2.Zero)
        {
            move = System.Numerics.Vector2.Normalize(move);
            var p = _player.Transform.Position;
            p.X += move.X * 3f * dt;
            p.Y += move.Y * 3f * dt;
            _player.Transform.Position = p;
        }

        _scene.Update(dt);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _renderer2D.Begin(_camera);
        _scene.Render(_renderer2D);
        _renderer2D.End();

        base.Draw(gameTime);
    }
}

