using Engine.Core.Assets;
using Engine.Core.Components;
using Engine.Core.Rendering;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using Engine.Core.Systems;
using Engine.Core.Assets.Animation;
using Engine.Core.Runtime;
using Engine.Core.Platform.Input;
using Engine.Core.Systems.BuiltIn;
using SandboxGame.Platform;
using Engine.Core.Runtime.Events;
using Engine.Core.Runtime.Debug;



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

    private readonly OnScreenDebug _onScreenDebug = new(maxTransientLines: 18);
    private readonly System.Collections.Generic.List<string> _debugLineBuffer = new();




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
        _services = new EngineServices(_input, _time, _assets, new EventBus());



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
        ReloadAssets(throwOnFailure: true);   // fail loudly on startup so you see the real error



        ReloadScene();    // loads scene
        //RebuildSystems(); // if you DON'T call it inside ReloadAssets(), keep this here

        _hotReloadStatus = $"{_hotReloadStatus}\nScene path: {_scenePath}";


        DebugPrint.Initialize(_onScreenDebug);
        DebugPrint.Print("DebugPrint ready (Unreal-style).", 2f);


    }

    protected override void Update(GameTime gameTime)
    {
        _time.Update(gameTime);
        _input.Update();

        _onScreenDebug.Update(_time.DeltaSeconds);


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

        _services.Events.Clear();
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

    private void ReloadAssets(bool throwOnFailure = false)
    {
        try
        {
            if (!File.Exists(_atlasPath))
                throw new FileNotFoundException("atlas.generated.json not found", _atlasPath);

            if (!File.Exists(_animationsPath))
                throw new FileNotFoundException("animations.generated.json not found", _animationsPath);

            if (!File.Exists(_controllersPath))
                throw new FileNotFoundException("controllers.json not found", _controllersPath);

            // Load + parse
            static string ReadJsonNoBom(string path)
            {
                var s = File.ReadAllText(path);
                return s.TrimStart('\uFEFF'); // remove UTF-8 BOM if present
            }

            var atlasJson = ReadJsonNoBom(_atlasPath);
            var sprites = AtlasJson.DeserializeSprites(atlasJson);

            var animJson = ReadJsonNoBom(_animationsPath);
            var clips = AnimationJson.DeserializeClips(animJson);

            var controllersJson = ReadJsonNoBom(_controllersPath);
            var controllers = Engine.Core.Serialization.AnimatorControllerJson.DeserializeControllers(controllersJson);


            _assets = new DictionaryAssetProvider(sprites, clips, controllers);

            // Recreate systems that depend on _assets (AnimationSystem holds _assets reference)
            RebuildSystems();

            // Revalidate with new assets
            _validationIssues = SceneValidator.Validate(_scene, _assets);

            _hotReloadStatus = $"Assets reloaded: {DateTime.Now:T}";
            _services.Assets = _assets;
        }
        catch (Exception ex)
        {
            _hotReloadStatus =
                "Assets reload FAILED:\n" +
                $"{ex.GetType().Name}: {ex.Message}\n" +
                $"atlas: {_atlasPath}\n" +
                $"anim:  {_animationsPath}\n" +
                $"ctrl:  {_controllersPath}";

            if (throwOnFailure)
                throw new Exception(_hotReloadStatus, ex);

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
        // 1) Build ALL overlay text via DebugPrint.Set(...)
       // PushPersistentDebugOverlay();

        // 2) Draw ONLY the new overlay system
        _uiSb.Begin();

        float x = 10f;
        float y = 10f;

        // NOTE: if your OnScreenDebug has GetLines() WITHOUT buffer, use that.
        // If you replaced OnScreenDebug with the newer version I gave, use GetLines(_debugLineBuffer).
        var lines = _onScreenDebug.GetLines(_debugLineBuffer);

        for (int i = 0; i < lines.Count; i++)
        {
            var line = FilterUnsupportedDebugChars(lines[i]);
            _uiSb.DrawString(_debugFont, line, new Microsoft.Xna.Framework.Vector2(x, y), Microsoft.Xna.Framework.Color.Yellow);
            y += _debugFont.LineSpacing;
        }

        _uiSb.End();
    }

    private void PushPersistentDebugOverlay()
    {
        // Existing status (multiline)
        SetMultilinePersistent("hot", _hotReloadStatus);

        DebugPrint.Set("time", $"dt={_services.Time.DeltaSeconds:0.0000} total={_services.Time.TotalSeconds:0.00}");

        bool hasIdle = _services.Assets.TryGetAnimation("player_idle", out var idleClip);
        DebugPrint.Set("assets_idle", $"assets: idleClip={hasIdle} frames={(hasIdle ? idleClip.Frames.Count : 0)}");

        var player = _scene.FindByName("Player");
        if (player is null)
        {
            DebugPrint.Set("player", "Player entity: NOT FOUND");
            DebugPrint.Clear("ctrl");
            DebugPrint.Clear("ctrl2");
            DebugPrint.Clear("anim1");
            DebugPrint.Clear("anim2");
            DebugPrint.Clear("anim3");
            DebugPrint.Clear("assets_cur");
        }
        else if (!player.TryGet<Engine.Core.Components.Animator>(out var anim) || anim is null)
        {
            DebugPrint.Set("player", "Player Animator: MISSING");
            DebugPrint.Clear("ctrl");
            DebugPrint.Clear("ctrl2");
            DebugPrint.Clear("anim1");
            DebugPrint.Clear("anim2");
            DebugPrint.Clear("anim3");
            DebugPrint.Clear("assets_cur");
        }
        else
        {
            DebugPrint.Set("player", "Player: OK");

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

            DebugPrint.Set("ctrl", $"ControllerId={anim.ControllerId} hasCtrl={hasCtrl}");
            if (hasCtrl)
                DebugPrint.Set("ctrl2", $"Ctrl initial={initial} states={stateCount}");
            else
                DebugPrint.Clear("ctrl2");

            DebugPrint.Set("anim1", $"Animator: clip={anim.ClipId} playing={anim.Playing} speed={anim.Speed:0.00}");
            DebugPrint.Set("anim2", $"Animator: frame={anim.FrameIndex} t={anim.TimeIntoFrame:0.000} reset={anim.ResetRequested}");
            DebugPrint.Set("anim3", $"Animator: state={anim.StateId} pending={anim.PendingStateId ?? "-"} next={anim.NextClipId ?? "-"}");

            bool hasCur = false;
            int curFrames = 0;
            if (!string.IsNullOrWhiteSpace(anim.ClipId) && _services.Assets.TryGetAnimation(anim.ClipId, out var tmp))
            {
                hasCur = true;
                curFrames = tmp.Frames.Count;
            }

            DebugPrint.Set("assets_cur", $"assets: currentClip={hasCur} curFrames={curFrames}");
        }

        // Existing: render item origin
        if (_renderQueue2D.Count > 0)
        {
            var it = _renderQueue2D[0];
            DebugPrint.Set("render0", $"RenderItem: origin={it.OriginPixels}");
        }
        else
        {
            DebugPrint.Clear("render0");
        }
    }

    private static void SetMultilinePersistent(string keyPrefix, string? text)
    {
        // Clear previous lines (up to some reasonable cap)
        for (int i = 0; i < 16; i++)
            Engine.Core.Runtime.Debug.DebugPrint.Clear($"{keyPrefix}_{i}");

        if (string.IsNullOrWhiteSpace(text))
            return;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (int i = 0; i < lines.Length && i < 16; i++)
        {
            Engine.Core.Runtime.Debug.DebugPrint.Set($"{keyPrefix}_{i}", lines[i]);
        }
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

            // Demo: E -> DamageEvent -> Animator trigger
            new SandboxGame.Systems.DebugDamageInputSystem(),
            new SandboxGame.Systems.DamageToAnimatorTriggerSystem(),

            new SandboxGame.Systems.AnimatorControllerSystem(() => _assets),
            new SandboxGame.Systems.AnimationSystem(),
             
            new Engine.Core.Systems.BuiltIn.AnimationNotifierSystem(),
            new SandboxGame.Systems.AnimationNotifyDebugSystem()
        };



    }


}





