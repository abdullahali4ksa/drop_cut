# DropCut — Module Catalog & Registry

This document lists all self-contained feature modules in DropCut adhering to the **Modular Vertical-Slice Architecture** (Django Apps / ERP pattern). Every module owns its vertical stack and communicates with other modules exclusively through `DropCut.Core` contracts and domain events.

---

## 1. `Shell` Module (DropCut.App)

* **Location:** `src/DropCut.App/`
* **Purpose:** Acts as the host container, composition root, navigation frame, and multi-tab document shell.
* **Responsibilities:**
  - Application entry point (`App.xaml.cs`) and Dependency Injection bootstrapping.
  - Multi-tab window layout (`MainWindow.xaml`) using WinUI 3 `TabView`.
  - Tab lifecycle, tab dragging, and universal context menu (Pin, Favorites, Close Right, Close Other, Close All).
  - Global unhandled exception trapping (`App.UnhandledException`).
  - Global UI styles, brushes, typography, and converters.

---

## 2. `Workspace` Module (DropCut.Modules/Workspace)

* **Location:** `src/DropCut.Modules/Workspace/`
* **Purpose:** Reverse-engineered screen capture engine, annotation canvas, and outward drag-and-drop sharing pipeline.
* **Responsibilities:**
  - **Screen Snapping Subsystem:**
    - Transparent multi-monitor overlay window for snip selection.
    - 4 Snip modes: Rectangular, Window, Full-screen, Freeform.
    - Countdown timer delay (3s, 5s, 10s).
    - Crosshair cursor, dimension labels, and magnified pixel loupe.
  - **Annotation & Editing Canvas:**
    - High-performance InkCanvas / Win2D rendering.
    - Pen, Highlighter, Shapes (Rectangle, Ellipse, Arrow, Line), Text box.
    - Crop tool with aspect-ratio locking.
    - Privacy Blur / Pixelate / Redact tool with adjustable blur radius.
  - **Outward Drag-and-Drop Sharing:**
    - In-canvas interactive hand cursor (`DragHand`) on image hover.
    - Packaging bitmap and file path into WinUI `DataPackage`.
    - Dragging out of DropCut into any web application or external desktop software.
* **Internal Anatomy:**
  - `Views/`: `WorkspacePage.xaml`, `SnipOverlayWindow.xaml`, `CanvasEditorView.xaml`.
  - `ViewModels/`: `WorkspaceViewModel.cs`, `SnipOverlayViewModel.cs`, `ToolboxViewModel.cs`.
  - `Models/`: `SnipRequest.cs`, `AnnotationStroke.cs`, `CropBounds.cs`, `ToolType.cs`.
  - `Contracts/`: `ISnappingEngine.cs`, `IAnnotationCanvasService.cs`, `IDragDropSharingService.cs`.
  - `Services/`: `SnappingEngine.cs`, `AnnotationCanvasService.cs`, `DragDropSharingService.cs`.
  - `Helpers/`: `CursorHelper.cs`, `CoordinateTransformHelper.cs`.
  - `WorkspaceModule.cs`: DI extension method.
* **Events Published:** `SnippetCapturedEvent`, `ImageAnnotatedEvent`, `CropAppliedEvent`.

---

## 3. `FileVault` Module (DropCut.Modules/FileVault)

* **Location:** `src/DropCut.Modules/FileVault/`
* **Purpose:** File management, continuous auto-saving on edit, storage paths, and image format export.
* **Responsibilities:**
  - **Continuous Auto-Save Engine**:
    - Listens to `ImageAnnotatedEvent` and debounces writes by 300ms.
    - Commits images atomically via `.tmp` staging files.
    - Eliminates the need for manual `Ctrl+S`.
  - **Recent Snippets Gallery**:
    - Indexes captures by timestamp, dimensions, tags, and origin.
  - **Multi-Format Exporter**:
    - Serializes canvas to PNG, JPEG, WebP, and SVG format.
* **Internal Anatomy:**
  - `Views/`: `VaultGalleryPage.xaml`, `ExportDialog.xaml`.
  - `ViewModels/`: `VaultGalleryViewModel.cs`, `ExportViewModel.cs`.
  - `Models/`: `VaultFile.cs`, `SnippetMetadata.cs`, `ExportFormat.cs`.
  - `Contracts/`: `IFileVaultService.cs`, `IAutoSaveService.cs`, `IImageExportService.cs`.
  - `Services/`: `FileVaultService.cs`, `AutoSaveService.cs`, `ImageExportService.cs`.
  - `Data/`: `VaultFileRepository.cs`, `IVaultFileRepository.cs`.
  - `Helpers/`: `ImageEncoderHelper.cs`, `FileNamingHelper.cs`.
  - `FileVaultModule.cs`: DI extension method.
* **Events Published:** `FileAutoSavedEvent`, `FileExportedEvent`.
* **Events Subscribed:** `SnippetCapturedEvent`, `ImageAnnotatedEvent`.

---

## 4. `Permissions` Module (DropCut.Modules/Permissions)

* **Location:** `src/DropCut.Modules/Permissions/`
* **Purpose:** Role-based access control, security policies, and feature locking (ERP-style governance).
* **Responsibilities:**
  - Evaluating user roles (Admin, Editor, Viewer).
  - Feature access checks (e.g. restricting export, redaction bypass, AI execution, or vault path remapping).
  - Integrating with Core security contracts.
* **Internal Anatomy:**
  - `Views/`: `SecuritySettingsView.xaml`.
  - `ViewModels/`: `SecuritySettingsViewModel.cs`.
  - `Models/`: `AppRole.cs`, `FeaturePermission.cs`, `AccessPolicy.cs`.
  - `Contracts/`: `IModulePermissionService.cs`, `IPolicyEvaluator.cs`.
  - `Services/`: `ModulePermissionService.cs`, `PolicyEvaluator.cs`.
  - `Data/`: `PermissionRepository.cs`, `IPermissionRepository.cs`.
  - `Helpers/`: `SecurityClaimsHelper.cs`.
  - `PermissionsModule.cs`: DI extension method.

---

## 5. `AuditLog` Module (DropCut.Modules/AuditLog)

* **Location:** `src/DropCut.Modules/AuditLog/`
* **Purpose:** Complete operation history, database audit trail, and non-destructive undo/redo tracking.
* **Responsibilities:**
  - Recording user and system operations (Capture, Draw, Erase, Redact, Crop, AutoSave, Export).
  - Managing per-snippet Undo and Redo state stacks.
  - Persisting audit trails to SQLite for regulatory / compliance tracking.
* **Internal Anatomy:**
  - `Views/`: `AuditHistoryView.xaml`.
  - `ViewModels/`: `AuditHistoryViewModel.cs`.
  - `Models/`: `AuditRecord.cs`, `UndoRedoState.cs`, `AuditEventType.cs`.
  - `Contracts/`: `IAuditLogService.cs`, `IUndoRedoManager.cs`.
  - `Services/`: `AuditLogService.cs`, `UndoRedoManager.cs`.
  - `Data/`: `AuditRecordRepository.cs`, `IAuditRecordRepository.cs`.
  - `Helpers/`: `AuditFormatterHelper.cs`.
  - `AuditLogModule.cs`: DI extension method.
* **Events Subscribed:** `SnippetCapturedEvent`, `ImageAnnotatedEvent`, `FileAutoSavedEvent`.

---

## 6. `Settings` Module (DropCut.Modules/Settings)

* **Location:** `src/DropCut.Modules/Settings/`
* **Purpose:** Application preferences, 10-language pure i18n localization, themes, and global shortcuts.
* **Responsibilities:**
  - **Pure Multi-Language Engine (i18n):**
    - Arabic (Default, full RTL layout), English, Spanish, Hindi, Chinese, French, German, Russian, Japanese, Portuguese.
    - Seamless dynamic language switching without application restart.
    - Zero compound labels (`Cut / قص` strictly forbidden).
  - **Themes & Presentation:**
    - Mica Alt, Acrylic, Light, Dark, System theme synchronization.
  - **Global Hotkey & Timing Settings:**
    - Global snipping hotkey (`Win+Shift+S` or custom hotkey registration).
    - Default delay countdown timers, auto-save storage paths, and clipboard auto-copy.
* **Internal Anatomy:**
  - `Views/`: `SettingsPage.xaml`.
  - `ViewModels/`: `SettingsViewModel.cs`.
  - `Models/`: `UserPreferences.cs`, `LanguageOption.cs`, `AppThemeOption.cs`.
  - `Contracts/`: `ISettingsService.cs`, `ILocalizationService.cs`.
  - `Services/`: `SettingsService.cs`, `LocalizationService.cs`.
  - `Data/`: `PreferencesRepository.cs`, `IPreferencesRepository.cs`.
  - `Helpers/`: `ResourceStringHelper.cs`, `SystemThemeHelper.cs`.
  - `SettingsModule.cs`: DI extension method.
* **Events Published:** `LanguageChangedEvent`, `ThemeChangedEvent`.
