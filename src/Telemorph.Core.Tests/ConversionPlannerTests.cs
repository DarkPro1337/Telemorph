using Telemorph.Core.Models;
using Telemorph.Core.Pipeline;
using Xunit;

namespace Telemorph.Core.Tests;

public sealed class ConversionPlannerTests
{
    [Fact]
    public void Create_UsesRequestedFpsAndFitsLongInput()
    {
        var options = CreateOptions(
            ConversionProfile.Sticker(maxFps: 24, maxDurationSeconds: 3, variableHeight: false),
            fitToMaxDuration: true);

        var source = new MediaInfo("gif", "gif", 320, 180, 6, 20, true);
        var plan = new ConversionPlanner().Create(options, source, "attempt.webm", 41);

        Assert.Contains("setpts=0.5*PTS", plan.VideoFilter);
        Assert.Contains("fps=fps=24", plan.VideoFilter);
        Assert.Contains("scale=512:512", plan.VideoFilter);
        Assert.Contains("pad=512:512", plan.VideoFilter);
        Assert.EndsWith("format=yuva420p", plan.VideoFilter);
    }

    [Fact]
    public void Create_CutsInsteadOfRetimeWhenFitIsDisabled()
    {
        var options = CreateOptions(
            ConversionProfile.Emoji(maxFps: 30, maxDurationSeconds: 3),
            fitToMaxDuration: false);

        var source = new MediaInfo("webp", "webp", 128, 128, 8, 25, true);
        var plan = new ConversionPlanner().Create(options, source, "attempt.webm", 38);

        Assert.DoesNotContain("setpts=", plan.VideoFilter);
        Assert.Contains("fps=fps=30", plan.VideoFilter);
        Assert.Contains("scale=100:100", plan.VideoFilter);
        Assert.Contains("pad=100:100", plan.VideoFilter);
    }

    [Fact]
    public void Create_PreservesAspectRatioForVariableHeightSticker()
    {
        var options = CreateOptions(
            ConversionProfile.Sticker(maxFps: 30, maxDurationSeconds: 3, variableHeight: true),
            fitToMaxDuration: false);

        var source = new MediaInfo("apng", "apng", 400, 200, 2, 12, true);
        var plan = new ConversionPlanner().Create(options, source, "attempt.webm", 38);

        Assert.Contains("if(gt(iw,ih),512,-2)", plan.VideoFilter);
        Assert.DoesNotContain("pad=", plan.VideoFilter);
    }

    private static ConversionOptions CreateOptions(ConversionProfile profile, bool fitToMaxDuration)
    {
        return new ConversionOptions
        {
            InputPath = "input.gif",
            OutputPath = "output.webm",
            Profile = profile,
            FitToMaxDuration = fitToMaxDuration
        };
    }
}
