using Engine.Core.Components;
using Engine.Core.Math;

namespace Engine.Core.Scene;

public sealed class Entity
{
    public Guid Id { get; private set; }
    public string Name { get; set; } = "Entity";
    public Transform Transform { get; } = new();

    private readonly Dictionary<Type, IComponent> _components = new();

    public Entity() : this(Guid.NewGuid()) { }

    public Entity(Guid id)
    {
        Id = id;
    }

    public T Add<T>(T component) where T : class, IComponent
    {
        _components[typeof(T)] = component;
        return component;
    }

    public bool TryGet<T>(out T? component) where T : class, IComponent
    {
        if (_components.TryGetValue(typeof(T), out var c))
        {
            component = (T)c;
            return true;
        }

        component = null;
        return false;
    }

    public T GetOrAdd<T>(Func<T> factory) where T : class, IComponent
    {
        if (TryGet<T>(out var existing) && existing is not null) return existing;
        return Add(factory());
    }

    public bool Remove<T>() where T : class, IComponent
        => _components.Remove(typeof(T));

    public bool Remove(Type componentType)
        => _components.Remove(componentType);
}
