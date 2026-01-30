using Engine.Core.Platform.Input;
using Microsoft.Xna.Framework.Input;

namespace SandboxGame.Platform;

public sealed class MonoGameInput : IInput
{
    private KeyboardState _prev;
    private KeyboardState _cur;

    public void Update()
    {
        _prev = _cur;
        _cur = Keyboard.GetState();
    }

    public bool IsDown(InputKey key) => _cur.IsKeyDown(Map(key));

    public bool WasPressed(InputKey key)
    {
        var k = Map(key);
        return _cur.IsKeyDown(k) && !_prev.IsKeyDown(k);
    }

    public bool WasReleased(InputKey key)
    {
        var k = Map(key);
        return !_cur.IsKeyDown(k) && _prev.IsKeyDown(k);
    }

    private static Keys Map(InputKey key) => key switch
    {
        InputKey.W => Keys.W,
        InputKey.A => Keys.A,
        InputKey.S => Keys.S,
        InputKey.D => Keys.D,

        InputKey.Up => Keys.Up,
        InputKey.Down => Keys.Down,
        InputKey.Left => Keys.Left,
        InputKey.Right => Keys.Right,

        InputKey.Space => Keys.Space,
        InputKey.Escape => Keys.Escape,
        InputKey.Enter => Keys.Enter,

        _ => Keys.None
    };
}
