# Telemorph

<img align="right" src="img/Telemorph.png" width="128" height="128" alt="Logo">

Telemorph converts animated images such as GIF, animated WebP, and APNG into
VP9 WebM files suitable for Telegram video stickers and custom emoji.

- Sticker profile: 512px canvas or variable height, up to 3 seconds and 30 FPS
- Emoji profile: 100×100, up to 3 seconds and 30 FPS
- VP9 with alpha (`yuva420p`)
- Automatic CRF selection for a target file size
- Live per-attempt encoding progress
- Direct input-to-WebM conversion with no ImageMagick or intermediate PNG files

## Installation

Download the archive for your platform from GitHub Releases and extract it.
Release archives contain:

- the self-contained Telemorph executable;
- pinned `ffmpeg` and `ffprobe` executables;
- third-party license notices.

No .NET runtime, ImageMagick, FFmpeg, or ffprobe installation is required for
these archives.

Run the built-in diagnostic after extraction:

```bash
telemorph --doctor
```

When running from source, FFmpeg and ffprobe must be available on `PATH`, or
provided with `--ffmpeg` and `--ffprobe`.

## Build

Requires the .NET 10 SDK:

```bash
dotnet build src/Telemorph.slnx -c Release
dotnet test src/Telemorph.slnx -c Release
```

Run from source:

```bash
dotnet run --project src/Telemorph.App -- input.gif --sticker
```

## Usage

```bash
telemorph <input> [options]
```

Common options:

- `--output, -o <path>` — output WebM path.
- `--sticker, -s` — 512px Telegram sticker profile; this is the default.
- `--emoji, -e` — 100×100 Telegram custom emoji profile.
- `--crf, -c <0..51>` — starting VP9 CRF; default `38`.
- `--fps, -f <value>` — maximum output FPS; default `30`.
- `--duration, -d <seconds>` — maximum duration; default `3`.
- `--max-size-kb <KiB>` — target maximum size; default `256`.
- `--max-attempts <count>` — maximum CRF search attempts; default `6`.
- `--no-optimize` — perform a single encode even if it exceeds the target size.
- `--fit-duration` — speed up a long animation instead of cutting it.
- `--variable-height` — make one sticker side 512px and preserve the other side.
- `--threads, -t <count>` — number of encoder threads.
- `--no-row-mt` — disable libvpx row-based multithreading.
- `--no-overwrite` — fail if the requested output already exists.
- `--ffmpeg <path>` / `--ffprobe <path>` — override bundled tools.
- `--doctor` — verify the media toolchain and `libvpx-vp9`.

Examples:

```bash
telemorph animation.gif
telemorph animation.webp --emoji
telemorph long.gif --sticker --fit-duration
telemorph noisy.gif --max-size-kb 200 --max-attempts 8
telemorph input.gif --no-optimize --crf 42
```

## Automatic size optimization

The initial CRF is treated as the preferred quality. Telemorph:

1. probes the source;
2. creates a conversion plan;
3. encodes directly from the source with FFmpeg;
4. probes and validates the resulting WebM;
5. if the file is too large, searches higher CRF values for the smallest
   quality reduction that meets the requested limit.

Every attempt is written to a temporary WebM. The requested output path is
replaced only after structural validation succeeds. A successful result is
checked for VP9, alpha metadata, dimensions, duration, FPS, and file size.

In an interactive terminal, Telemorph shows an in-place FFmpeg progress bar for
each CRF attempt and then records its resulting size. When output is redirected
to a file or CI log, progress automatically switches to stable line-by-line
messages without terminal control characters.

If CRF 51 still cannot meet the limit, conversion fails without leaving a
partially validated output. Reduce FPS, duration, or visual complexity in that
case.

## Architecture

The core conversion flow is intentionally split into independent stages:

```text
FfmpegProbe
    → ConversionPlanner
    → FfmpegEncoder
    → OutputValidator
    → ConversionOptimizer
```

`ConversionPipeline` coordinates these stages. ImageMagick is not used.
FFmpeg receives the original animation directly, while ffprobe supplies source
metadata and validates every encoded candidate.

## Release packaging

Pull requests from `dev` to `main` run formatting checks, a warnings-as-errors
build, and the full test suite. Pushing or merging a commit into `main` does not
create release binaries.

Publishing a GitHub Release starts the release workflow from that release's tag
and produces self-contained builds for:

- `win-x64`
- `win-arm64`
- `linux-x64`
- `linux-arm64`
- `osx-arm64`

FFmpeg and ffprobe are restored from exact npm dependency versions using the
committed lockfile. The Windows ARM64 release uses a pinned BtbN x64 media
backend through Windows 11 ARM emulation because the native build lacks
`libvpx-vp9`; its archive has an explicit SHA-256 check. The tools are copied
under `tools/<RID>/`, checked with `telemorph --doctor`, and exercised by a
transparent APNG-to-VP9 WebM smoke test before an archive is uploaded.

See [third-party notices](THIRD_PARTY_NOTICES.md) for FFmpeg licensing and
source information.

## Use case

I used Telemorph to convert some of my favorite [7TV](https://7tv.app/) emojis into Telegram video emojis and create my own [7TV emoji pack](https://t.me/addemoji/favorite7tv).

I also used it to convert several Tenor GIFs into Telegram stickers for my [shitpost sticker pack](https://t.me/addstickers/shitpost_hub).

> I recommend using the [@Stickers](https://t.me/Stickers) mini app to create a new sticker or emoji pack and upload your `.webm` video stickers or emojis to it.

## Contributing

PRs are welcome. Please keep the code style consistent with the existing files. If you add options, mirror them in this README.
