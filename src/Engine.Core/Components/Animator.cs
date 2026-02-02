using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Engine.Core.Components;

public sealed class Animator : IComponent
{
    // Which clip to play (from animations.generated.json)
    public string ClipId { get; set; } = "";

    public bool Playing { get; set; } = true;
    public bool LoopOverride { get; set; } = false; // if true, use Loop instead of clip.Loop
    public bool Loop { get; set; } = true;

    public float Speed { get; set; } = 1f; // 1.0 = normal speed

    // Runtime playback state
    public int FrameIndex { get; set; } = 0;
    public float TimeIntoFrame { get; set; } = 0f;

    [JsonIgnore] public string? NextClipId { get; set; } = null;
    [JsonIgnore] public bool ResetRequested { get; set; } = false;

    // ----------------------------
    // Controller state
    // ----------------------------
    public string ControllerId { get; set; } = ""; // e.g. "player"

    [JsonIgnore] public string StateId { get; set; } = "";
    [JsonIgnore] public string? PendingStateId { get; set; } = null;

    // ----------------------------
    // Parameters (serializable)
    // ----------------------------
    public Dictionary<string, float> Floats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Bools { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Ints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ----------------------------
    // Triggers (runtime-only)
    // ----------------------------
    [JsonIgnore] private readonly HashSet<string> _triggers = new(StringComparer.OrdinalIgnoreCase);

    public void SetTrigger(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _triggers.Add(name);
    }

    public bool HasTrigger(string name)
        => !string.IsNullOrWhiteSpace(name) && _triggers.Contains(name);

    public bool ConsumeTrigger(string name)
        => !string.IsNullOrWhiteSpace(name) && _triggers.Remove(name);

    public float GetFloat(string name, float fallback = 0f)
        => Floats.TryGetValue(name, out var v) ? v : fallback;

    public bool GetBool(string name, bool fallback = false)
        => Bools.TryGetValue(name, out var v) ? v : fallback;

    public int GetInt(string name, int fallback = 0)
        => Ints.TryGetValue(name, out var v) ? v : fallback;

    // ----------------------------
    // NEW: robust timing signals
    // ----------------------------
    [JsonIgnore] public bool ClipFinishedThisFrame { get; set; } = false;

    [JsonIgnore] public float ClipTimeSeconds { get; set; } = 0f;
    [JsonIgnore] public float ClipLengthSeconds { get; set; } = 0f;

    [JsonIgnore] public float StateTimeSeconds { get; set; } = 0f;

    [JsonIgnore]
    public float NormalizedTime
        => ClipLengthSeconds > 0f ? MathF.Min(1f, MathF.Max(0f, ClipTimeSeconds / ClipLengthSeconds)) : 0f;
}
