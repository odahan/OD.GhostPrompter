using GhostPrompter.Services;

namespace GhostPrompter.Tests;

/// <summary>Tests scroll position, speed bounds, pause behavior and end handling.</summary>
public sealed class ScrollControllerTests
{
    [Fact]
    public void Advance_UsesElapsedTimeAndStopsAtTheEnd()
    {
        var controller = new ScrollController();
        controller.Configure(100, hasContent: true);
        controller.Play();

        controller.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(50, controller.Offset);
        Assert.True(controller.IsRunning);

        controller.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(100, controller.Offset);
        Assert.Equal(1, controller.Progress);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void Speed_IsClampedToTheSupportedRange()
    {
        var controller = new ScrollController();
        controller.SetSpeed(-40);
        Assert.Equal(ScrollController.MinimumSpeed, controller.Speed);
        controller.SetSpeed(999);
        Assert.Equal(ScrollController.MaximumSpeed, controller.Speed);
    }

    [Fact]
    public void Speed_UsesTwoDipIncrementsAcrossTheHumanReadableRange()
    {
        var controller = new ScrollController();
        controller.SetSpeed(3);
        Assert.Equal(4, controller.Speed);

        controller.ChangeSpeed(-2);
        Assert.Equal(2, controller.Speed);
        Assert.Equal(2, ScrollController.MinimumSpeed);
        Assert.Equal(150, ScrollController.MaximumSpeed);
    }

    [Fact]
    public void EmptyContent_CannotStartPlayback()
    {
        var controller = new ScrollController();
        controller.Configure(100, hasContent: false);
        controller.Play();

        Assert.False(controller.IsRunning);
        Assert.Equal(0, controller.Progress);
    }
}
