using Engine.Core.Assets;
using Engine.Core.Components;
using Engine.Core.Rendering;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using Engine.Core.Systems;
using Engine.Core.Assets;
using Engine.Core.Assets.Animation;
using Engine.Core.Runtime;
using Engine.Core.Platform.Input;
using Engine.Core.Systems.BuiltIn;
using SandboxGame.Platform;

using Engine.Runtime.MonoGame.Assets;
using Engine.Runtime.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SandboxGame.HotReload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.Core.Rendering.Queue;
using Engine.Core.Validation;


namespace SandboxGame;

public sealed class GameApp : Game

{

    private MonoGameTime _time = null!;
    private EngineServices _services = null!;

    private IAssetProvider _assets = null!;
    private HotReloadService? _hotReload;
    private MonoGameInput _input = null!;

    private string _scenePath = "";
    private string _atlasPath = "";
    private string _animationsPath = "";

    private SpriteBatch _uiSb = null!;
    private SpriteFont _debugFont = null!;

    private readonly List<RenderItem2D> _renderQueue2D = new();
    private List<ValidationIssue> _validationIssues = new();


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
        _input = new MonoGameInput();
        _time = new MonoGameTime();

        // _assets must exist by now (even if empty)
        _services = new EngineServices(_input, _time, _assets);


        _textures = new TextureStore(Content);
        _renderer2D = new MonoGameRenderer2D(GraphicsDevice, _textures);
        _camera = new Camera2D();

        _uiSb = new SpriteBatch(GraphicsDevice);
        _debugFont = Content.Load<SpriteFont>("DebugFont");

        string watchRoot = AppContext.BaseDirectory;

        #if DEBUG
            watchRoot = SandboxGame.HotReload.DevPaths.FindProjectRoot("SandboxGame.csproj");
            _hotReload = new SandboxGame.HotReload.HotReloadService(
                directoryToWatch: watchRoot,
                filters: new[] { "atlas.json", ".scene.json", "animations.json" });

            _hotReloadStatus = "Hot reload: ON (watching source folder)";
        #else
            _hotReloadStatus = "Hot reload: OFF";
        #endif

        _scenePath = Path.Combine(watchRoot, "Scenes", "test.scene.json");
        _atlasPath = Path.Combine(watchRoot, "Assets", "atlas.json");
        _animationsPath = Path.Combine(watchRoot, "Assets", "animations.json");

        // Always initialize to non-null so later code can't explode
        _assets = new DictionaryAssetProvider(
            new Dictionary<string, SpriteDefinition>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase));
        _services.SetAssets(_assets);


        _scene = new Engine.Core.Scene.Scene();

        

        // Load real data
        ReloadAssets();   // loads atlas + animations, also calls RebuildSystems() inside (recommended)
        ReloadScene();    // loads scene
        //RebuildSystems(); // if you DON'T call it inside ReloadAssets(), keep this here

        _hotReloadStatus = $"{_hotReloadStatus}\nScene path: {_scenePath}";
    }

    protected override void Update(GameTime gameTime)
    {
        _time.Update(gameTime);
        _input.Update();

        if (_input.WasPressed(InputKey.Escape))
            Exit();

        // Hot reload first
        var changes = _hotReload?.ConsumeChanges();
        if (changes is { Count: > 0 })
        {
            bool assetsChanged =
                changes.Any(p => p.EndsWith("atlas.json", StringComparison.OrdinalIgnoreCase)) ||
                changes.Any(p => p.EndsWith("animations.json", StringComparison.OrdinalIgnoreCase));

            bool sceneChanged =
                changes.Any(p => p.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase));

            if (assetsChanged) ReloadAssets(); // must call _services.SetAssets(_assets) inside
            if (sceneChanged) ReloadScene();
        }

        var ctx = new EngineContext(_services);

        for (int i = 0; i < _systems.Count; i++)
            _systems[i].Update(_scene, ctx);

        _scene.Update(_time.DeltaSeconds);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _renderer2D.Begin(_camera);

        

        _scene.CollectRenderItems2D(_renderQueue2D, _assets);
        _renderQueue2D.Sort(RenderItem2DComparer.Instance);

        for (int i = 0; i < _renderQueue2D.Count; i++)
        {
            var it = _renderQueue2D[i];
            _renderer2D.DrawSprite(
                it.TextureKey,
                it.WorldPosition,
                it.WorldScale,
                it.RotationRadians,
                it.SourceRect,
                it.Tint,
                it.Layer,
                it.PixelsPerUnit);
        }

        _renderer2D.End();

        DrawDebugOverlay();

        base.Draw(gameTime);
    }

    private void ReloadAssets()
    {
        try
        {
            if (!File.Exists(_atlasPath))
                throw new FileNotFoundException("atlas.json not found", _atlasPath);

            if (!File.Exists(_animationsPath))
                throw new FileNotFoundException("animations.json not found", _animationsPath);

            // Load + parse
            var atlasJson = File.ReadAllText(_atlasPath);
            var sprites = AtlasJson.DeserializeSprites(atlasJson);

            var animJson = File.ReadAllText(_animationsPath);
            var clips = AnimationJson.DeserializeClips(animJson);

            // Swap provider atomically
            _assets = new DictionaryAssetProvider(sprites, clips);

            // Recreate systems that depend on _assets (AnimationSystem holds _assets reference)
            RebuildSystems();

            // Revalidate with new assets
            _validationIssues = SceneValidator.Validate(_scene, _assets);

            _hotReloadStatus = $"Assets reloaded: {DateTime.Now:T}";

            _services.SetAssets(_assets);
            _validationIssues = SceneValidator.Validate(_scene, _assets);

        }
        catch (Exception ex)
        {
            _hotReloadStatus = $"Assets reload FAILED: {ex.Message}";
            // Keep previous _assets and systems running
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

            _validationIssues = SceneValidator.Validate(_scene, _assets);


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

    private void DrawDebugOverlay()
    {
        _uiSb.Begin();

        var y = 10f;

        _uiSb.DrawString(_debugFont, _hotReloadStatus, new Microsoft.Xna.Framework.Vector2(10, y), Microsoft.Xna.Framework.Color.White);
        y += 40f;

        _uiSb.DrawString(_debugFont, $"Queue: {_renderQueue2D.Count}", new Microsoft.Xna.Framework.Vector2(10, y), Microsoft.Xna.Framework.Color.White);
        y += 20f;

        var player = _scene.FindByName("Player");
        if (player != null && player.TryGet<Engine.Core.Components.SpriteRenderer>(out var sr) && sr != null)
        {
            _uiSb.DrawString(_debugFont, $"Player SpriteId: {sr.SpriteId}", new Microsoft.Xna.Framework.Vector2(10, y), Microsoft.Xna.Framework.Color.White);
            y += 20f;
        }



        if (_validationIssues.Count == 0)
        {
            _uiSb.DrawString(_debugFont, "Validation: OK", new Microsoft.Xna.Framework.Vector2(10, y), Microsoft.Xna.Framework.Color.LightGreen);
        }
        else
        {
            _uiSb.DrawString(_debugFont, $"Validation: {_validationIssues.Count} issue(s)", new Microsoft.Xna.Framework.Vector2(10, y), Microsoft.Xna.Framework.Color.Yellow);
            y += 24f;

            // Show first N issues
            int max = System.Math.Min(_validationIssues.Count, 8);
            for (int i = 0; i < max; i++)
            {
                var issue = _validationIssues[i];
                var line = $"{issue.Severity} {issue.Code} {(issue.EntityName is null ? "" : $"[{issue.EntityName}] ")}{issue.Message}";
                _uiSb.DrawString(_debugFont, line, new Microsoft.Xna.Framework.Vector2(10, y), Microsoft.Xna.Framework.Color.OrangeRed);
                y += 20f;
            }

            if (_validationIssues.Count > max)
                _uiSb.DrawString(_debugFont, $"...and {_validationIssues.Count - max} more", new Microsoft.Xna.Framework.Vector2(10, y), Microsoft.Xna.Framework.Color.OrangeRed);
        }

        _uiSb.End();
    }


    private void RebuildSystems()
    {
        _systems = new List<ISystem>
    {
        new SandboxGame.Systems.AnimationSystem(),
        new Engine.Core.Systems.BuiltIn.PlayerMovementSystem()
    };
    }


}





