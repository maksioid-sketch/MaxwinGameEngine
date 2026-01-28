using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Engine.Core.Components;
using Engine.Core.Math;

namespace Engine.Core.Scene;

public sealed class Entity
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Entity";
    public Transform Transform { get; } = new();

    private readonly Dictionary<Type, IComponent> _components = new();

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
}
