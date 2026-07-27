# Third-party notices

Telemorph release archives include separate `ffmpeg` and `ffprobe` executables.
Telemorph invokes these programs as child processes; they are not linked into the
Telemorph assemblies.

The packaged executables are obtained through the pinned `ffmpeg-static` 5.3.0
and `@derhuerst/ffprobe-static` 5.3.0 npm packages. The exact npm package
contents are verified by the committed `eng/ffmpeg/package-lock.json`.

The Windows ARM64 archive uses the native BtbN FFmpeg build
`ffmpeg-n8.0-30-g71007e6c12-winarm64-gpl-8.0.zip`, pinned to its immutable
release URL and verified against SHA-256
`5276af84b736b279a70514c5b7e236f03961ab5517d911a88371838459b94c40`.

FFmpeg is licensed separately from Telemorph. The packaged builds can include
GPL-licensed components. Copyright, license, corresponding-source, and build
information are available from:

- https://ffmpeg.org/legal.html
- https://ffmpeg.org/download.html
- https://github.com/eugeneware/ffmpeg-static
- https://github.com/eugeneware/ffmpeg-static/tree/master/packages/ffprobe-static
- https://github.com/BtbN/FFmpeg-Builds

Run `telemorph --doctor` to print the exact packaged FFmpeg and ffprobe versions.
