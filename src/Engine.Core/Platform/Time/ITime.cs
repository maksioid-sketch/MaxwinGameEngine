namespace Engine.Core.Platform.Time;

public interface ITime
{
    float DeltaSeconds { get; }
    double TotalSeconds { get; }
}
