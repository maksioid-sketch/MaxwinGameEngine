using Engine.Core.Animation;
using Engine.Core.Components;
using Engine.Core.Platform.Input;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;

namespace Engine.Core.Systems.BuiltIn;

public sealed class PlayerCapybaraStateSystem : ISystem
{
    public string PlayerEntityName { get; set; } = "Player";

    // Clips (match your generated ids)
    public string Idle = "player_idle";
    public string Walk = "player_walk";
    public string SitDown = "player_sitdown";
    public string SitIdle = "player_sitidle";
    public string StandUp = "player_standup";
    public string LeanDown = "player_leandown";
    public string Eat = "player_eat";
    public string LeanUp = "player_leanup";

    // Very simple “mode” state (runtime-only)
    private enum Mode { Standing, Sitting, Eating }
    private Mode _mode = Mode.Standing;

    public void Update(Scene.Scene scene, EngineContext ctx)
    {
        var player = scene.FindByName(PlayerEntityName);
        if (player is null) return;
        if (!player.TryGet<Animator>(out var anim) || anim is null) return;

        // Movement -> walk/idle only when standing (not sitting/eating)
        bool moving =
            ctx.Input.IsDown(InputKey.W) || ctx.Input.IsDown(InputKey.A) ||
            ctx.Input.IsDown(InputKey.S) || ctx.Input.IsDown(InputKey.D) ||
            ctx.Input.IsDown(InputKey.Up) || ctx.Input.IsDown(InputKey.Left) ||
            ctx.Input.IsDown(InputKey.Down) || ctx.Input.IsDown(InputKey.Right);

        // Sit / Stand toggle on Space
        if (ctx.Input.WasPressed(InputKey.Space))
        {
            if (_mode == Mode.Standing)
            {
                // Sit down transition -> sit idle
                anim.PlayThen(SitDown, SitIdle, restart: true);
                _mode = Mode.Sitting;
                return;
            }

            if (_mode == Mode.Sitting || _mode == Mode.Eating)
            {
                // Stand up transition -> idle
                anim.PlayThen(StandUp, Idle, restart: true);
                _mode = Mode.Standing;
                return;
            }
        }

        // Eat toggle on E (only if sitting)
        if (ctx.Input.WasPressed(InputKey.Enter))
        {
            if (_mode == Mode.Standing)
            {
                anim.PlayThen(LeanDown, Eat, restart: true);
                _mode = Mode.Eating;
                return;
            }

            if (_mode == Mode.Eating)
            {
                anim.PlayThen(LeanUp, Idle, restart: true);
                _mode = Mode.Standing;
                return;
            }
        }

        // If we’re standing and not currently in a transition clip, drive idle/walk
        if (_mode == Mode.Standing)
        {
            // Don't override transition clips
            if (IsTransition(anim.ClipId))
                return;

            anim.Play(moving ? Walk : Idle, restart: false);
        }
        else if (_mode == Mode.Sitting)
        {
            // If not in transition, ensure sitidle
            if (!IsTransition(anim.ClipId) && !string.Equals(anim.ClipId, SitIdle, StringComparison.OrdinalIgnoreCase))
                anim.Play(SitIdle, restart: false);
        }
        else if (_mode == Mode.Eating)
        {
            if (!IsTransition(anim.ClipId) && !string.Equals(anim.ClipId, Eat, StringComparison.OrdinalIgnoreCase))
                anim.Play(Eat, restart: false);
        }
    }

    private bool IsTransition(string clipId)
    {
        return clipId.Equals(SitDown, StringComparison.OrdinalIgnoreCase) ||
               clipId.Equals(StandUp, StringComparison.OrdinalIgnoreCase) ||
               clipId.Equals(LeanDown, StringComparison.OrdinalIgnoreCase) ||
               clipId.Equals(LeanUp, StringComparison.OrdinalIgnoreCase);
    }
}
