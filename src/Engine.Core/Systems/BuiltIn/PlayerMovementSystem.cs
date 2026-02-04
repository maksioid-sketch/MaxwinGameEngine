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
    public float JumpSpeedUnitsPerSecond { get; set; } = 9f;
    public float GroundCheckDistance { get; set; } = 0.03f;

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
        // Vertical movement is now jump-only (Space).

        float speed = 0f;

        if (move != Vector2.Zero)
        {
            move = Vector2.Normalize(move);
            speed = SpeedUnitsPerSecond;
        }

        bool grounded = IsGrounded(scene, player);

        // Apply movement (velocity if Rigidbody2D exists, otherwise direct position)
        if (player.TryGet<Rigidbody2D>(out var rb) && rb is not null)
        {
            var v = rb.Velocity;
            v.X = move.X * SpeedUnitsPerSecond;

            if (grounded && ctx.Input.WasPressed(InputKey.W))
                v.Y = -JumpSpeedUnitsPerSecond;

            rb.Velocity = v;
        }
        else if (move != Vector2.Zero)
        {
            var p = player.Transform.Position;
            p.X += move.X * SpeedUnitsPerSecond * ctx.DeltaSeconds;
            p.Y += move.Y * SpeedUnitsPerSecond * ctx.DeltaSeconds;
            player.Transform.Position = p;
        }

        // Optional: face direction
        if (move != Vector2.Zero && player.TryGet<SpriteRenderer>(out var sr) && sr != null)
        {
            bool left = (ctx.Input.IsDown(InputKey.A) || ctx.Input.IsDown(InputKey.Left));
            bool right = (ctx.Input.IsDown(InputKey.D) || ctx.Input.IsDown(InputKey.Right));

            if (left && !right)
                sr.Flip = Engine.Core.Rendering.SpriteFlip.X;
            else if (right && !left)
                sr.Flip = Engine.Core.Rendering.SpriteFlip.None;
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
            anim.Bools["grounded"] = grounded;
            anim.Bools["crouch"] = crouch;
            anim.Bools["guard"] = guard;

        }
    }

    private bool IsGrounded(Scene.Scene scene, Entity player)
    {
        if (!player.TryGet<BoxCollider2D>(out var playerBox) || playerBox is null)
            return false;

        var pScale = player.Transform.Scale;
        var pSize = new Vector2(MathF.Abs(pScale.X) * playerBox.Size.X, MathF.Abs(pScale.Y) * playerBox.Size.Y);
        var pOffset = new Vector2(playerBox.Offset.X * pScale.X, playerBox.Offset.Y * pScale.Y);
        var pCenter = new Vector2(player.Transform.Position.X, player.Transform.Position.Y) + pOffset;
        var pHalf = pSize * 0.5f;
        var pMin = pCenter - pHalf;
        var pMax = pCenter + pHalf;

        var entities = scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            if (ReferenceEquals(e, player))
                continue;

            if (!e.TryGet<BoxCollider2D>(out var box) || box is null)
                continue;

            if (box.IsTrigger)
                continue;

            if (e.TryGet<PhysicsBody2D>(out var phys) && phys is not null && !phys.IsStatic)
                continue;

            var scale = e.Transform.Scale;
            var size = new Vector2(MathF.Abs(scale.X) * box.Size.X, MathF.Abs(scale.Y) * box.Size.Y);
            var offset = new Vector2(box.Offset.X * scale.X, box.Offset.Y * scale.Y);
            var center = new Vector2(e.Transform.Position.X, e.Transform.Position.Y) + offset;
            var half = size * 0.5f;
            var min = center - half;
            var max = center + half;

            bool overlapX = pMax.X > min.X && pMin.X < max.X;
            if (!overlapX)
                continue;

            float gap = min.Y - pMax.Y;
            if (gap >= -GroundCheckDistance && gap <= GroundCheckDistance)
                return true;
        }

        return false;
    }
}
