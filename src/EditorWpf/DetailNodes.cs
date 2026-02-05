using System.Collections.ObjectModel;
using Engine.Core.Inspection;

namespace EditorWpf;

public sealed class ComponentNode
{
    public string Name { get; }
    public ObservableCollection<FieldNode> Fields { get; } = new();

    public ComponentNode(string name)
    {
        Name = name;
    }
}

public sealed class FieldNode
{
    public string Name { get; }
    public string ValueText { get; set; }
    public bool CanReset { get; }
    public Type? ComponentType { get; }
    public FieldDescriptor? FieldDescriptor { get; }

    public FieldNode(string name, string valueText, Type componentType, FieldDescriptor fieldDescriptor, bool canReset)
    {
        Name = name;
        ValueText = valueText;
        ComponentType = componentType;
        FieldDescriptor = fieldDescriptor;
        CanReset = canReset;
    }
}
