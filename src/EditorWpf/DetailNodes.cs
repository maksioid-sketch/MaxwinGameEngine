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
    private string _enumValue;
    private string _xValue;
    private string _yValue;
    private string _zValue;
    private string _wValue;
    public Type? ComponentType { get; }
    public FieldDescriptor? FieldDescriptor { get; }
    public FieldKind Kind { get; }
    public ObservableCollection<string> EnumOptions { get; } = new();

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
        Kind = fieldDescriptor.Kind;
        _valueText = valueText;
        _canReset = canReset;
        _isBool = isBool;
        _boolValue = boolValue;
        _enumValue = valueText;
        _xValue = string.Empty;
        _yValue = string.Empty;
        _zValue = string.Empty;
        _wValue = string.Empty;
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

    public string EnumValue
    {
        get => _enumValue;
        set { if (_enumValue != value) { _enumValue = value; Notify(); } }
    }

    public string XValue
    {
        get => _xValue;
        set { if (_xValue != value) { _xValue = value; Notify(); } }
    }

    public string YValue
    {
        get => _yValue;
        set { if (_yValue != value) { _yValue = value; Notify(); } }
    }

    public string ZValue
    {
        get => _zValue;
        set { if (_zValue != value) { _zValue = value; Notify(); } }
    }

    public string WValue
    {
        get => _wValue;
        set { if (_wValue != value) { _wValue = value; Notify(); } }
    }

    public bool IsEnum => Kind == FieldKind.Enum;
    public bool IsVector2 => Kind == FieldKind.Vector2;
    public bool IsVector3 => Kind == FieldKind.Vector3;
    public bool IsColor4 => Kind == FieldKind.Color4;
    public bool IsText => !IsBool && !IsEnum && !IsVector2 && !IsVector3 && !IsColor4;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
