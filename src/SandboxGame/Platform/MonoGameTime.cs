using Engine.Core.Platform.Time;
using Microsoft.Xna.Framework;

namespace SandboxGame.Platform;

public sealed class MonoGameTime : ITime
{
    public float DeltaSeconds { get; private set; }
    public double TotalSeconds { get; private set; }

    public void Update(GameTime gameTime)
    {
        DeltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        TotalSeconds = gameTime.TotalGameTime.TotalSeconds;
    }
}
