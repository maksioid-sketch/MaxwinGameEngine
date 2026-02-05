using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Engine.Core.Inspection;

namespace EditorWpf;

public sealed class ComponentNode : INotifyPropertyChanged
{
    public string Name { get; }
    public ObservableCollection<FieldNode> Fields { get; } = new();

    public ComponentNode(string name)
    {
        Name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class FieldNode : INotifyPropertyChanged
{
    public string Name { get; }
    private string _valueText;
    private bool _canReset;
    private bool _isBool;
    private bool _boolValue;
    public Type? ComponentType { get; }
    public FieldDescriptor? FieldDescriptor { get; }

    public FieldNode(
        string name,
        string valueText,
        Type componentType,
        FieldDescriptor fieldDescriptor,
        bool canReset,
        bool isBool,
        bool boolValue)
    {
        Name = name;
        ComponentType = componentType;
        FieldDescriptor = fieldDescriptor;
        _valueText = valueText;
        _canReset = canReset;
        _isBool = isBool;
        _boolValue = boolValue;
    }

    public string ValueText
    {
        get => _valueText;
        set { if (_valueText != value) { _valueText = value; Notify(); } }
    }

    public bool CanReset
    {
        get => _canReset;
        set { if (_canReset != value) { _canReset = value; Notify(); } }
    }

    public bool IsBool
    {
        get => _isBool;
        set { if (_isBool != value) { _isBool = value; Notify(); } }
    }

    public bool BoolValue
    {
        get => _boolValue;
        set { if (_boolValue != value) { _boolValue = value; Notify(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
