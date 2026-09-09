namespace GhostPrompter.Services;

/// <summary>Maintains time-independent scrolling state expressed in device-independent pixels.</summary>
public sealed class ScrollController
{
    public const double MinimumSpeed = 10;
    public const double MaximumSpeed = 300;
    public double Offset { get; private set; }
    public double Speed { get; private set; } = 50;
    public double MaximumOffset { get; private set; }
    public bool IsRunning { get; private set; }
    public double Progress => MaximumOffset <= 0 ? (HasContent ? 1 : 0) : Math.Clamp(Offset / MaximumOffset, 0, 1);
    public bool HasContent { get; private set; }

    /// <summary>Configures the extent while preserving a valid offset.</summary>
    public void Configure(double maximumOffset, bool hasContent)
    {
        MaximumOffset = Math.Max(0, maximumOffset);
        HasContent = hasContent;
        Offset = Math.Clamp(Offset, 0, MaximumOffset);
        if (!hasContent || MaximumOffset <= 0) IsRunning = false;
    }

    /// <summary>Advances playback by a monotonic elapsed duration.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (!IsRunning || elapsed <= TimeSpan.Zero) return;
        Offset = Math.Min(MaximumOffset, Offset + Speed * elapsed.TotalSeconds);
        if (Offset >= MaximumOffset) IsRunning = false;
    }

    public void Play() { if (HasContent && Offset < MaximumOffset) IsRunning = true; }
    public void Pause() => IsRunning = false;
    public void Restart() { IsRunning = false; Offset = 0; }
    public void SetSpeed(double speed) => Speed = Math.Clamp(Math.Round(speed / 10) * 10, MinimumSpeed, MaximumSpeed);
    public void ChangeSpeed(double delta) => SetSpeed(Speed + delta);
    public void SetOffset(double offset) => Offset = Math.Clamp(offset, 0, MaximumOffset);
}
