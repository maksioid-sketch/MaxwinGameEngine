using System;
using System.Collections.Generic;

namespace Engine.Core.Runtime.Events;

public sealed class EventBus : IEventBus
{
    private interface IEventList
    {
        void Clear();
    }

    private sealed class EventList<T> : IEventList where T : notnull
    {
        public readonly List<T> Items = new();
        public void Clear() => Items.Clear();
    }

    private readonly Dictionary<Type, IEventList> _lists = new();

    public void Publish<T>(in T evt) where T : notnull
    {
        var type = typeof(T);

        if (!_lists.TryGetValue(type, out var listObj))
        {
            listObj = new EventList<T>();
            _lists[type] = listObj;
        }

        ((EventList<T>)listObj).Items.Add(evt);
    }

    public IReadOnlyList<T> Read<T>() where T : notnull
    {
        if (_lists.TryGetValue(typeof(T), out var listObj))
            return ((EventList<T>)listObj).Items;

        return Array.Empty<T>();
    }

    public void Clear()
    {
        foreach (var kv in _lists)
            kv.Value.Clear();
    }
}
