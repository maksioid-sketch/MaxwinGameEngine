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
    private string _controllersPath = "";

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
                filters: new[] { "atlas.generated.json", ".scene.json", "animations.generated.json", "controllers.json" });

            _hotReloadStatus = "Hot reload: ON (watching source folder)";
        #else
            _hotReloadStatus = "Hot reload: OFF";
        #endif

        _scenePath = Path.Combine(watchRoot, "Scenes", "test.scene.json");
        _atlasPath = Path.Combine(watchRoot, "Assets", "atlas.generated.json");
        _animationsPath = Path.Combine(watchRoot, "Assets", "animations.generated.json");
        _controllersPath = Path.Combine(watchRoot, "Assets", "controllers.json");


        // Always initialize to non-null so later code can't explode
        _assets = new DictionaryAssetProvider(
            new Dictionary<string, SpriteDefinition>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AnimatorController>(StringComparer.OrdinalIgnoreCase)
            );
        _services.Assets = _assets;



        _scene = new Engine.Core.Scene.Scene();

        

        // Load real data
        ReloadAssets();   // loads atlas + animations, also calls RebuildSystems() inside (recommended)
        if (!_assets.TryGetAnimation("player_idle", out var idle) || idle.Frames.Count == 0)
            throw new Exception("Animation 'player_idle' not loaded or has 0 frames. Check animations.generated.json load path + ReloadAssets().");


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
                changes.Any(p => p.EndsWith("atlas.generated.json", StringComparison.OrdinalIgnoreCase)) ||
                changes.Any(p => p.EndsWith("animations.generated.json", StringComparison.OrdinalIgnoreCase)) ||
                changes.Any(p => p.EndsWith("controllers.json", StringComparison.OrdinalIgnoreCase));

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
                it.PixelsPerUnit,
                it.OriginPixels,
                it.Flip);

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
                throw new FileNotFoundException("atlas.generated.json not found", _atlasPath);

            if (!File.Exists(_animationsPath))
                throw new FileNotFoundException("animations.generated.json not found", _animationsPath);

            // Load + parse
            var atlasJson = File.ReadAllText(_atlasPath);
            var sprites = AtlasJson.DeserializeSprites(atlasJson);

            var animJson = File.ReadAllText(_animationsPath);
            var clips = AnimationJson.DeserializeClips(animJson);

            var controllersJson = File.ReadAllText(_controllersPath);
            var controllers = Engine.Core.Serialization.AnimatorControllerJson.DeserializeControllers(controllersJson);

            _assets = new DictionaryAssetProvider(sprites, clips, controllers);


            // Recreate systems that depend on _assets (AnimationSystem holds _assets reference)
            RebuildSystems();

            // Revalidate with new assets
            _validationIssues = SceneValidator.Validate(_scene, _assets);

            _hotReloadStatus = $"Assets reloaded: {DateTime.Now:T}";

            _services.Assets = _assets;

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

            var p = _scene.FindByName("Player");
            if (p != null && p.TryGet<Engine.Core.Components.Animator>(out var a) && a != null)
            {
                if (string.IsNullOrWhiteSpace(a.ControllerId))
                    throw new Exception("Scene loaded but Player.Animator.ControllerId is empty. SceneJson mapping is missing controllerId.");
            }


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

        float x = 10f;
        float y = 10f;
        float lineH = _debugFont.LineSpacing;

        // Existing status
        DrawLines(_hotReloadStatus, ref y, x, lineH);

        // ---- NEW: Time + Assets sanity ----
        DrawLine($"dt={_services.Time.DeltaSeconds:0.0000} total={_services.Time.TotalSeconds:0.00}", ref y, x, lineH);

        bool hasIdle = _services.Assets.TryGetAnimation("player_idle", out var idleClip);
        DrawLine($"assets: idleClip={hasIdle} frames={(hasIdle ? idleClip.Frames.Count : 0)}", ref y, x, lineH);

        // ---- NEW: Player animator state ----
        var player = _scene.FindByName("Player");
        if (player is null)
        {
            DrawLine("Player entity: NOT FOUND", ref y, x, lineH);
        }
        else if (!player.TryGet<Engine.Core.Components.Animator>(out var anim) || anim is null)
        {
            DrawLine("Player Animator: MISSING", ref y, x, lineH);
        }
        else
        {

            bool hasCtrl = false;
            string initial = "-";
            int stateCount = 0;

            if (!string.IsNullOrWhiteSpace(anim.ControllerId) &&
                _services.Assets.TryGetController(anim.ControllerId, out var tmpCtrl))
            {
                hasCtrl = true;
                initial = tmpCtrl.InitialState ?? "-";
                stateCount = tmpCtrl.States?.Count ?? 0;
            }

            DrawLine($"ControllerId={anim.ControllerId} hasCtrl={hasCtrl}", ref y, x, lineH);
            if (hasCtrl)
                DrawLine($"Ctrl initial={initial} states={stateCount}", ref y, x, lineH);


            DrawLine($"Animator: clip={anim.ClipId} playing={anim.Playing} speed={anim.Speed:0.00}", ref y, x, lineH);
            DrawLine($"Animator: frame={anim.FrameIndex} t={anim.TimeIntoFrame:0.000} reset={anim.ResetRequested}", ref y, x, lineH);
            DrawLine($"Animator: state={anim.StateId} pending={anim.PendingStateId ?? "-"} next={anim.NextClipId ?? "-"}", ref y, x, lineH);

            var hasCur = false;
            var curFrames = 0;

            if (!string.IsNullOrWhiteSpace(anim.ClipId) && _services.Assets.TryGetAnimation(anim.ClipId, out var tmp))
            {
                hasCur = true;
                curFrames = tmp.Frames.Count;
            }

            DrawLine($"assets: currentClip={hasCur} curFrames={curFrames}", ref y, x, lineH);

        }

        // Existing: render item origin
        if (_renderQueue2D.Count > 0)
        {
            var it = _renderQueue2D[0];
            DrawLine($"RenderItem: origin={it.OriginPixels}", ref y, x, lineH);
        }

        _uiSb.End();
    }

    private void DrawLines(string text, ref float y, float x, float lineH)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Normalize newlines and split
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var raw in lines)
        {
            var line = FilterUnsupportedDebugChars(raw);
            _uiSb.DrawString(_debugFont, line, new Microsoft.Xna.Framework.Vector2(x, y), Microsoft.Xna.Framework.Color.White);
            y += lineH;
        }
    }
    private void DrawLine(string line, ref float y, float x, float lineH)
    {
        line = FilterUnsupportedDebugChars(line ?? "");
        _uiSb.DrawString(_debugFont, line, new Microsoft.Xna.Framework.Vector2(x, y), Microsoft.Xna.Framework.Color.White);
        y += lineH;
    }


    private static string FilterUnsupportedDebugChars(string s)
    {
        // Remove control chars that often break SpriteFont glyph lookup
        // Keep normal ASCII printable range.
        var chars = s.Where(c => c >= 32 && c <= 126).ToArray();
        return new string(chars);
    }


    private void RebuildSystems()
    {
        _systems = new List<ISystem>
{
    new Engine.Core.Systems.BuiltIn.PlayerMovementSystem(),
    new SandboxGame.Systems.AnimatorControllerSystem(() => _assets),
    new SandboxGame.Systems.AnimationSystem()
};


    }


}





