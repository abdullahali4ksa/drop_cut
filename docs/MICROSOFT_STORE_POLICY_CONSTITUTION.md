# Universal Microsoft Store & Developer Account Protection Constitution
# (دستور حماية حساب المطور والاعتماد الشامل لمتجر مايكروسوفت)

> **Authority & Scope:**  
> This constitution is the supreme, non-negotiable policy governing **all current and future Windows desktop applications** authored by Abdullah Ali (`abdullahali4ksa` / `birotech`).  
> **Supreme Mandate:** Never compromise the developer's reputation or account standing. Eliminate every possible cause of certification rejection, store failure, or developer account suspension by enforcing 100% architectural rigor, zero-crash safety, total privacy transparency, and flawless packaging pre-flight checks.

---

## 1. The Core Enterprise Philosophy: Django / ERP Modular Architecture
Every software project must adopt the clean, modular, vertical-slice pattern proven in enterprise ERPs and Django applications:

1. **Self-Contained Vertical Slices (`src/[AppName].Modules/[Feature]/`)**:
   - Every business feature (e.g. Workspace, FileVault, Settings, AuditLog, Permissions) must reside entirely inside its own directory containing:
     - `Views/`: Pure XAML views (zero business logic in code-behind).
     - `ViewModels/`: Presentation state and commands (MVVM).
     - `Models/`: Feature-specific models and DTOs.
     - `Contracts/`: Local interface contracts.
     - `Services/`: Concrete business implementations.
     - `[Feature]Module.cs`: Dependency Injection registration.
2. **Zero Cross-Module Direct Coupling**:
   - Module A must **NEVER** reference the internal concrete types of Module B.
   - All cross-module contracts and shared domain models belong in `[AppName].Core/Contracts/`.
   - Inter-module communication must happen via a strongly-typed, in-process **Domain Event Bus** (`IEventBus`), achieving compile-time safety and runtime isolation.
3. **One Class = One File Law**:
   - Every `class`, `interface`, `record`, `struct`, or `enum` must reside in its own dedicated `.cs` file matching its exact name. Never bundle multiple types into one file.
4. **Anti-Duplication**:
   - Never create duplicate or versioned files (`*_v2`, `*_new`, `*_temp`). Refactor authoritative code in place.

---

## 2. Complete Microsoft Store Certification Laws (Policies 10.1 – 10.14)
To ensure zero risk of rejection or account penalty, every project must strictly satisfy each Microsoft Store Commercial Marketplace Certification Policy:

### Policy 10.1: Distinct Value & Clean Utility (القيمة الحقيقية ومنع التكرار)
- Every application must provide authentic, tangible, standalone utility for Windows users.
- **Strictly Prohibited:** Web-view wrappers of existing websites without native value, generic template clones, scraped content, or misleading placeholder apps.
- The app must perform substantially better, faster, and more integrated into Windows than a generic browser tab.

### Policy 10.2: Security & Binary Integrity (الأمان ونظافة الحزم البرمجية)
- **Zero Malware / Zero Harm:** The application must contain no malicious code, cryptominers, keystroke loggers, hidden background tasks, or unwanted software (PUS).
- **Zero Obfuscation / Zero Remote Payloads:** All application logic must be compiled deterministically from verifiable source code. Downloading and executing arbitrary unvetted executable code from remote servers is strictly forbidden.
- **Verified Dependencies:** Only official, maintained, vulnerability-free NuGet packages authored by Microsoft or reputable community foundations (e.g., CommunityToolkit) are allowed.

### Policy 10.5 & 10.14: Personal Information, Privacy & 100% Offline Mandate (الخصوصية المطلقة وحماية البيانات)
> **Account Suspension Warning:** Privacy violations (such as collecting user data or sending screenshots to external servers without consent) are the #1 reason for developer account bans on Microsoft Partner Center.
- **100% Offline-First Architecture:** Unless an app is explicitly an online cloud service, all capturing, editing, file processing, and OCR must execute **100% locally on the user's machine**.
- **Zero Telemetry / Zero Tracking:** No user screen captures, images, annotations, keystrokes, clipboard data, or analytics beacons may ever leave the device.
- **Mandatory Privacy Policy (`PRIVACY_POLICY.md`):**
  - Every project must maintain an authoritative, clearly stated `PRIVACY_POLICY.md` at the repository root and in `docs/`.
  - The live public GitHub link to `PRIVACY_POLICY.md` must be provided in the Microsoft Store listing and in the app's "About" dialog.
  - The policy must explicitly declare: zero data collection, local storage only, and user ownership of all created files.
- **Sandbox File System Isolation:**
  - MSIX install directory (`C:\Program Files\WindowsApps\...`) is strictly read-only.
  - All temporary files, databases, and logs must be written to `%LocalAppData%\[AppName]\`.
  - User-facing exported documents must only be written to standard user directories (`%USERPROFILE%\Pictures\[AppName]`, `%USERPROFILE%\Documents`, etc.).

### Policy 10.8: Software Quality & Mandatory Store Visual Assets Checklist (معايير جودة الحزم والشعارات)
> **Rejection Prevention:** A package will be immediately rejected during upload if any image referenced in `Package.appxmanifest` is missing.
- **Mandatory 5-Asset Suite:** Every package MUST generate, bundle, and verify all 5 standard visual assets before any submission:
  1. `Assets\Square44x44Logo.png` (44×44 px — App list, Taskbar, Alt+Tab)
  2. `Assets\Square150x150Logo.png` (150×150 px — Medium Start tile)
  3. `Assets\Wide310x150Logo.png` (310×150 px — Wide Start tile)
  4. `Assets\SplashScreen.png` (620×300 px — Application launch screen)
  5. `Assets\StoreLogo.png` (50×50 px — Official Store catalog icon)
- **High-DPI Vector Scaling:** All assets must be derived from a high-resolution master SVG, with high-quality bicubic interpolation and clean transparency.
- **Pre-Flight Asset Verification:** The build pipeline must inspect the generated `.msix` or `.msixbundle` ZIP archive to verify the physical presence of all 5 assets before uploading to Partner Center.

### Policy 10.14: Restricted Capabilities Protocol (`runFullTrust`)
- Modern WinUI 3 desktop applications require the `runFullTrust` capability in `Package.appxmanifest` to access Win32 desktop APIs (e.g. `GraphicsCaptureItem`, GDI+, global hotkeys).
- **Approved Certification Declaration Template:**  
  When submitting to Partner Center, always paste the standardized justification in the "Notes for certification" box:
  > *"This is a native WinUI 3 desktop application requiring the runFullTrust capability to access Windows desktop capture APIs (GraphicsCaptureItem / GDI) and system-wide keyboard shortcuts."*

---

## 3. Zero-Crash Mandate & Anti-Hang Engineering (ميثاق منع الانهيار)
To guarantee 100% stability, 0 crashes on user machines, and a perfect rating in the Store:

1. **Triple Global Exception Boundaries**:
   - `App.UnhandledException`
   - `TaskScheduler.UnobservedTaskException`
   - `AppDomain.CurrentDomain.UnhandledException`
   Must all be trapped, marked handled (`e.Handled = true`), and logged to a local non-blocking `crash.log`.
2. **Absolute Ban on Raw `async void`**:
   - All async methods must return `Task` or `ValueTask`.
   - The only exception is WinUI 3 event handlers (`Click`, `PointerPressed`, `Drop`), which MUST enclose their entire body in a complete `try { ... } catch (Exception ex) { ... }`.
3. **Non-Locking & Safe File I/O**:
   - Never lock files: open all streams with `FileShare.ReadWrite`.
   - Atomic replacement: write large files to `.tmp` first, then perform an atomic rename/move.
4. **Anti-Hang Responsiveness**:
   - Heavy tasks (OCR, image encoding, disk writes) must execute on background threads via `Task.Run`.
   - User inputs and continuous auto-save must be debounced (300ms) to maintain a rock-solid 60 FPS UI.

---

## 4. Internationalization & Developer Identity Neutrality (الاحترافية العالمية)
To ensure international customers and Microsoft certification testers experience a flawless, world-class product:

1. **Zero Compound Labels**:
   - Never combine languages on a single label (e.g. `Save / حفظ` is strictly forbidden).
2. **Automatic OS Locale Detection**:
   - If the user's Windows operating system is configured for English, Spanish, French, etc., the application must open **100% in that language** without any developer-language artifacts.
   - If the OS is Arabic, it defaults naturally to Arabic with proper Right-to-Left (RTL) flow direction.
3. **100% Translated UI Surface**:
   - Every single button, menu item, flyout option, tooltip, guidance banner, and status bar message must be retrieved via `ILocalizationService`.
   - Never hardcode user-visible strings in code-behind or event handlers.

---

## 5. Universal Multi-Architecture Support (x64 + Arm64)
1. Build native binaries for both architectures:
   - **`win-x64`**: Intel / AMD processors (Windows 10 & 11).
   - **`win-arm64`**: Qualcomm Snapdragon X Elite, Microsoft Surface Pro, Copilot+ PCs.
2. In Partner Center Device Families, check **Windows 10/11 Desktop (PC)** only, and uncheck Xbox, HoloLens, and Surface Hub unless explicitly built for them.

---

## 6. Pre-Submission Pre-Flight Gate (بوابة التدقيق الإلزامية قبل الرفع)
Before any package is submitted to Microsoft Partner Center, the developer/agent MUST complete this 6-point verification gate:

- [ ] **Gate 1 (Zero Warnings / Zero Errors Build):** `dotnet build -c Release` succeeds with 0 errors and 0 warnings.
- [ ] **Gate 2 (100% Test Pass Rate):** `dotnet test --no-build` passes 100% of unit and integration tests.
- [ ] **Gate 3 (5-Asset Integrity Check):** The generated `.msix` archive contains `StoreLogo.png`, `Wide310x150Logo.png`, `SplashScreen.png`, `Square150x150Logo.png`, and `Square44x44Logo.png`.
- [ ] **Gate 4 (Identity Parity):** `Identity/@Name`, `Identity/@Publisher`, and `Properties/@PublisherDisplayName` in `Package.appxmanifest` match the Partner Center product identity character-for-character.
- [ ] **Gate 5 (Privacy Policy Parity):** `PRIVACY_POLICY.md` is published and accessible via a public HTTPS URL.
- [ ] **Gate 6 (Clean Execution Test):** The compiled `.exe` runs and exits cleanly without unhandled exceptions in `crash.log`.
