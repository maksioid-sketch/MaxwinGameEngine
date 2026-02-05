using System.Globalization;
using System.Numerics;
using Engine.Core.Components;
using Engine.Core.Math;
using Engine.Core.Rendering;

namespace Engine.Core.Inspection;

public enum FieldKind
{
    Float,
    Int,
    Bool,
    String,
    Vector2,
    Vector3,
    Color4,
    Enum
}

public sealed class FieldDescriptor
{
    public string Name { get; }
    public FieldKind Kind { get; }
    public object? DefaultValue { get; }
    public float? Min { get; }
    public float? Max { get; }
    public string? Tooltip { get; }
    public Type? EnumType { get; }
    public Func<object, object?>? Getter { get; }
    public Action<object, object?>? Setter { get; }

    public FieldDescriptor(
        string name,
        FieldKind kind,
        object? defaultValue = null,
        float? min = null,
        float? max = null,
        string? tooltip = null,
        Type? enumType = null,
        Func<object, object?>? getter = null,
        Action<object, object?>? setter = null)
    {
        Name = name;
        Kind = kind;
        DefaultValue = defaultValue;
        Min = min;
        Max = max;
        Tooltip = tooltip;
        EnumType = enumType;
        Getter = getter;
        Setter = setter;
    }
}

public sealed class ComponentDescriptor
{
    public Type Type { get; }
    public string DisplayName { get; }
    public IReadOnlyList<FieldDescriptor> Fields { get; }

    public ComponentDescriptor(Type type, string displayName, IReadOnlyList<FieldDescriptor> fields)
    {
        Type = type;
        DisplayName = displayName;
        Fields = fields;
    }
}

public static class ComponentRegistry
{
    private static readonly Dictionary<Type, ComponentDescriptor> ByType = new();

    static ComponentRegistry()
    {
        RegisterBuiltIns();
    }

    public static ComponentDescriptor? TryGet(Type type)
        => ByType.TryGetValue(type, out var d) ? d : null;

    public static ComponentDescriptor? TryGet<T>()
        => TryGet(typeof(T));

    public static IReadOnlyList<ComponentDescriptor> All()
        => ByType.Values.OrderBy(d => d.DisplayName).ToList();

    public static void Register(ComponentDescriptor descriptor)
    {
        if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));
        ByType[descriptor.Type] = descriptor;
    }

    private static void RegisterBuiltIns()
    {
        Register(new ComponentDescriptor(
            typeof(Transform),
            "Transform",
            new List<FieldDescriptor>
            {
                Field<Transform, Vector3>(
                    "Position", FieldKind.Vector3,
                    t => t.Position, (t, v) => t.Position = v,
                    defaultValue: Vector3.Zero),
                Field<Transform, float>(
                    "RotationZDegrees", FieldKind.Float,
                    t => RadToDeg(GetZRotationRadians(t.Rotation)),
                    (t, v) => t.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, DegToRad(v)),
                    defaultValue: 0f),
                Field<Transform, Vector3>(
                    "Scale", FieldKind.Vector3,
                    t => t.Scale, (t, v) => t.Scale = v,
                    defaultValue: Vector3.One)
            }));

        Register(new ComponentDescriptor(
            typeof(SpriteRenderer),
            "SpriteRenderer",
            new List<FieldDescriptor>
            {
                Field<SpriteRenderer, string>(
                    "SpriteId", FieldKind.String,
                    s => s.SpriteId, (s, v) => s.SpriteId = v,
                    defaultValue: string.Empty),
                Field<SpriteRenderer, int>(
                    "Layer", FieldKind.Int,
                    s => s.Layer, (s, v) => s.Layer = v,
                    defaultValue: 0),
                Field<SpriteRenderer, Color4>(
                    "Tint", FieldKind.Color4,
                    s => s.Tint, (s, v) => s.Tint = v,
                    defaultValue: Color4.White),
                Field<SpriteRenderer, SpriteFlip>(
                    "Flip", FieldKind.Enum,
                    s => s.Flip, (s, v) => s.Flip = v,
                    defaultValue: SpriteFlip.None,
                    enumType: typeof(SpriteFlip))
            }));

        Register(new ComponentDescriptor(
            typeof(Animator),
            "Animator",
            new List<FieldDescriptor>
            {
                Field<Animator, string>(
                    "ControllerId", FieldKind.String,
                    a => a.ControllerId, (a, v) => a.ControllerId = v,
                    defaultValue: string.Empty),
                Field<Animator, string>(
                    "ClipId", FieldKind.String,
                    a => a.ClipId, (a, v) => a.ClipId = v,
                    defaultValue: string.Empty),
                Field<Animator, bool>(
                    "Playing", FieldKind.Bool,
                    a => a.Playing, (a, v) => a.Playing = v,
                    defaultValue: true),
                Field<Animator, float>(
                    "Speed", FieldKind.Float,
                    a => a.Speed, (a, v) => a.Speed = v,
                    defaultValue: 1f, min: 0f),
                Field<Animator, bool>(
                    "LoopOverride", FieldKind.Bool,
                    a => a.LoopOverride, (a, v) => a.LoopOverride = v,
                    defaultValue: false),
                Field<Animator, bool>(
                    "Loop", FieldKind.Bool,
                    a => a.Loop, (a, v) => a.Loop = v,
                    defaultValue: true),
                Field<Animator, float>(
                    "DefaultCrossFadeSeconds", FieldKind.Float,
                    a => a.DefaultCrossFadeSeconds, (a, v) => a.DefaultCrossFadeSeconds = v,
                    defaultValue: 0f, min: 0f),
                Field<Animator, bool>(
                    "DefaultFreezeDuringCrossFade", FieldKind.Bool,
                    a => a.DefaultFreezeDuringCrossFade, (a, v) => a.DefaultFreezeDuringCrossFade = v,
                    defaultValue: false)
            }));

        Register(new ComponentDescriptor(
            typeof(BoxCollider2D),
            "BoxCollider2D",
            new List<FieldDescriptor>
            {
                Field<BoxCollider2D, Vector2>(
                    "Size", FieldKind.Vector2,
                    b => b.Size, (b, v) => b.Size = v,
                    defaultValue: new Vector2(1f, 1f), min: 0.001f),
                Field<BoxCollider2D, Vector2>(
                    "Offset", FieldKind.Vector2,
                    b => b.Offset, (b, v) => b.Offset = v,
                    defaultValue: Vector2.Zero),
                Field<BoxCollider2D, bool>(
                    "IsTrigger", FieldKind.Bool,
                    b => b.IsTrigger, (b, v) => b.IsTrigger = v,
                    defaultValue: false)
            }));

        Register(new ComponentDescriptor(
            typeof(PhysicsBody2D),
            "PhysicsBody2D",
            new List<FieldDescriptor>
            {
                Field<PhysicsBody2D, bool>(
                    "IsStatic", FieldKind.Bool,
                    p => p.IsStatic, (p, v) => p.IsStatic = v,
                    defaultValue: false)
            }));

        Register(new ComponentDescriptor(
            typeof(Rigidbody2D),
            "Rigidbody2D",
            new List<FieldDescriptor>
            {
                Field<Rigidbody2D, float>(
                    "Mass", FieldKind.Float,
                    r => r.Mass, (r, v) => r.Mass = v,
                    defaultValue: 1f, min: 0.001f),
                Field<Rigidbody2D, Vector2>(
                    "Velocity", FieldKind.Vector2,
                    r => r.Velocity, (r, v) => r.Velocity = v,
                    defaultValue: Vector2.Zero),
                Field<Rigidbody2D, bool>(
                    "UseGravity", FieldKind.Bool,
                    r => r.UseGravity, (r, v) => r.UseGravity = v,
                    defaultValue: true),
                Field<Rigidbody2D, float>(
                    "GravityScale", FieldKind.Float,
                    r => r.GravityScale, (r, v) => r.GravityScale = v,
                    defaultValue: 1f),
                Field<Rigidbody2D, float>(
                    "LinearDrag", FieldKind.Float,
                    r => r.LinearDrag, (r, v) => r.LinearDrag = v,
                    defaultValue: 0f, min: 0f),
                Field<Rigidbody2D, float>(
                    "Friction", FieldKind.Float,
                    r => r.Friction, (r, v) => r.Friction = v,
                    defaultValue: 0.2f, min: 0f)
            }));

        Register(new ComponentDescriptor(
            typeof(DebugRender2D),
            "DebugRender2D",
            new List<FieldDescriptor>
            {
                Field<DebugRender2D, bool>(
                    "ShowCollider", FieldKind.Bool,
                    d => d.ShowCollider, (d, v) => d.ShowCollider = v,
                    defaultValue: false)
            }));
    }

    private static FieldDescriptor Field<TComponent, TValue>(
        string name,
        FieldKind kind,
        Func<TComponent, TValue> getter,
        Action<TComponent, TValue> setter,
        object? defaultValue = null,
        float? min = null,
        float? max = null,
        string? tooltip = null,
        Type? enumType = null)
        where TComponent : class
    {
        return new FieldDescriptor(
            name,
            kind,
            defaultValue ?? default(TValue),
            min,
            max,
            tooltip,
            enumType,
            getter: o => getter((TComponent)o),
            setter: (o, v) => setter((TComponent)o, ConvertTo<TValue>(v)));
    }

    private static TValue ConvertTo<TValue>(object? value)
    {
        if (value is TValue tv) return tv;
        if (value is null) return default!;

        var t = typeof(TValue);
        if (t.IsEnum)
        {
            if (value is string s)
                return (TValue)Enum.Parse(t, s, ignoreCase: true);
            return (TValue)Enum.ToObject(t, value);
        }

        return (TValue)Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
    }

    private static float GetZRotationRadians(Quaternion q)
    {
        var siny_cosp = 2f * (q.W * q.Z + q.X * q.Y);
        var cosy_cosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        return (float)System.Math.Atan2(siny_cosp, cosy_cosp);
    }

    private static float DegToRad(float degrees) => degrees * (MathF.PI / 180f);
    private static float RadToDeg(float radians) => radians * (180f / MathF.PI);
}
