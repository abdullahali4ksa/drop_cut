# DropCut — Constitution & Development Rules (الدستور البرمجي والتطويري)

> **Purpose:** This document serves as the supreme, non-negotiable constitution for every human developer and AI agent working on the **DropCut** project. These rules exist to enforce architectural purity, prevent technical debt, ensure zero crashes, and guarantee enterprise-grade maintainability matching the philosophies of **Django Apps** and modern **ERP systems**.

---

## 1. Modular Vertical-Slice Architecture Law (قانون الشرائح الرأسية المستقلة)

1. **Django-Style Feature Autonomy**:
   - Every business capability MUST reside entirely within its own self-contained directory under `src/DropCut.Modules/[ModuleName]/` (e.g., `Workspace/`, `FileVault/`, `Permissions/`, `AuditLog/`, `Settings/`).
   - A module encapsulates its complete vertical lifecycle:
     - `Views/`: XAML pages and UserControls (`*Page.xaml`, `*View.xaml`).
     - `ViewModels/`: MVVM presentation logic (`*ViewModel.cs`).
     - `Models/`: Module domain models, entities, and DTOs (`*Model.cs`, `*Dto.cs`).
     - `Contracts/`: Interfaces specific to the module (`I*Service.cs`, `I*Repository.cs`).
     - `Services/`: Concrete business implementations (`*Service.cs`).
     - `Data/`: Persistence mappings and repositories (`*Repository.cs`).
     - `Helpers/`: Module-specific utilities and validators (`*Helper.cs`).
     - `[ModuleName]Module.cs`: Module dependency injection registration and composition entry point.

2. **Strict Encapsulation & Compile-Time Propagation**:
   - Direct cross-module internal type references are **STRICTLY FORBIDDEN**. Module A must NEVER reference internal classes of Module B.
   - All inter-module communication, contracts, and shared domain models MUST flow exclusively through `DropCut.Core/Contracts/` or strongly-typed domain events via `DropCut.Core/Events/`.
   - Modifying a core contract immediately forces all dependent modules to adapt at compile time, eliminating silent runtime bugs.

---

## 2. Strict One-Class-Per-File Law (قاعدة ملف مستقل لكل كلاس)

1. Every `class`, `interface`, `record`, `struct`, or `enum` MUST live in its own dedicated `.cs` file matching its exact type name.
   - Example: `ISnippetAutoSaveService.cs` contains only `public interface ISnippetAutoSaveService`.
   - Example: `CaptureMode.cs` contains only `public enum CaptureMode`.
2. Grouping multiple classes, helper types, or inner classes into a single file is **STRICTLY PROHIBITED**.
3. File naming must match type casing exactly.

---

## 3. Strict Separation of Concerns (فصل الطبقات)

1. **Views (`.xaml.cs`)**:
   - Zero business logic, zero calculations, zero database queries, and zero direct file system I/O.
   - Code-behind is strictly limited to UI initialization (`InitializeComponent()`) and view-only interactions (such as Canvas pointer capture or visual state animations).
2. **ViewModels**:
   - Presentation state and user commands only.
   - Interact solely with services via interface contracts (`Contracts/`), never directly with the database context or raw storage APIs.
3. **Data / Repositories**:
   - Handle all persistence operations behind interfaces.

---

## 4. Zero-Crash Architecture & Anti-Hang Mandate (ميثاق منع الانهيار والتهنيج)

To guarantee 100% stability, zero process exits, and silky-smooth 60fps responsiveness under all workloads:

### 4.1 Global Exception Boundaries
- `App.UnhandledException`, `TaskScheduler.UnobservedTaskException`, and `AppDomain.CurrentDomain.UnhandledException` must always be trapped, marked handled where possible (`e.Handled = true`), and logged with full stack traces to `crash.log`.
- Recoverable errors must display an in-app error InfoBar banner rather than crashing the process.

### 4.2 Strict Ban on Raw `async void`
- **Rule:** Never author `async void` methods. Always use `async Task`.
- **UI Event Handler Exception:** WinUI 3 event handlers (`Click`, `PointerPressed`, `Drop`) that require `void` return MUST enclose their entire body in a comprehensive `try { ... } catch (Exception ex) { ... }` block. In .NET, an unhandled exception escaping an `async void` method bypasses the call stack and immediately terminates the application.

### 4.3 Anti-Hang & Responsiveness Standards
- **Debounced Operations**: All frequent operations (user keystrokes, canvas ink strokes, crop bounds updates) must be debounced (e.g., 300ms) before committing to disk.
- **ReDoS Protection**: Any regular expression evaluation must specify an explicit timeout (`TimeSpan.FromSeconds(1)`) and catch `RegexMatchTimeoutException`.
- **Non-Blocking Asynchronous I/O**: File reads, writes, and image encodings must never block the UI thread. Use `Task.Run` or native async I/O.

### 4.4 Non-Locking & Safe Atomic File Operations
- **Non-Locking Reads**: Files must always be opened with `FileShare.ReadWrite` so concurrent tools or external viewers do not fail.
- **Atomic Writes**: Image files and metadata must be saved via temporary staging files (`.tmp`) followed by an atomic rename/replace. If power is interrupted mid-write, original data is never corrupted.

---

## 5. Mandatory Dual-Line Reference Policy (إلزامية سطر سياسة التطوير وسطر حماية الكود)

Every single task description, Linear ticket, agent prompt, and code change instruction MUST explicitly include two mandatory reference headers:

```text
POLICY_REF: docs/PROJECT_RULES.md (Architecture, Modular Vertical-Slice, Clean UI, Microsoft Store Standards)
SECURITY_REF: docs/PROJECT_RULES.md §Zero-Crash (Zero-Crash Mandate, Protected Async Void, Safe Try/Catch, Non-Locking I/O)
```

Any proposed feature or PR lacking these two lines must be rejected immediately.

---

## 6. Continuous Auto-Save on Edit Mandate (الحفظ التلقائي المستمر)

1. DropCut eliminates the friction of manual saving.
2. Every screen capture taken is instantly assigned a secure draft record in `FileVault`.
3. Any annotation, drawing stroke, crop, text addition, or redaction blur triggers a debounced (300ms) automatic background save.
4. Non-destructive undo/redo history is preserved in `AuditLog` so users can always revert changes without risk of data loss.

---

## 7. Outward Drag-and-Drop Sharing Mandate (السحب والإفلات الخارجي للصور)

1. **Tab-Header Dragging**:
   - Dragging a snippet tab header initiates an OLE Drag-and-Drop operation via `DataPackage`.
2. **In-Canvas Interactive Hand Cursor (`DragHand`)**:
   - Hovering over an image preview transforms the mouse cursor into an intuitive hand icon (`CoreCursorType.Hand`).
   - Clicking and dragging directly from the image packages the rendered bitmap (`StandardDataFormats.Bitmap`) and the local file path (`StorageItems`).
   - The user can release the mouse over ANY external application (WhatsApp Web, Discord, Telegram, GitHub issues, Linear cards, Gmail, Outlook, Slack, or File Explorer) for instant dropping.

---

## 8. Pure Multi-Language (i18n) Architecture (دعم اللغات العالمي والتبديل اللحظي)

1. **Strict Prohibition of Compound Labels**:
   - UI elements must NEVER combine multiple languages on the same label (e.g., `Cut / قص` or `Save / حفظ` is strictly forbidden).
   - The interface must render exclusively and cleanly in the single currently active language.
2. **Top 10 Global Languages Supported**:
   - **العربية (Arabic - `ar`)** [Default language, natural RTL alignment and typography].
   - **English (`en`)**
   - **Español (`es`)**
   - **हिन्दी (`hi`)**
   - **中文 (`zh-Hans`)**
   - **Français (`fr`)**
   - **Deutsch (`de`)**
   - **Русский (`ru`)**
   - **日本語 (`ja`)**
   - **Português (`pt`)**
3. **Dynamic Seamless Language Switching**:
   - Switching language in Settings applies instantaneously across all open windows, menus, toolbars, and tooltips without requiring an app restart.
4. **Persistent Preference**:
   - Selected language is saved in `preferences.json` and loaded before the initial window renders to prevent UI flicker.

---

## 9. Modern Iconography, Bilingual Tooltips & Touch Accessibility

1. **Fluent Iconography**:
   - No clunky ASCII symbols or esoteric glyphs. All actions utilize polished Segoe Fluent Icons or modern vector SVGs.
2. **Mandatory Tooltips on Hover & Touch Long-Press**:
   - Every button and tool MUST provide a descriptive `ToolTipService.ToolTip`.
   - Tooltips must include the function name and its keyboard shortcut (e.g. `New Snip (Ctrl+N)` / `قص جديد (Ctrl+N)`).
   - WinUI 3 `ToolTipService` triggers reliably on both mouse hover and touch long-press.

---

## 10. Universal Tab Context Menu & Pinning Lifecycle

1. Right-clicking or long-pressing any snippet tab header must display a Fluent context menu with:
   - `📌 Pin Tab`: Locks tab to prevent accidental closing (`IsClosable=false`).
   - `⭐ Add to Favorites`: Persists snippet into favorites gallery.
   - `❌ Close Tab (Ctrl+W)`: Closes active tab safely.
   - `➡️ Close Tabs to the Right`: Closes all unpinned tabs to the right.
   - `👥 Close Other Tabs`: Closes all open tabs except active and pinned tabs.
   - `🚫 Close All Tabs`: Closes all unpinned tabs after verifying auto-save completion.

---

## 11. Dual Task Registration & Tracking Policy (Linear.app & TASKS.md)

1. **Dual Registration Mandate**:
   - Every single feature, refactor, bugfix, or release step must be registered in both:
     - `docs/TASKS.md` (Local project control layer)
     - **Linear.app** (Team `JMD`, Project `DropCut — Windows App`)
2. **No Ghost Features**:
   - Writing code without a registered task ID in Linear and `docs/TASKS.md` is strictly forbidden.
3. **Commit Traceability**:
   - Commits must reference both task identifiers (e.g., `feat(Workspace): JMD-45 / P03-T002 implement rectangular snip overlay`).

---

## 12. File Creation & Anti-Duplication Policy (منع تكرار الملفات)

1. Never create duplicate or versioned files such as `*_new`, `*_v2`, `*_temp`, `*_fixed`, or duplicate classes.
2. Always modify authoritative files in place.
3. Every file must adhere to the modular vertical-slice taxonomy.

---

## 13. Architecture Authority & STOP Protocol

1. `docs/ARCHITECTURE.md` is the supreme architectural blueprint.
2. Developers and AI agents must NOT add new external NuGet packages, change database engines, alter DI strategies, or modify solution boundaries without explicit human approval.
3. If an architectural modification or new package is required, **STOP immediately** and output:
```text
APPROVAL_REQUIRED:
TYPE: [ARCHITECTURE_CHANGE | DEPENDENCY]
WHY:
FILES_AFFECTED:
PROPOSED_SOLUTION:
```

---

## 14. Universal Microsoft Store Certification & Developer Account Protection Laws
(قوانين الاعتماد وحماية حساب المطور لمتجر مايكروسوفت)

> **Supreme Mandate:** Adherence to `docs/MICROSOFT_STORE_POLICY_CONSTITUTION.md` is mandatory for **DropCut and every software project authored or generated**. Never risk developer account suspension, embarrassment, or store rejection.

1. **Zero-Account-Risk Policy**:
   - Zero telemetry, zero cloud data transfer, 100% offline-first execution, and zero unapproved background activity.
2. **Mandatory 5-Asset Suite Pre-Flight Verification**:
   - No package may be generated or submitted without verifying the physical presence of all 5 required visual assets:
     - `Assets\Square44x44Logo.png` (44×44 px)
     - `Assets\Square150x150Logo.png` (150×150 px)
     - `Assets\Wide310x150Logo.png` (310×150 px)
     - `Assets\SplashScreen.png` (620×300 px)
     - `Assets\StoreLogo.png` (50×50 px)
3. **Mandatory Published Privacy Policy**:
   - Every project must maintain a complete, valid `PRIVACY_POLICY.md` at root and online on GitHub.
4. **Standardized `runFullTrust` Justification**:
   - Whenever `runFullTrust` capability is declared, always provide the approved justification in Store submission notes:
     > *"This is a native WinUI 3 desktop application requiring runFullTrust capability to access Windows screen clipping APIs and global hotkeys."*
5. **Multi-Architecture Native Binaries (x64 + Arm64)**:
   - Release builds must support `win-x64` (Intel/AMD) and `win-arm64` (Snapdragon X Elite / Copilot+ PCs).
6. **Isolated Storage Paths**:
   - Respect MSIX read-only install directories (`C:\Program Files\WindowsApps\...`).
   - Write working files and caches to `%LocalAppData%\[AppName]\` and user exports strictly to user-designated personal folders (`%USERPROFILE%\Pictures\[AppName]`).
7. **Global Polish & Identity Neutrality**:
   - Detect OS language automatically; if non-Arabic, launch 100% in English (or selected language) without any lingering developer-language artifacts. Zero compound slashed labels (`Save / حفظ` is prohibited).
