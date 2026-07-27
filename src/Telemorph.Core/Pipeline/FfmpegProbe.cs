using System.Globalization;
using System.Text.Json;
using Telemorph.Core.Infrastructure;
using Telemorph.Core.Models;

namespace Telemorph.Core.Pipeline;

public sealed class FfmpegProbe(FfmpegToolchain toolchain)
{
    public async Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            toolchain.FfprobePath,
            [
                "-v", "error",
                "-print_format", "json",
                "-show_entries",
                "format=format_name,duration:stream=codec_type,codec_name,width,height,pix_fmt,duration,avg_frame_rate,r_frame_rate:stream_tags=alpha_mode",
                path
            ],
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed with exit code {result.ExitCode}:{Environment.NewLine}{result.StandardError}");

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;

        if (!root.TryGetProperty("streams", out var streams))
            throw new InvalidOperationException("The input contains no media streams.");

        var video = streams.EnumerateArray().FirstOrDefault(stream =>
            GetString(stream, "codec_type").Equals("video", StringComparison.OrdinalIgnoreCase));

        if (video.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("The input contains no video stream.");

        var format = root.TryGetProperty("format", out var formatElement) ? formatElement : default;
        var duration = GetDouble(video, "duration");
        if (duration <= 0 && format.ValueKind != JsonValueKind.Undefined)
            duration = GetDouble(format, "duration");

        var fps = ParseRate(GetString(video, "avg_frame_rate"));
        if (fps <= 0)
            fps = ParseRate(GetString(video, "r_frame_rate"));

        var pixelFormat = GetString(video, "pix_fmt");
        var alphaMode = video.TryGetProperty("tags", out var tags)
            ? GetString(tags, "alpha_mode")
            : string.Empty;

        return new MediaInfo(
            GetString(format, "format_name"),
            GetString(video, "codec_name"),
            GetInt(video, "width"),
            GetInt(video, "height"),
            duration,
            fps,
            pixelFormat.Contains('a', StringComparison.OrdinalIgnoreCase) || alphaMode == "1");
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return string.Empty;

        if (element.TryGetProperty(propertyName, out var property))
            return property.ToString();

        foreach (var candidate in element.EnumerateObject())
        {
            if (candidate.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                return candidate.Value.ToString();
        }

        return string.Empty;
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Undefined &&
            element.TryGetProperty(propertyName, out var property) &&
            property.TryGetInt32(out var value))
            return value;

        return 0;
    }

    private static double GetDouble(JsonElement element, string propertyName)
    {
        if (double.TryParse(GetString(element, propertyName), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return 0;
    }

    private static double ParseRate(string value)
    {
        var parts = value.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            denominator != 0)
            return numerator / denominator;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct)
            ? direct
            : 0;
    }
}
