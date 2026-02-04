using System.Collections.Generic;

namespace Engine.Core.Runtime.Events;

public interface IEventBus
{
    void Publish<T>(in T evt) where T : notnull;

    IReadOnlyList<T> Read<T>() where T : notnull;

    void Clear();
}
