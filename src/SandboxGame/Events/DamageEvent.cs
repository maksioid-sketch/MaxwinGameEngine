using System;

namespace SandboxGame.Events;

public readonly record struct DamageEvent(Guid EntityId, int Amount);
