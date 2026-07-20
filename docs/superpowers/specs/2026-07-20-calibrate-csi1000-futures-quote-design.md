# Calibrate CSI 1000 Futures Continuous Quote

## Goal

Display the latest traded price for `中千主连` (`nf_IM0`) instead of a volume or position field from the Sina response.

## Root Cause

WinDesk currently assigns the generic `futures` parser to `nf_IM0`. That parser defaults to field index `6`, which is the latest-price field for commodity futures such as `nf_AO0` and `nf_M0`.

Sina uses a different response layout for CFFEX futures. Live responses for `nf_IM0`, `nf_IF0`, and `nf_IC0` place the latest traded price at field index `3`; field index `6` contains a non-price statistic. For example, one `nf_IM0` response contained `7030.000` at index `3` and `258552.000` at index `6`.

## Design

Keep the existing parser types and use the supported per-market override:

- Set `priceFieldIndex` to `3` for `nf_IM0` in `appsettings.json`.
- Set `PriceFieldIndex` to `3` for `nf_IM0` in `AppConfig.Default.Markets`.
- When loading a saved user configuration, copy a non-null default `PriceFieldIndex` to the matching saved market only when the saved value is null.
- Preserve every explicit user value, including a non-default field index.

This keeps the CFFEX-specific choice visible in configuration, avoids symbol heuristics in `QuoteService`, and leaves commodity futures behavior unchanged.

## Data Flow

1. `SettingsStore` loads the saved user configuration when present.
2. Default markets are merged by symbol.
3. For an existing market whose `PriceFieldIndex` is null, the calibrated non-null default is applied and the migrated user configuration is saved.
4. `QuoteService.ResolvePriceFieldIndex` uses the configured index before the parser default.
5. `nf_IM0` therefore reads field index `3` and formats it as the displayed latest price.

## Testing

- Assert the default `nf_IM0` market uses `PriceFieldIndex == 3`.
- Load a saved `nf_IM0` entry without `priceFieldIndex`; assert it is migrated to `3` and persisted.
- Load a saved `nf_IM0` entry with an explicit custom index; assert it is preserved.
- Run the full test executable and Release build.
- Re-read a live Sina `nf_IM0` response and compare the displayed field with the response's latest-price field.

## Scope

No parser enum, symbol-prefix heuristic, UI change, or unrelated market migration is included.
