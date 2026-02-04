using System.Numerics;
using Engine.Core.Components;
using Engine.Core.Platform.Input;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;

namespace Engine.Core.Systems.BuiltIn;

public sealed class PlayerMovementSystem : ISystem
{
    public string PlayerEntityName { get; set; } = "Player";
    public float SpeedUnitsPerSecond { get; set; } = 3f;

    // Name of the float parameter written to Animator (used by controllers.json)
    public string SpeedParamName { get; set; } = "speed";

    // Optional: test trigger mapping (press E to fire "damaged")
    public string DamageTriggerName { get; set; } = "damaged";

    private bool _cachedCollider;
    private Vector2 defBoxSize;
    private Vector2 defBoxOffset;



    

    public void Update(Scene.Scene scene, EngineContext ctx)
    {
        var player = scene.FindByName(PlayerEntityName);
        if (player is null) return;

        // Compute desired movement direction from input
        var move = Vector2.Zero;

        if (ctx.Input.IsDown(InputKey.A) || ctx.Input.IsDown(InputKey.Left)) move.X -= 1f;
        if (ctx.Input.IsDown(InputKey.D) || ctx.Input.IsDown(InputKey.Right)) move.X += 1f;
        if (ctx.Input.IsDown(InputKey.W) || ctx.Input.IsDown(InputKey.Up)) move.Y -= 1f;
        if (ctx.Input.IsDown(InputKey.S) || ctx.Input.IsDown(InputKey.Down)) move.Y += 1f;

        float speed = 0f;

        if (move != Vector2.Zero)
        {
            move = Vector2.Normalize(move);
            speed = SpeedUnitsPerSecond;

            // Apply movement
            var p = player.Transform.Position;
            p.X += move.X * SpeedUnitsPerSecond * ctx.DeltaSeconds;
            p.Y += move.Y * SpeedUnitsPerSecond * ctx.DeltaSeconds;
            player.Transform.Position = p;

            // Optional: face direction
            if (player.TryGet<SpriteRenderer>(out var sr) && sr != null)
            {
                bool left = (ctx.Input.IsDown(InputKey.A) || ctx.Input.IsDown(InputKey.Left));
                bool right = (ctx.Input.IsDown(InputKey.D) || ctx.Input.IsDown(InputKey.Right));

                if (left && !right)
                    sr.Flip = Engine.Core.Rendering.SpriteFlip.X;
                else if (right && !left)
                    sr.Flip = Engine.Core.Rendering.SpriteFlip.None;
            }
        }
        bool crouch = false;
        bool guard = false;
        
        if (ctx.Input.IsDown(InputKey.Shift)) crouch = true; else crouch = false;

          if (player.TryGet<BoxCollider2D>(out var box) && box != null)
        {
            if (!_cachedCollider)
            {
                defBoxSize = box.Size;
                defBoxOffset = box.Offset;
                _cachedCollider = true;
            }

            if (crouch)
            {
                box.Size = new Vector2(defBoxSize.X, defBoxSize.Y * 0.70f);

                // Keep bottom aligned: move collider down by half the height reduction
                float deltaY = (defBoxSize.Y - box.Size.Y) * 0.5f;
                box.Offset = new Vector2(defBoxOffset.X, defBoxOffset.Y + deltaY);
            }
            else
            {
                box.Size = defBoxSize;
                box.Offset = defBoxOffset;
            }
        }


            
            


        if (ctx.Input.IsDown(InputKey.E)) guard = true; else guard = false;




        // Write parameters for the animation controller (works even if movement later becomes AI-driven)
        if (player.TryGet<Animator>(out var anim) && anim != null)
        {
            anim.Floats[SpeedParamName] = speed;
            anim.Bools["grounded"] = true;
            anim.Bools["crouch"] = crouch;
            anim.Bools["guard"] = guard;

        }
    }
}
