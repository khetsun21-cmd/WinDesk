# WinDesk

A small Windows .NET 8 WPF floating quote widget.

## Features

- Borderless topmost floating window
- Drag the window with the left mouse button
- Right-click the floating window to switch symbols, hide, or exit
- System tray icon with restore, hide, exit, and symbol switching
- Sina HTTP quote parsing for index, domestic futures, and global futures symbols
- OKX ticker JSON support for `data[0].last`
- Simple WebSocket JSON source support via `url`, `subscribeMessage`, and `jsonPricePath`
- Default settings from `appsettings.json`; runtime changes are saved to `%AppData%\\WinDesk\\appsettings.json`

## Run

```powershell
dotnet run --project .\WinDesk.csproj
```

## Publish single exe

```powershell
dotnet publish .\WinDesk.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Keep the published artifact unique. After publishing, keep only:

```text
bin\Release\net8.0-windows\win-x64\publish\WinDesk.exe
```

Do not leave extra Debug, intermediate Release, satellite-resource, or `obj` outputs around. If extra build output is created, clean it so the `bin` tree contains only the published `WinDesk.exe`.

Use `--self-contained true` if the target machine does not have the .NET 8 Desktop Runtime installed.

## Configure symbols

Edit `appsettings.json` before first run, or edit `%AppData%\\WinDesk\\appsettings.json` after the app has saved runtime settings.

Sina parser defaults:

- `index`: price field `1`, e.g. `s_sh000852`
- `futures`: price field `6`, e.g. `nf_AO0`, `nf_M0`
- `global`: price field `0`, e.g. `hf_GC`, `hf_CL`

Sina uses a different field layout for CFFEX futures. The `nf_IM0` market sets `priceFieldIndex` to `3` to read its latest traded price.

For a custom Sina field, set `priceFieldIndex` on a market entry.

For a simple WebSocket JSON feed, use:

```json
{
  "name": "Example",
  "displayCode": "EX",
  "symbol": "EXAMPLE",
  "source": "webSocketJson",
  "parser": "okxTicker",
  "url": "wss://example.com/quotes",
  "subscribeMessage": "{\"op\":\"subscribe\",\"symbol\":\"EXAMPLE\"}",
  "jsonPricePath": "data[0].last"
}
```
