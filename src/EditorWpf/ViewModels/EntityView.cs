using Engine.Core.Scene;

namespace EditorWpf.ViewModels;

public sealed class EntityView
{
    public Entity Entity { get; }
    public string DisplayName { get; }

    public EntityView(Entity entity)
    {
        Entity = entity;
        DisplayName = $"{entity.Name} ({entity.Id.ToString("N")[..8]})";
    }
}
