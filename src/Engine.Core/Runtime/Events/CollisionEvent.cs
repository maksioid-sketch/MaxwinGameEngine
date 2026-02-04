using Engine.Core.Scene;

namespace Engine.Core.Runtime.Events;

public readonly record struct CollisionEvent(Entity A, Entity B);
