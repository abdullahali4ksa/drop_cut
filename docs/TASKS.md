# DropCut — Tasks & Implementation Roadmap

> **Rule:** Every task in this file is synchronized with **Linear.app** (Team `JMD`, Project `DropCut — Windows App`). No code may be written without an active task in `READY` status. Every task strictly adheres to `docs/PROJECT_RULES.md`.

---

## Phase 0: Project Governance, Constitution & Scaffolding (المرحلة الحالية)

### [P00-T001] Solution Hierarchy, Governance Constitution & Documentation (Linear: `JMD-41`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Architecture, Modular Vertical-Slice, Clean UI, Microsoft Store Standards)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Zero-Crash Mandate, Protected Async Void, Safe Try/Catch, Non-Locking I/O)`
- **DESCRIPTION:** Establish the complete project skeleton, master solution `DropCut.sln`, empty projects for all layers, and write authoritative documentation:
  - `docs/PROJECT_RULES.md`
  - `docs/ARCHITECTURE.md`
  - `docs/MODULE_CATALOG.md`
  - `docs/DATABASE_SCHEMA.md`
  - `docs/TASKS.md`
  - `AGENTS.md`
  - `README.md`
- **VERIFICATION:** `dotnet build DropCut.sln` succeeds with 0 errors.

---

## Phase 1: Core Kernel, Event Bus & Domain Contracts (`DropCut.Core`)

### [P01-T001] Domain Base Types, Result Pattern & Value Objects (Linear: `JMD-42`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (One-Class-Per-File, Pure C# Kernel)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Guard Clauses, Safe Nullability)`
- **DESCRIPTION:** Implement base types in `DropCut.Core/Domain/`: `Entity.cs`, `Result.cs`, `ResultOfT.cs`, `Guard.cs`, `ValueObject.cs`, `CaptureMode.cs`, `ExportFormat.cs`, `ToolType.cs`.
- **VERIFICATION:** `dotnet test tests/DropCut.Core.Tests` passes (10 tests).

### [P01-T002] In-Process Strongly-Typed Domain Event Bus (Linear: `JMD-42`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Compile-Time Propagation, Event-Driven Decoupling)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Safe Try/Catch in Handler Execution)`
- **DESCRIPTION:** Implement `IEventBus.cs`, `IDomainEvent.cs`, `InMemoryEventBus.cs`, and core domain events:
  - `SnippetCapturedEvent.cs`
  - `ImageAnnotatedEvent.cs`
  - `FileAutoSavedEvent.cs`
  - `LanguageChangedEvent.cs`
- **VERIFICATION:** Unit tests verify event publishing and multiple subscriber invocations with crash isolation.

### [P01-T003] Security Contracts & AI Abstraction Layer (Linear: `JMD-42`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (AI Independence, Security Guard Contracts)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Strict Policy Evaluation)`
- **DESCRIPTION:** Define `IPermissionService.cs`, `ISecurityPolicy.cs`, `SecurityClaim.cs`, `SecurityPolicyResult.cs`, `IAICommandHandler.cs`, `IAIProvider.cs`, `AICommandRegistry.cs`, and `AISecurityPolicy.cs`.
- **VERIFICATION:** Compiles with zero warnings; security unit tests pass.

---

## Phase 2: Infrastructure & Persistence (`DropCut.Infrastructure`)

### [P02-T001] SQLite Database Context & EF Core Repositories (Linear: `JMD-43`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Separation of Concerns, SQLite Persistence)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (WAL Mode, Connection Fault Tolerance)`
- **DESCRIPTION:** Implement `DropCutDbContext.cs`, entity configurations (`SnippetEntity`, `AnnotationEntity`, `AuditLogEntity`, `PermissionEntity`, `UserPreferenceEntity`), and repository implementations (`SnippetRepository`, `AuditLogRepository`, `UserPreferenceRepository`).
- **VERIFICATION:** `dotnet test tests/DropCut.Modules.Tests` passes with in-memory SQLite integration tests.

### [P02-T002] Atomic Safe File System Adapter (Linear: `JMD-43`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (File Storage Isolation)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Non-Locking FileShare.ReadWrite, Staging .tmp Atomic Replacement)`
- **DESCRIPTION:** Implement `SafeFileStorageService.cs` ensuring file integrity against sudden power loss and concurrent non-locking access.
- **VERIFICATION:** File write / read / delete unit tests pass.

---

## Phase 3: ERP/Django-Style Feature Modules (`DropCut.Modules`)

### [P03-T001] FileVault Module & Continuous Auto-Save Engine (Linear: `JMD-44`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Modular Vertical-Slice, Continuous Auto-Save)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Debounced Async Worker, Non-Blocking Disk Write)`
- **DESCRIPTION:** Build self-contained `FileVault` module with debounced (300ms) continuous auto-save on image modification, recents gallery, and multi-format exporters.
- **VERIFICATION:** Auto-save unit tests pass (`AutoSaveServiceTests`); modifies and commits files without Ctrl+S.

### [P03-T002] Workspace Module — Screen Snapping Engine (Linear: `JMD-45`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (High-DPI Multi-Monitor Snapping)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Protected Win32 Hooks, Memory Cleanup)`
- **DESCRIPTION:** Build transparent overlay window, crosshair cursor, 4 capture modes (Rectangular, Window, Fullscreen, Freeform), and countdown timer delay (3s, 5s, 10s).
- **VERIFICATION:** `SnappingEngineTests` pass; publishes `SnippetCapturedEvent`.

### [P03-T003] Workspace Module — Annotation Canvas, Redaction & Crop (Linear: `JMD-46`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (WinUI 3 Canvas & Win2D Hardware Acceleration)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Debounced Canvas Strokes, Protected UI Event Handlers)`
- **DESCRIPTION:** Implement high-performance drawing canvas with Pen, Highlighter, Shapes, Text tool, Crop tool, and Privacy Blur / Redaction tool.
- **VERIFICATION:** `AnnotationCanvasServiceTests` pass (stroke addition, undo, redo, and crop bounds).

### [P03-T004] Workspace Module — Interactive Outward Drag & Drop Sharing (Linear: `JMD-47`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Outward Drag-and-Drop Sharing Mandate)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Protected DataPackage Construction, Fallback Memory Streams)`
- **DESCRIPTION:** Implement interactive hand cursor (`DragHand`) on image hover and tab drag. Packing `DataPackage` with bitmap and storage file for immediate dropping into WhatsApp Web, Discord, GitHub, Linear, Gmail, Outlook, Slack, and Explorer.
- **VERIFICATION:** `DragDropSharingServiceTests` pass.

### [P03-T005] Permissions & AuditLog Modules (Linear: `JMD-48`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Role-Based Access Control & Undo/Redo Stacks)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Auditing Fail-Safe, Protected Rollbacks)`
- **DESCRIPTION:** Pluggable role-based feature policies (`ModulePermissionService`) and automated event-driven audit log trail (`AuditLogService`).
- **VERIFICATION:** `ModulePermissionServiceTests` and `AuditLogServiceTests` pass.

### [P03-T006] Settings Module & 10-Language Pure i18n Engine (Linear: `JMD-49`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Pure i18n Architecture, Top 10 Languages, Dynamic Switching)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Null-Safe Localization Keys, Default Fallback)`
- **DESCRIPTION:** Implement 10-language dynamic localization engine (Arabic default RTL, English, Spanish, etc.), Mica Alt / Dark theme manager, and global hotkey preferences.
- **VERIFICATION:** Language switches dynamically without app restart and with zero compound labels.

---

## Phase 4: WinUI 3 Shell & Release Packaging (`DropCut.App`)

### [P04-T001] MainWindow Shell, Tab Context Menu & MSIX Packaging (Linear: `JMD-50`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Fluent Design, Tab Lifecycle, Store Standards)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Global Unhandled Exception Boundaries, Multi-Arch x64/arm64)`
- **DESCRIPTION:** Build root `MainWindow` with `TabView`, right-click / touch long-press context menu (Pin, Favorites, Close), bilingual tooltips, global exception logging, and MSIX packaging for x64 and arm64.
- **VERIFICATION:** App launches smoothly, tab context menu operates as specified, and build passes Store certification checks.

### [P04-T002] Opaque GDI Pixel Pipeline & Elimination of Unwanted Transparency (Linear: `JMD-51`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (High-DPI Multi-Monitor Snapping, GDI 32-bit DIB Architecture)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Pixel Buffer Integrity, Alpha Channel Enforcement)`
- **DESCRIPTION:** Resolve the screenshot transparency defect by explicitly setting the GDI Alpha channel byte (Alpha = 255 / 0xFF) for all desktop captures and cropped pixel buffers, ensuring screenshots are 100% solid, opaque, vibrant, and identical to the source screen.
- **VERIFICATION:** Screen captures render with 100% opacity in WinUI 3; background windows do not show through captured images.

### [P04-T003] Compact Drag Preview Thumbnail & Floating Drag UI (Linear: `JMD-52`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Outward Drag-and-Drop Sharing Mandate, Clean UI)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (WIC Hardware Downsampling, Protected DragUI Configuration)`
- **DESCRIPTION:** Replace the full-screen 1920x1080 drag preview ghost with a compact, centered 140x140 square thumbnail card using `DragUI.SetContentFromBitmapImage` with WIC hardware-accelerated decode scaling. Maintain the signature label "اسحبها وشاركها إلينا (WhatsApp / Telegram / Explorer)" without obscuring the desktop during dragging.
- **VERIFICATION:** Dragging any image, tab, or share pill displays a neat 140x140 square preview centered under the cursor, and dropping into external apps immediately delivers the full-resolution PNG file.

### [P04-T004] Laughing Scissors (مقص ضاحك) Brand Identity & Multi-Resolution Windows Icons (Linear: `JMD-53`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Brand Identity, Microsoft Store Standards, Fluent Visual Polish)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Resource Embedding, Multi-Resolution Windows Asset Scaling)`
- **DESCRIPTION:** Design a joyful, modern "Laughing Scissors" (مقص ضاحك) mascot identity for DropCut with cheerful arc eyes, open smile, polished chrome blades, electric cyan and creative orange handles, and sparkles. Produce a high-resolution SVG master (`app_icon_laughing_scissors.svg`) and a multi-resolution Windows ICO (`app.ico` with 256, 128, 64, 48, 32, 16 px) embedded into the executable and window title bar.
- **VERIFICATION:** Window titlebar, taskbar, Alt+Tab, and `DropCut.App.exe` display the new Laughing Scissors mascot icon.

### [P04-T005] End-to-End Application Quality Assurance & Automated Verification Suite (Linear: `JMD-54`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Quality Assurance, Anti-Regression Mandate, Zero-Crash Protocol)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Full Unit & Integration Test Coverage)`
- **DESCRIPTION:** Comprehensive review and testing of all modules: multi-monitor DPI scaling, keyboard shortcuts (PrintScreen, Ctrl+B+B, Ctrl+N, Ctrl+S, Ctrl+C, Ctrl+W, Ctrl+Z), in-tab crop accuracy, tab drag-and-drop to third party apps, memory leak prevention, and zero-crash fault tolerance.
- **VERIFICATION:** 100% of test suites pass with 0 errors and 0 warnings (52/52 tests passing).

---

## Phase 5: Advanced UX, Navigation Tools & Offline Store Packaging

### [P05-T001] Interactive Zoom Controls (+ / - / 100%) & Keyboard Shortcuts (Linear: `JMD-55`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Clean UI, High-DPI Multi-Monitor Standards)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Bounded Scale Factor Math, Clamp Zoom Between 10% and 500%)`
- **DESCRIPTION:** Implement dedicated Zoom In (`+`), Zoom Out (`-`), and Reset (`100%` / Actual Size) controls in the bottom status bar and toolbar. Wire keyboard shortcuts `Ctrl + Plus` (`Ctrl + =`), `Ctrl + Minus` (`Ctrl + -`), and `Ctrl + 0` directly to `ScrollViewer.ChangeView` with smooth animated scaling, mouse-wheel zooming (`Ctrl + Wheel`), and dynamic zoom percentage binding on the active tab.
- **VERIFICATION:** Pressing `Ctrl + +` zooms in, `Ctrl + -` zooms out, `Ctrl + 0` resets to 100%, and status bar reflects the exact zoom level.

### [P05-T002] Dedicated Hand / Pan Tool (علامة اليد) for Mode Separation (Linear: `JMD-56`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Intuitive Ergonomics, Mode Separation Law)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Pointer Routing Isolation, Stroke Collision Prevention)`
- **DESCRIPTION:** Introduce a dedicated Hand tool button (`Glyph="&#xE768;"` Pan/Hand) positioned directly beside the Pen and Highlighter tools in the ribbon to explicitly distinguish between Drawing/Annotation mode vs. Navigation/Dragging mode:
  - **Drawing Mode (Pen / Highlighter):** Pointer creates ink strokes; canvas is active for annotation.
  - **Hand Mode (علامة اليد):** Cursor transforms to `Hand`; canvas drawing is locked; left-click drags and pans the zoomed image; dragging initiates the outward sharing package without accidental ink smudges.
  - Shortcut `H` or holding `Spacebar` activates temporary Hand mode.
- **VERIFICATION:** Switching to Hand mode disables drawing and enables smooth panning and drag-to-share.

### [P05-T003] Multi-Language (10 Languages) & Fluent Theme Settings Engine (Linear: `JMD-57`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md §7 (Pure i18n Architecture, Top 10 Languages, Zero Compound Labels)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Dynamic RTL/LTR FlowDirection Re-layout, Null-Safe Resource Dictionaries)`
- **DESCRIPTION:** Build a comprehensive Settings dialog / modal page:
  - **Languages (10 Languages):** Arabic (default RTL), English, Spanish, French, German, Japanese, Chinese, Turkish, Russian, Hindi.
  - **Dynamic Hot-Switching:** Selection publishes `LanguageChangedEvent`, instantly flipping layout direction (`FlowDirection.RightToLeft` vs `LeftToRight`) and updating all ribbon labels, tab titles, status text, and tooltips with **zero compound labels** (`"إعدادات"` or `"Settings"`).
  - **Themes:** Dark Mode, Light Mode, and Windows 11 System Default with Mica Alt backdrops.
  - **Strict Tooltips Localization:** All icon tooltips dynamically reflect the active language without any English/Arabic mixed slashes.
- **VERIFICATION:** Switching language updates all labels and tooltips in real-time without application restart.

### [P05-T004] 100% Offline Standalone Executable & Microsoft Store Packaging (Linear: `JMD-58`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Zero External Network Dependencies, Air-Gapped Operation)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Self-Contained Binaries, Local SQLite Database Isolation)`
- **DESCRIPTION:** Ensure DropCut runs 100% offline with zero network calls, zero telemetry, and zero online licensing requirements:
  - Configure `DropCut.App.csproj` publish profile for `SelfContained=true` with single-file executable options, bundling the .NET 10 runtime and Windows App SDK binaries.
  - Generate a verified offline `.msixbundle` package for Microsoft Store release and direct sideloading on air-gapped enterprise machines.
- **VERIFICATION:** Application installs and runs on a disconnected Windows machine without internet access or pre-installed .NET runtimes.

---

## Phase 6: UX Enhancements & Strategic Streamlining

### [P06-T001] Privacy Blur & Redaction Tool (Linear: `JMD-59`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (User Privacy Mandate, Store Security Standards)`
- **DESCRIPTION:** Add a 1-click Gaussian blur / pixelation box tool in the annotation ribbon to quickly obscure passwords, credit cards, and personal data before sharing.
- **VERIFICATION:** Blurred region is permanently rasterized into the bitmap before export or drag & drop.

### [P06-T002] Countdown Delay Snip Timer (3s / 5s / 10s) (Linear: `JMD-60`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Windows 11 Snipping Tool Parity)`
- **DESCRIPTION:** Add a delay dropdown timer (No delay, 3 seconds, 5 seconds, 10 seconds) allowing users to open dropdown menus, tooltips, or context menus before the desktop freezes for capture.
- **VERIFICATION:** Timer shows countdown animation, freezes desktop at T-0, and opens sniper overlay.

### [P06-T003] Deprecate In-App Video Recording in Favor of Pure Lightweight Snapping (Linear: `JMD-61`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Lean Architecture, Zero Bloat Mandate)`
- **DESCRIPTION:** Remove obsolete video recording placeholders and media player icons from DropCut to keep the executable lightweight (<30MB RAM footprint) and focused on lightning-fast image snip & drag sharing.

---

## Phase 7: Ribbon Pure Icons Law, Gray Theme & Microsoft Store GitHub Release

### [P07-T001] Ribbon Pure Icons Law & Slate Gray Theme (Linear: `JMD-62`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Pure Icons Law, Clean Fluent Ribbon, Store UI Standards)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Dynamic Theme Switching, Slate Gray #2B2D30)`
- **DESCRIPTION:** Converted all top ribbon toolbar buttons to pure Fluent icons (no hardcoded Arabic/English text labels), hooked all tooltips to the 10-language dynamic localization engine, and added the dedicated Gray Theme (`#2B2D30`) alongside Dark, Light, and System themes.
- **VERIFICATION:** Ribbon buttons display clean Fluent glyphs with dynamic localized tooltips across all 10 languages; Gray theme applies smoothly.

### [P07-T002] 100% Offline Privacy Policy & Microsoft Store Certification Docs (Linear: `JMD-63`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Microsoft Store Standards, Offline-First Architecture)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Zero Telemetry, Local Storage Only)`
- **DESCRIPTION:** Authored authoritative `PRIVACY_POLICY.md` and `docs/PRIVACY_POLICY.md` certifying 100% offline data handling, zero cloud transmission, local `%USERPROFILE%\Pictures\DropCut` vault, and compliance with Microsoft Store Certification Requirements (Section 10.5, 10.8, 10.1).
- **VERIFICATION:** Privacy policy validated for Store submission and published in repository root and docs.

### [P07-T003] GitHub Repository Setup & Push to @abdullahali4ksa (Linear: `JMD-64`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Source Control Governance, Store Release Pipeline)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Protected Branch Policies, Clean .gitignore)`
- **DESCRIPTION:** Create public GitHub repository `drop_cut` under authenticated account `abdullahali4ksa`, configure origin remote, commit all clean verified code, and push the main branch.
- **VERIFICATION:** Remote repository is accessible on GitHub at https://github.com/abdullahali4ksa/drop_cut with full commit history, tests, and clean build.

---

## Phase 8: Version 2.0 Feature Roadmap (خارطة طريق الإصدار الثاني v2.0)

> **Mandate:** All tasks in Phase 8 are scheduled for Version 2.0 implementation upon explicit user authorization. No implementation code may be introduced until requested.  
> **Scope Rule:** Only the approved tasks below are part of Version 2.0. All other exploratory suggestions (macOS/Linux cross-platform, Chrome extension, scrolling capture, etc.) are strictly deferred until explicitly requested.

### [P08-T001] Smart Auto-Fit Viewport Scaling (احتواء ذكي للصورة داخل أبعاد التاب) (Linear: `JMD-65`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Ergonomic UI Standards, High-DPI Viewport Math)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Bounded Zoom Clamping, Safe Viewport Aspect Ratio Calculation)`
- **DESCRIPTION:** Automatically calculate the aspect ratio of captured images relative to the active tab viewport (`ScrollViewer.ViewportWidth / ImageWidth`, `ScrollViewer.ViewportHeight / ImageHeight`) with comfortable padding (24px). When a large screenshot (e.g., 1080p, 2K, 4K) is taken, automatically scale it down to "Fit to Window" so the entire capture is immediately visible without appearing huge or requiring immediate scrolling, while updating the status zoom level (e.g. `65%`) and allowing 1-click toggling back to 100% (Actual Size via `Ctrl + 0`).
- **VERIFICATION:** Capturing a fullscreen 1080p or 4K screenshot fits cleanly inside the active tab viewport without exceeding borders; status bar reflects exact fit zoom percentage, and `Ctrl + 0` resets to `100%`.

### [P08-T002] Native Windows Print Pipeline & Print Hotkey (Ctrl+P) (نظام الطباعة المتكامل واختصار Ctrl+P) (Linear: `JMD-66`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Windows Integration, Fluent Native Tools)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Protected PrintDocument Lifecycle, Memory Stream Disposal)`
- **DESCRIPTION:** Implement direct printing capabilities via `Windows.Graphics.Printing` / `PrintManagerInterop` / `PrintDocument`:
  - Add dedicated Print icon button in the ribbon and 3-dots dropdown menu with dynamic localized tooltip (`Print / طباعة`).
  - Wire standard global shortcut `Ctrl + P` to trigger the Windows native print dialog.
  - Support full printer settings: paper size (A4, Letter), orientation (Portrait, Landscape), margins, fit-to-page scaling, and direct export to physical printers or Microsoft Print to PDF.
- **VERIFICATION:** Pressing `Ctrl + P` or clicking Print opens the native Windows print preview dialog with the active tab image rendered accurately; zero crashes if print spooler is busy or cancelled.

### [P08-T003] Geometric Shapes Annotation Tool & Dropdown Flyout (أداة الأشكال الهندسية والأسهم والشروحات) (Linear: `JMD-67`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Modular Vertical-Slice, Clean Fluent Ribbon, Store UI Standards)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Vector Geometry Math, Bounds Validation, Hit-Testing Isolation)`
- **DESCRIPTION:** Introduce a dedicated Shapes SplitButton (`&#xE73B;` / Shape Tool) in Section 2 of the top ribbon toolbar with an interactive dropdown flyout:
  - **Supported Shapes:**
    1. **Rectangle / Square** (مستطيل ومربع حاد الحواف)
    2. **Rounded Rectangle** (مستطيل بأركان منحنية ناعمة)
    3. **Ellipse / Circle** (دائرة وشكل بيضاوي متناسق)
    4. **Directional Arrow** (سهم توجيهي مشطوف لشروحات الدروس والتوضيحات)
    5. **Line** (خط مستقيم منتظم)
    6. **Callout Speech Bubble** (فقاعة توضيحية لكتابة الملاحظات داخلها)
  - **Customization Options:**
    - Stroke thickness selector (2px, 4px, 8px, 12px)
    - Color palette picker (Red, Blue, Green, Yellow, Black, White, etc.)
    - Fill modes (Transparent outline vs semi-translucent solid fill)
  - **Interaction:**
    - Click and drag on canvas renders live vector shape preview with real-time bounding box.
    - Full Undo / Redo support (`Ctrl + Z`, `Ctrl + Y`).
    - Rasterizes cleanly into export bitmap upon save or drag & drop.
- **VERIFICATION:** Selecting Arrow or Rectangle allows smooth click-and-drag drawing on the screenshot; shapes render with crisp anti-aliased geometry, undoable via `Ctrl + Z`, and cleanly rasterized into the export bitmap.

### [P08-T004] Step Numbering Badges Tool (أداة الترقيم المتسلسل التلقائي للشروحات ①, ②, ③...) (Linear: `JMD-68`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Clean Fluent Ribbon, Educational Annotation Suite)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Stateful Badge Counter, Undo-Stack Synchronization)`
- **DESCRIPTION:** Introduce an auto-incrementing Step Numbering Badge tool (`&#xE735;` / Badge Number Tool) in Section 2 of the ribbon toolbar:
  - Each mouse click on the screenshot stamps an attractive, high-contrast circular badge displaying a sequential step number (`①`, `②`, `③`...).
  - Customizable badge background color (Accent Cyan, Red, Orange, Green, Purple) and diameter (24px, 32px, 40px).
  - Stateful sequential counter that auto-increments on placement and automatically decrements on Undo (`Ctrl + Z`).
  - Option in flyout or double-click to reset counter back to 1.
  - Renders as high-DPI vector graphics, fully undoable, and rasterized into export PNG.
- **VERIFICATION:** Clicking sequentially on 3 areas stamps badges 1, 2, 3; pressing `Ctrl + Z` removes badge 3 and sets next counter to 3; exporting image preserves clean numbers.

### [P08-T005] Dual Architecture Packaging: Native Arm64 & Universal Store Bundle (حزم x64 و Arm64 المزدوجة) (Linear: `JMD-69`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Microsoft Store Standards, Future-Proof Architecture)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Cross-Platform Native Binaries, Multi-Target MSBuild)`
- **DESCRIPTION:** Build native Arm64 target (`-r win-arm64`) alongside x64 (`-r win-x64`) for Snapdragon X Elite and Surface Pro Copilot+ PCs, eliminating emulation overhead and ensuring 100% compliance with Microsoft Store's latest recommendations for Windows on Arm.
- **VERIFICATION:** Both `DropCut_v2.0.0.0_x64.msix` and `DropCut_v2.0.0.0_arm64.msix` generate cleanly and pass Store pre-submission checks.

### [P08-T006] Rectangular Snip High-DPI Scaling & Multi-Tab Isolation (إصلاح إزاحة القص المستطيل وعزل النوافذ والشفافية) (Linear: `JMD-70`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (High-DPI Coordinate Math, Clean UI, Microsoft Store Standards)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Non-Locking Stream I/O, Safe Try/Catch, Pointer Capture Isolation)`
- **DESCRIPTION:** Fix coordinate shifting in rectangular snip mode and eliminate recurring transparency/dimmer defects across multi-tab captures:
  1. Translate WinUI 3 DIP pointer drag coordinates to physical GDI pixel coordinates via dynamic aspect scaling (`(double)bmpWidth / RootGrid.ActualWidth`), ensuring 100% pixel-perfect crop boundaries without leftward offset.
  2. Implement 4-way dynamic slab cutout dimmer (`DimmerTop`, `DimmerBottom`, `DimmerLeft`, `DimmerRight`) in `SnipOverlayWindow`, leaving the selected capture region with 0% opacity overlay and 100% crystal-clear brightness.
  3. Increase pre-capture delay to 320ms in `MainWindow` to allow Windows 11 DWM window hide fade animation to complete 100%, preventing semi-transparent ghost captures.
  4. Scope active visual tree queries (`GetActiveScrollViewer()`, `GetActiveDrawingCanvas()`) strictly to `MainTabView.ContainerFromItem(ViewModel.SelectedTab)`.
  5. Reset overlay states (`IsCropping`, `IsBlurring`, `IsDrawingMode`) on each new capture tab.
  6. Eliminate UI thread deadlocks by refactoring bitmap stream conversion to synchronous `AsStreamForWrite()`.
- **VERIFICATION:** Rectangular snip matches selection exactly with zero pixel shift across 100%-200% DPI; multi-tab captures remain solid without transparency leaks; `dotnet build` passes with 0 warnings/0 errors and 53 unit tests pass.

### [P08-T007] Zero-Black Screen Capture Robustness & In-App Policy 10.5 Privacy & Licensing Suite (معالجة الشاشة السوداء وتكامل سياسة الخصوصية والتراخيص) (Linear: `JMD-71`)
- **STATUS:** `DONE`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Microsoft Store Policy 10.5, 100% Offline-First Architecture, Zero-Crash Engineering)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Stream Disposal Isolation, GDI DC Lifecycle, Dual BitBlt Fallback)`
- **DESCRIPTION:** Eliminate black-screen capture issues and integrate the official Microsoft Store Policy 10.5 privacy and licensing suite:
  1. Fix critical WinRT stream premature disposal in `BytesToBitmapImageConverter` and `SnipOverlayWindow` where `using (netStream)` closed the underlying `InMemoryRandomAccessStream`, causing `BitmapImage.SetSource()` to fail and render pitch-black canvases.
  2. Implement dual-tier `BitBlt` capture fallback: attempt `SRCCOPY | CAPTUREBLT` first, and if denied or unsupported by display drivers, seamlessly fallback to standard `SRCCOPY`.
  3. Ensure GDI bitmap is properly deselected (`SelectObject(memDc, oldObj)`) before invoking `GetDIBits()`, and use positive height for universal driver compatibility.
  4. Change `SnipOverlayWindow.RootGrid` background from `Black` to `#01000000` to guarantee live desktop visibility through the overlay while maintaining 100% pointer hit-testing.
  5. Integrate interactive in-app Privacy Policy & Licensing card in the Settings dialog:
     - 100% Offline-First Commitment (Zero external servers, zero telemetry, local SQLite storage).
     - Single-User Perpetual Educational License (EULA) for up to 10 devices.
     - Open-Source MIT License terms.
     - 1-Click button to launch the official public `PRIVACY_POLICY.md` on GitHub.
  6. Localize privacy and licensing strings across all 10 supported languages in `LocalizationService`.
  7. Formally enshrine Policy 10.5 and the licensing models into `docs/MICROSOFT_STORE_POLICY_CONSTITUTION.md`.
- **VERIFICATION:** `dotnet build` passes with 0 warnings and 0 errors; all 53 unit tests pass; dual x64 and arm64 MSIX packages built and verified with all 5 mandatory Store assets.
