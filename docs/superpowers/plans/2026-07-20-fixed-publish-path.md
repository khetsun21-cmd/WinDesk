# Fixed Publish Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pin WinDesk publish output to `D:\proj\WinDesk\publish\WinDesk.exe` and produce a fresh single-file exe.

**Architecture:** Add a `PublishDir` MSBuild property to the Release `PropertyGroup` in `WinDesk.csproj`, clean stale `bin/` and `obj/` directories, then run `dotnet publish`.

**Tech Stack:** .NET 8 WPF, MSBuild, `dotnet publish`

## Global Constraints

- Output path must be `$(ProjectDir)publish\`.
- Keep `PublishSingleFile=true` and `SelfContained=false`.
- Preserve existing Release publish settings (`DebugType=none`, app icon, embedded appsettings.json).
- Clean `bin\` and `obj\` under both `WinDesk\` and `Tests\WinDesk.Tests\`.

---

### Task 1: Pin publish directory in WinDesk.csproj

**Files:**
- Modify: `WinDesk.csproj`

**Interfaces:**
- Consumes: existing Release `PropertyGroup`
- Produces: updated Release `PropertyGroup` with `PublishDir`

- [ ] **Step 1: Add PublishDir property**

Add this line inside the existing Release `PropertyGroup` (after `PublishSingleFile`):

```xml
    <PublishDir>$(ProjectDir)publish\</PublishDir>
```

The Release group should look like:

```xml
  <PropertyGroup Condition="'$(Configuration)'=='Release'">
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>false</SelfContained>
    <PublishDir>$(ProjectDir)publish\</PublishDir>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>
```

- [ ] **Step 2: Commit**

```bash
git add WinDesk.csproj
git commit -m "build: pin publish output to $(ProjectDir)publish\"
```

---

### Task 2: Clean stale build artifacts

**Files:**
- Delete: `WinDesk\bin\`
- Delete: `WinDesk\obj\`
- Delete: `Tests\WinDesk.Tests\bin\`
- Delete: `Tests\WinDesk.Tests\obj\`

**Interfaces:**
- Consumes: existing build output directories
- Produces: clean working tree with no `bin/` or `obj/` folders

- [ ] **Step 1: Remove directories**

```powershell
Remove-Item -Recurse -Force "D:\proj\WinDesk\bin"
Remove-Item -Recurse -Force "D:\proj\WinDesk\obj"
Remove-Item -Recurse -Force "D:\proj\WinDesk\Tests\WinDesk.Tests\bin"
Remove-Item -Recurse -Force "D:\proj\WinDesk\Tests\WinDesk.Tests\obj"
```

- [ ] **Step 2: Verify removal**

Run:
```powershell
Get-ChildItem -Recurse -Directory -Path "D:\proj\WinDesk" | Where-Object { $_.Name -in @('bin','obj') }
```

Expected: no output.

- [ ] **Step 3: Commit (optional)**

No tracked files change; cleanup is workspace-only. Skip commit.

---

### Task 3: Publish and verify

**Files:**
- Produces: `D:\proj\WinDesk\publish\WinDesk.exe`

**Interfaces:**
- Consumes: updated `WinDesk.csproj`
- Produces: published single-file exe

- [ ] **Step 1: Publish Release build**

Run:
```bash
dotnet publish "D:\proj\WinDesk\WinDesk.csproj" -c Release -r win-x64
```

Expected: publish succeeds with no errors.

- [ ] **Step 2: Verify exe location**

Run:
```powershell
Test-Path "D:\proj\WinDesk\publish\WinDesk.exe"
```

Expected: `True`

- [ ] **Step 3: Verify no old deep publish directory**

Run:
```powershell
Test-Path "D:\proj\WinDesk\bin\Release\net8.0-windows\win-x64\publish\WinDesk.exe"
```

Expected: `False`

- [ ] **Step 4: Commit publish artifacts (optional)**

Publish output is typically not committed. Add `publish/` to `.gitignore` if not already present.
