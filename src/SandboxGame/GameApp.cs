using Engine.Core.Components;
using Engine.Core.Rendering;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using Engine.Core.Systems;
using Engine.Runtime.MonoGame.Assets;
using Engine.Runtime.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SandboxGame.Systems;
using System;
using System.Collections.Generic;
using System.IO;


namespace SandboxGame;

public sealed class GameApp : Game
{

    private List<ISystem> _systems = new();
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

        // Load scene JSON from copied output folder:
        var scenePath = Path.Combine(AppContext.BaseDirectory, "Scenes", "test.scene.json");
        var json = File.ReadAllText(scenePath);
        _scene = SceneJson.Deserialize(json);

        // Systems
        _systems = new List<ISystem>
    {
        new PlayerMovementSystem()
    };
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        foreach (var sys in _systems)
            sys.Update(_scene, dt);

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

