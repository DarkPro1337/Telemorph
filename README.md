# Telemorph

<img align="right" src="img/Telemorph.png" width="128" height="128" alt="Logo">

CLI tool for converting animated images (GIF/WebP/etc.) into VP9 WEBM files for Telegram video stickers and video emoji.

- Sticker mode: 512×512 canvas, up to 3 seconds, up to 30 fps
- Emoji mode: 100×100 canvas, up to 3 seconds, up to 30 fps
- Encodes with `libvpx-vp9` and preserves alpha (uses `yuva420p`)

Internally, Telemorph uses ImageMagick to extract frames and timing, then feeds a timestamped concat script to FFmpeg for high‑quality VP9 encoding.

---

## Requirements

To run the app, you will need:
- [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0/runtime)
- [FFmpeg (ffmpeg)](https://www.ffmpeg.org/download.html) available on `PATH` or provided via `--ffmpeg`
- [ImageMagick (magick)](https://imagemagick.org/script/download.php) available on `PATH` or provided via `--magick`

To build from source, you will also need:
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

Windows, macOS, and Linux are supported as long as FFmpeg and ImageMagick are installed.

> Telegram size guidance: a final file should be ≤ 256 KB. Going above may be rejected by Telegram clients.

---

## Build

From the repo root:

```bash
# Build all projects
dotnet build -c Release

# Or run the app directly
dotnet run --project src/Telemorph.App -- <args>
```

---

## Usage

Basic form:

```bash
telemorph <input> [options]
```

Options (from `Program.cs`):

- `--output, -o <path>` — Output `.webm` path. If omitted, uses `<input>_<mode>.webm`.
- `--emoji, -e` — Convert to Telegram custom emoji (100×100). Mutually exclusive with `--sticker`.
- `--sticker, -s` — Convert to Telegram video sticker (512×512 canvas). Mutually exclusive with `--emoji`.
- `--crf, -q <int>` — CRF for VP9 (higher = smaller file, lower quality). Default: `38`.
- `--ffmpeg <path>` — Path to `ffmpeg` executable. Default: `ffmpeg` (from PATH).
- `--magick <path>` — Path to ImageMagick `magick` executable. Default: `magick` (from PATH).
- `--fit-duration, -fd` — Fit the whole animation into the max duration by proportionally scaling frame delays (time‑stretch) instead of cutting.

Notes:
- If neither `--emoji` nor `--sticker` is specified, the app defaults to `--sticker`.
- If both are specified, the app will exit with an error.

---

## Examples

Convert a GIF to a Telegram sticker (defaults, CRF=38):

```bash
telemorph my_anim.gif --sticker
```

Convert a WebP to a custom emoji, set CRF for stronger compression:

```bash
telemorph funny.webp --emoji -q 42
```

Keep the entire animation by scaling time to fit 3 seconds (rather than cutting):

```bash
telemorph long.gif --sticker --fit-duration
```

Explicitly point to tools if they are not on PATH:

```bash
telemorph in.gif --sticker \
  --ffmpeg "/usr/local/bin/ffmpeg" \
  --magick "/usr/local/bin/magick"
```

Write output to a specific file:

```bash
telemorph in.gif --emoji -o out_emoji.webm
```

---

## Tips for staying under 256 KB

- Increase CRF (e.g., `-q 42`, `-q 45`). Higher CRF reduces size at the cost of quality.
- Shorten the animation to ≤ 3 seconds (default behavior is to cut; `--fit-duration` will time‑scale instead).
- Reduce visual complexity: crop/pad instead of scaling up, remove noise, simplify frames.
- Start from smaller source dimensions (especially for emoji), so less detail needs to be encoded.

After conversion, Telemorph prints the output size and warns if it exceeds 256 KB.

---

## How it works

1. ImageMagick (`magick`) extracts frames with alpha and normalizes them (`-coalesce -alpha set -background none`).
2. Frame delays are read via `magick identify` and converted from centiseconds to seconds.
3. Telemorph generates an `ffconcat` file with accurate per‑frame timestamps.
4. FFmpeg encodes the sequence using `libvpx-vp9` with alpha (`yuva420p`), variable frame rate, scaling, and padding to the selected canvas.

Key limits baked into profiles (see `Telemorph.Core`):
- Max duration: 3.0 seconds
- Max fps: 30
- Sticker canvas: 512×512
- Emoji canvas: 100×100

---

## Troubleshooting

- "Input file not found": check the path and permissions to the source file.
- "Choose either --emoji or --sticker": the two modes are mutually exclusive.
- "ffmpeg/ImageMagick not found": ensure they are installed and reachable on PATH, or pass `--ffmpeg`/`--magick` with full paths.
- Output is too large (> 256 KB): try higher `--crf` (e.g., 42–45), reduce duration/fps/complexity, or start from a simpler source.

---

## Use case

I used Telemorph to convert some of my favorite [7TV](https://7tv.app/) emojis into Telegram video emojis and create my own [7TV emoji pack](https://t.me/addemoji/favorite7tv).

I also used it to convert several Tenor GIFs into Telegram stickers for my [shitpost sticker pack](https://t.me/addstickers/shitpost_hub).

> I recommend using the [@Stickers](https://t.me/Stickers) mini app to create a new sticker or emoji pack and upload your `.webm` video stickers or emojis to it.

---

## Contributing

PRs are welcome. Please keep the code style consistent with the existing files. If you add options, mirror them in this README.