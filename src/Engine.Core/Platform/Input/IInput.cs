namespace Engine.Core.Platform.Input;

public interface IInput
{
    bool IsDown(InputKey key);
    bool WasPressed(InputKey key);   // down this frame, not down last frame
    bool WasReleased(InputKey key);  // up this frame, down last frame
}

