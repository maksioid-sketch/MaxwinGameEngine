using Engine.Core.Components;
using Engine.Core.Rendering;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using Engine.Core.Systems;
using Engine.Runtime.MonoGame.Assets;
using Engine.Runtime.MonoGame.Rendering;
using Engine.Core.Assets;


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
    private IAssetProvider _assets = null!;

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

        var atlasPath = Path.Combine(AppContext.BaseDirectory, "Assets", "atlas.json");
        var atlasJson = File.ReadAllText(atlasPath);
        var sprites = AtlasJson.DeserializeSprites(atlasJson);
        _assets = new DictionaryAssetProvider(sprites);

        var player = _scene.FindByName("Player");
        if (player is null)
            throw new Exception("Scene loaded but no entity named 'Player'.");

        if (!player.TryGet<Engine.Core.Components.SpriteRenderer>(out var sr) || sr is null)
            throw new Exception("Player exists but has no SpriteRenderer component.");

        if (string.IsNullOrWhiteSpace(sr.SpriteId))
            throw new Exception("Player SpriteRenderer.SpriteId is empty. Your scene JSON is likely outdated in bin output.");

        if (!_assets.TryGetSprite(sr.SpriteId, out var def))
            throw new Exception($"Atlas does not contain spriteId '{sr.SpriteId}'. Check atlas.json keys.");



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
        _scene.Render(_renderer2D, _assets);
        _renderer2D.End();

        base.Draw(gameTime);
    }
}

