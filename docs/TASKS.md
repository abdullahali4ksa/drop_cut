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
- **STATUS:** `BACKLOG`
- **POLICY_REF:** `docs/PROJECT_RULES.md (Fluent Design, Tab Lifecycle, Store Standards)`
- **SECURITY_REF:** `docs/PROJECT_RULES.md §Zero-Crash (Global Unhandled Exception Boundaries, Multi-Arch x64/arm64)`
- **DESCRIPTION:** Build root `MainWindow` with `TabView`, right-click / touch long-press context menu (Pin, Favorites, Close), bilingual tooltips, global exception logging, and MSIX packaging for x64 and arm64.
- **VERIFICATION:** App launches smoothly, tab context menu operates as specified, and build passes Store certification checks.
