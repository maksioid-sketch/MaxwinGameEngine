using System;

namespace Engine.Core.Runtime.Events;

public readonly record struct AnimationNotifyEvent(
    Guid EntityId,
    string EntityName,
    string ClipId,
    int FrameIndex,
    string Name
);
