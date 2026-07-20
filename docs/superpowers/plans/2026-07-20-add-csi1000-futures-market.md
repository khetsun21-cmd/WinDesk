# Add 中千主连 Market Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the CSI 1000 futures continuous contract (中千主连, `nf_IM0`) to WinDesk's market configuration.

**Architecture:** Add one market entry to both `appsettings.json` (runtime config) and `AppConfig.Default.Markets` (default config). No C# code changes are needed because `QuoteService` already supports Sina futures quotes.

**Tech Stack:** C# / WPF, .NET 8, System.Text.Json

## Global Constraints

- Keep changes minimal; this is a configuration-only addition.
- Follow the existing market entry format exactly.
- Use `nf_IM0` as the Sina symbol for 中证1000股指期货主连.
- Use the existing `futures` parser and `CCCCCC` color.

---

### Task 1: Add market entry to appsettings.json

**Files:**
- Modify: `appsettings.json`

**Interfaces:**
- Consumes: existing `markets` array schema
- Produces: updated `markets` array with 中千主连 entry

- [ ] **Step 1: Insert the market entry**

Add the following object to the `markets` array in `appsettings.json`, after the existing `合成橡胶2609` entry and before the `BTC永续` entry:

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
    },
```

- [ ] **Step 2: Verify JSON is valid**

Run: `dotnet build`
Expected: Build succeeds (JSON is loaded during build/publish).

- [ ] **Step 3: Commit**

```bash
git add appsettings.json
git commit -m "config: add 中千主连 to appsettings.json"
```

---

### Task 2: Add market entry to AppConfig.Default

**Files:**
- Modify: `Models/AppConfig.cs`

**Interfaces:**
- Consumes: existing `MarketDefinition` class and `Default` property
- Produces: updated `Default.Markets` list

- [ ] **Step 1: Insert the market definition**

Add the following `MarketDefinition` to `AppConfig.Default.Markets` in `Models/AppConfig.cs`, after `合成橡胶2609` and before the closing of the collection:

```csharp
            new MarketDefinition
            {
                Name = "中千主连",
                DisplayCode = "IM0",
                Symbol = "nf_IM0",
                UseRn = true,
                Referer = "https://vip.stock.finance.sina.com.cn/",
                Source = QuoteSource.Sina,
                Parser = QuoteParser.Futures,
                Color = "CCCCCC"
            }
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add Models/AppConfig.cs
git commit -m "config: add 中千主连 to default markets"
```

---

### Task 3: Verify end-to-end

**Files:**
- None (runtime verification)

- [ ] **Step 1: Run WinDesk**

Run: `dotnet run --project WinDesk.csproj`

- [ ] **Step 2: Select 中千主连**

Right-click the tray icon or the floating window, select "中千主连", and confirm the price updates.

- [ ] **Step 3: Commit verification notes (optional)**

No code changes; verification is manual.
