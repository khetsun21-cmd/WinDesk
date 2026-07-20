# Fixed Publish Path for WinDesk

## Goal

Fix the WinDesk publish output so the single-file exe always lands in `D:\proj\WinDesk\publish\WinDesk.exe`, clean stale build artifacts, and produce a fresh publish build.

## Design

1. **Pin publish directory** — add `<PublishDir>$(ProjectDir)publish\</PublishDir>` to the existing Release `PropertyGroup` in `WinDesk.csproj`.
2. **Clean redundant build output** — delete `bin\` and `obj\` folders under `WinDesk\` and `Tests\WinDesk.Tests\`.
3. **Publish** — run `dotnet publish -c Release -r win-x64`.
4. **Verify** — confirm `publish\WinDesk.exe` exists and no stale deep `bin\Release\...\publish\` copy remains.

## Constraints

- Keep `PublishSingleFile=true` and `SelfContained=false` (framework-dependent).
- Preserve all other existing publish settings (`DebugType=none`, app icon, embedded appsettings.json).

## Testing

- Build succeeds with no warnings/errors.
- `publish\WinDesk.exe` is produced.
- No `bin\` or `obj\` directories remain after cleanup.
