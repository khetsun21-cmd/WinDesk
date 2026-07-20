# Add 中千主连 (CSI 1000 Futures Continuous) Market

## Goal

Add the CSI 1000 index futures continuous contract to WinDesk's market list so it can be selected and quoted alongside existing futures markets.

## Design

Add one market entry to both configuration locations:

- `appsettings.json` `markets` array
- `AppConfig.Default.Markets` in `Models/AppConfig.cs`

No C# code changes are required. The existing `QuoteService` already supports Sina futures quotes via the `nf_*` symbol prefix and the `Futures` parser (price field index 6).

## Market Definition

```json
{
  "name": "中千主连",
  "displayCode": "IM0",
  "symbol": "nf_IM0",
  "useRn": true,
  "referer": "https://vip.stock.finance.sina.com.cn/",
  "source": "sina",
  "parser": "futures",
  "color": "CCCCCC"
}
```

## Verification

1. Build the project.
2. Run WinDesk and open the tray/context menu.
3. Select "中千主连" and confirm the price updates from Sina.
