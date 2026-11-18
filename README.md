# DropCut (أداة القص ومعالجة الصور المتقدمة بالسحب والإفلات)

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![UI](https://img.shields.io/badge/WinUI-3%20Windows%20App%20SDK-0078D4)](https://github.com/microsoft/microsoft-ui-xaml)
[![Architecture](https://img.shields.io/badge/Architecture-Modular%20Vertical--Slice-2ea44f)](#architecture)
[![License](https://img.shields.io/badge/License-Proprietary-red)](#)

> **DropCut** is an enterprise-grade, high-performance native Windows 10/11 desktop application engineered with a **Modular Vertical-Slice Architecture** (inspired by Django self-contained apps and enterprise ERP systems). 
> It modernizes and elevates the Windows Snipping Tool into a professional media capture, continuous auto-saving, and instant drag-and-drop sharing suite.

---

## Key Features (أهم الميزات)

1. **Advanced Screen Snapping Engine (محرك القص المتقدم)**:
   - High-DPI multi-monitor translucent capture overlay with precision crosshair cursor and pixel coordinates.
   - 4 Capture Modes: **Rectangular**, **Window Selection**, **Full-screen**, and **Freeform**.
   - Configurable delay timers (3s, 5s, 10s) with countdown animations.

2. **Hardware-Accelerated Annotation Suite (لوحة الرسم والتعديل)**:
   - 60fps WinUI 3 / Win2D Canvas rendering.
   - Pen with smooth ink smoothing, customizable thickness, and full color palette.
   - Translucent highlighter (alpha-blended) for highlighting text and documents.
   - Geometric shapes: Rectangle, Ellipse, Pointer Arrow, and Straight Line.
   - High-DPI Text tool with font sizing and color selection.
   - Non-destructive Crop tool with aspect ratio presets (1:1, 16:9, Free).
   - **Privacy Blur / Redact Tool**: Seamlessly pixelate or blur sensitive credentials, passwords, or credit card numbers.

3. **Continuous Auto-Save on Edit (الحفظ التلقائي المستمر)**:
   - Eliminates the need to ever press `Ctrl+S`.
   - Every snip is instantly assigned a draft in the secure `FileVault`.
   - Every annotation stroke or crop commits to disk automatically via debounced (300ms) background workers.
   - Full Undo / Redo history preserved in SQLite database.

4. **Interactive Outward Drag-and-Drop Sharing (السحب والإفلات الخارجي الذكي)**:
   - **Tab Dragging**: Drag directly from the snippet tab header.
   - **In-Canvas Hand Cursor (`DragHand`)**: Hovering over the image preview transforms the mouse into an interactive hand icon.
   - Clicking and dragging immediately initiates an OLE Drag-and-Drop operation.
   - Drop the image directly into **WhatsApp Web**, **Discord**, **Linear**, **GitHub**, **Gmail**, **Outlook**, **Slack**, or **File Explorer**.

5. **Pure 10-Language i18n Architecture (دعم اللغات العالمي)**:
   - Arabic default with natural RTL alignment and modern typography.
   - English, Spanish, Hindi, Chinese, French, German, Russian, Japanese, Portuguese.
   - Instant dynamic switching without application restart.
   - Zero compound labels (`Cut / قص` strictly forbidden).

6. **Enterprise Governance & Zero-Crash Mandate (الدستور البرمجي)**:
   - Strict One-Class-Per-File law.
   - Complete global exception boundaries; process never abruptly exits.
   - Safe atomic file operations (`.tmp` staging + atomic replace; non-locking `FileShare.ReadWrite`).
   - Dual tracking in **Linear.app** and `docs/TASKS.md`.

---

## Solution Architecture

```text
DropCut/
├── src/
│   ├── DropCut.App/                    # WinUI 3 Shell, Entry Point & DI Composition Root
│   ├── DropCut.Modules/                # ERP/Django-Style Self-Contained Slices
│   │   ├── Workspace/                  # Snapping engine, canvas annotations, drag-drop
│   │   ├── FileVault/                  # Continuous auto-save, recent captures, export
│   │   ├── Permissions/                # Role-based feature policies and locks
│   │   ├── AuditLog/                   # Action history, undo/redo states, database logging
│   │   └── Settings/                   # Pure i18n localization, themes, hotkeys
│   ├── DropCut.Core/                   # Cross-Module Kernel & Contracts
│   └── DropCut.Infrastructure/         # SQLite persistence & Windows OS Adapters
├── tests/                              # Unit, integration, and UI test suites
└── docs/                               # Authoritative system documentation
```

---

## Getting Started & Building

### Prerequisites:
- Windows 10 (version 1809+) or Windows 11.
- .NET 10.0 LTS SDK (pinned via `global.json`).
- Visual Studio 2022 / VS Code with C# Dev Kit.

### Build:
```bash
dotnet build DropCut.sln
```

### Run Tests:
```bash
dotnet test
```

---

## Documentation & Governance

- [docs/PROJECT_RULES.md](docs/PROJECT_RULES.md) — The authoritative Constitution and coding laws.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — Technical blueprint and layer boundaries.
- [docs/MODULE_CATALOG.md](docs/MODULE_CATALOG.md) — Catalog of all modules and their vertical anatomy.
- [docs/DATABASE_SCHEMA.md](docs/DATABASE_SCHEMA.md) — SQLite schema and entity design.
- [docs/TASKS.md](docs/TASKS.md) — Phased implementation roadmap and task tracker.
- [AGENTS.md](AGENTS.md) — Operational instructions for AI agents.
