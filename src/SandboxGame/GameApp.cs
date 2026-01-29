using Engine.Core.Assets;
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
using SandboxGame.HotReload;
using SandboxGame.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace SandboxGame;

public sealed class GameApp : Game
{
    private IAssetProvider _assets = null!;
    private HotReloadService? _hotReload;

    private string _scenePath = "";
    private string _atlasPath = "";

    private SpriteBatch _uiSb = null!;
    private SpriteFont _debugFont = null!;


    // Optional: expose status for future debug overlay
    private string _hotReloadStatus = "Hot reload: off";


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

        _uiSb = new SpriteBatch(GraphicsDevice);
        _debugFont = Content.Load<SpriteFont>("DebugFont.spritefont"); // use asset name without extension in MGCB

        // Always initialize to non-null so later code can't explode
        _assets = new DictionaryAssetProvider(new Dictionary<string, Engine.Core.Assets.SpriteDefinition>(StringComparer.OrdinalIgnoreCase));
        _scene = new Engine.Core.Scene.Scene();

#if DEBUG
        var projectRoot = SandboxGame.HotReload.DevPaths.FindProjectRoot("SandboxGame.csproj");
        _scenePath = Path.Combine(projectRoot, "Scenes", "test.scene.json");
        _atlasPath = Path.Combine(projectRoot, "Assets", "atlas.json");

        // Watch the project root (source files)
        _hotReload = new SandboxGame.HotReload.HotReloadService(
            directoryToWatch: projectRoot,
            filters: new[] { "atlas.json", ".scene.json" });

        _hotReloadStatus = "Hot reload: ON (watching source folder)";
#else
    _scenePath = Path.Combine(AppContext.BaseDirectory, "Scenes", "test.scene.json");
    _atlasPath = Path.Combine(AppContext.BaseDirectory, "Assets", "atlas.json");
    _hotReloadStatus = "Hot reload: OFF";
#endif

        // Load real data (AFTER paths are set)
        ReloadAtlas();
        ReloadScene();

        // Optional: fail-fast check AFTER both are loaded
        var player = _scene.FindByName("Player");
        if (player is null) throw new Exception("No entity named 'Player' in scene.");

        if (!player.TryGet<Engine.Core.Components.SpriteRenderer>(out var sr) || sr is null)
            throw new Exception("Player has no SpriteRenderer.");

        if (string.IsNullOrWhiteSpace(sr.SpriteId))
            throw new Exception("Player SpriteId is empty.");

        if (!_assets.TryGetSprite(sr.SpriteId, out var def))
            throw new Exception($"Atlas does not contain spriteId '{sr.SpriteId}'.");

        _hotReloadStatus = $"{_hotReloadStatus}\nScene path: {_scenePath}";

        _systems = new List<ISystem>
    {
        new PlayerMovementSystem()
    };
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();



        var changes = _hotReload?.ConsumeChanges();




        if (changes is { Count: > 0 })
        {
            bool atlasChanged = changes.Any(p => p.EndsWith("atlas.json", StringComparison.OrdinalIgnoreCase));
            bool sceneChanged = changes.Any(p => p.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase));

            // Reload atlas first, then scene (scene depends on atlas mappings)
            if (atlasChanged)
                ReloadAtlas();

            if (sceneChanged)
                ReloadScene();
        }


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

        _uiSb.Begin();
        _uiSb.DrawString(_debugFont, _hotReloadStatus, new Microsoft.Xna.Framework.Vector2(10, 10), Microsoft.Xna.Framework.Color.White);
        _uiSb.End();

        _scene.Render(_renderer2D, _assets);
        _renderer2D.End();

        base.Draw(gameTime);
    }

    private void ReloadAtlas()
    {
        try
        {
            if (!File.Exists(_atlasPath))
                throw new FileNotFoundException("atlas.json not found at runtime path", _atlasPath);

            var json = File.ReadAllText(_atlasPath);
            var sprites = AtlasJson.DeserializeSprites(json);
            _assets = new DictionaryAssetProvider(sprites);

            _hotReloadStatus = $"Atlas reloaded: {DateTime.Now:T}";
        }
        catch (Exception ex)
        {
            _hotReloadStatus = $"Atlas reload FAILED: {ex.Message}";
            // Keep the previous _assets so the game continues running.
        }
    }

    private void ReloadScene()
    {
        try
        {
            if (!File.Exists(_scenePath))
                throw new FileNotFoundException("scene not found at runtime path", _scenePath);

            var json = File.ReadAllText(_scenePath);
            _scene = SceneJson.Deserialize(json);

            _hotReloadStatus = $"Scene reloaded: {DateTime.Now:T}";
        }
        catch (Exception ex)
        {
            _hotReloadStatus = $"Scene reload FAILED: {ex.Message}";
            // Keep the previous _scene so the game continues running.
        }

    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _hotReload?.Dispose();

        base.Dispose(disposing);
    }


}

