using System.Globalization;
using Telemorph.Core.Models;

namespace Telemorph.Core.Pipeline;

public sealed class ConversionPlanner
{
    public ConversionPlan Create(ConversionOptions options, MediaInfo source, string outputPath, int crf)
    {
        var filters = new List<string>();

        if (options.FitToMaxDuration && source.DurationSeconds > options.Profile.MaxDurationSeconds && source.DurationSeconds > 0)
        {
            var timeScale = options.Profile.MaxDurationSeconds / source.DurationSeconds;
            filters.Add($"setpts={timeScale.ToString("0.########", CultureInfo.InvariantCulture)}*PTS");
        }

        filters.Add($"fps=fps={options.Profile.MaxFps}");
        filters.Add(BuildScaleFilter(options.Profile));
        filters.Add("format=yuva420p");

        return new ConversionPlan(
            options.InputPath,
            outputPath,
            options.Profile,
            source,
            crf,
            options.Threads,
            options.EnableRowMultithreading,
            options.Overwrite,
            string.Join(',', filters));
    }

    private static string BuildScaleFilter(ConversionProfile profile)
    {
        if (profile is { Kind: TargetKind.Sticker, VariableHeight: true })
        {
            var maxSide = profile.Width;
            return $"scale='if(gt(iw,ih),{maxSide},-2)':'if(gt(iw,ih),-2,{maxSide})':flags=lanczos";
        }

        return
            $"scale={profile.Width}:{profile.Height}:force_original_aspect_ratio=decrease:flags=lanczos," +
            $"pad={profile.Width}:{profile.Height}:(ow-iw)/2:(oh-ih)/2:color=0x00000000";
    }
}
