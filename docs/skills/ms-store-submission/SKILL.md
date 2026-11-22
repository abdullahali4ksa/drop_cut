---
name: ms-store-submission
description: End-to-end Microsoft Store submission, pre-flight packaging audit, capability verification (runFullTrust), and browser automation for Windows desktop apps (MSIX/WinUI 3/WPF) adhering to Policies 10.1–10.14.
---

# Microsoft Store Submission & Compliance Automation Skill (`ms-store-submission`)

Use this skill whenever auditing, packaging, or automating the submission of Windows desktop applications (WinUI 3, WPF, Win32, MSIX) to the **Microsoft Partner Center** and **Microsoft Store**.

---

## 1. Core Principles & Supreme Governance

1. **Strict Policy Compliance (Policies 10.1 – 10.14)**:
   - **Policy 10.1 (Distinct Utility)**: The application must provide genuine, standalone desktop utility (not a mere website wrapper).
   - **Policy 10.2 (Security & Integrity)**: Clean deterministic binaries; zero malware, zero third-party remote payloads.
   - **Policy 10.5 & 10.14 (Privacy & Licensing)**: In-app privacy commitments, 100% offline-first operation where applicable, zero unconsented telemetry, and publicly accessible HTTPS `PRIVACY_POLICY.md`.
   - **Policy 10.8 (Visual Assets Suite)**: All 5 mandatory visual assets MUST be physically present inside the package archive before submission.
   - **Policy 10.14 (Restricted Capabilities)**: Any use of `runFullTrust` must be accompanied by the approved justification.

2. **Zero-Crash Mandate**:
   - Triple global unhandled exception boundaries (`App.UnhandledException`, `TaskScheduler.UnobservedTaskException`, `AppDomain.CurrentDomain.UnhandledException`).
   - Every `async void` UI event handler MUST be wrapped in a complete `try { ... } catch (Exception ex) { ... }`.
   - All file I/O must use `FileShare.ReadWrite` and atomic staging (`.tmp` -> replace).

---

## 2. Mandatory 5-Asset Suite Checklist

Every MSIX package must contain these exact visual assets referenced in `Package.appxmanifest`:

| Asset File | Required Dimensions | Purpose |
| :--- | :--- | :--- |
| `Assets\Square44x44Logo.png` | 44 × 44 px | Taskbar, Alt+Tab, App list icon |
| `Assets\Square150x150Logo.png` | 150 × 150 px | Medium Windows Start tile |
| `Assets\Wide310x150Logo.png` | 310 × 150 px | Wide Windows Start tile |
| `Assets\SplashScreen.png` | 620 × 300 px | App launch splash screen |
| `Assets\StoreLogo.png` | 50 × 50 px | Store catalog and installer badge |

---

## 3. Automated Pre-Flight Package Audit (Python)

Run this Python script on the generated `.msix` or `.msixbundle` file before uploading to Partner Center:

```python
import zipfile, xml.etree.ElementTree as ET, sys, os

def audit_msix(msix_path, expected_identity=None):
    print(f"=== Auditing MSIX: {os.path.basename(msix_path)} ({os.path.getsize(msix_path):,} bytes) ===")
    assert os.path.exists(msix_path), f"File not found: {msix_path}"
    
    with zipfile.ZipFile(msix_path, 'r') as z:
        names = z.namelist()
        assert 'AppxManifest.xml' in names, "FAIL: AppxManifest.xml is missing from MSIX!"
        
        manifest_data = z.read('AppxManifest.xml')
        root = ET.fromstring(manifest_data)
        ns = {'ns': 'http://schemas.microsoft.com/appx/manifest/foundation/windows10'}
        identity = root.find('ns:Identity', ns)
        
        name = identity.get('Name')
        pub = identity.get('Publisher')
        ver = identity.get('Version')
        arch = identity.get('ProcessorArchitecture')
        print(f"  [Identity] Name: {name} | Pub: {pub} | Ver: {ver} | Arch: {arch}")
        
        if expected_identity:
            assert name == expected_identity.get('Name'), f"Identity name mismatch: {name}"
            assert pub == expected_identity.get('Publisher'), f"Publisher mismatch: {pub}"
            
        required_assets = [
            'Assets/Square44x44Logo.png',
            'Assets/Square150x150Logo.png',
            'Assets/Wide310x150Logo.png',
            'Assets/SplashScreen.png',
            'Assets/StoreLogo.png'
        ]
        
        for asset in required_assets:
            matches = [n for n in names if n.lower() == asset.lower() or n.lower().endswith(asset.lower())]
            assert matches, f"FAIL: Mandatory asset missing: {asset}"
            info = z.getinfo(matches[0])
            print(f"  [PASS] {asset} -> {matches[0]} ({info.file_size} bytes)")
            
    print(">>> AUDIT RESULT: 100% PASS - Ready for Microsoft Partner Center Upload.\n")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        audit_msix(sys.argv[1])
```

---

## 4. Microsoft Partner Center Submission Workflow

When automating or executing a Store submission:

### 4.1 Products & Overview
1. Reserve Product Name matching brand (`Apps & Games` -> `Create a new app` -> Reserve Name).
2. Note Product ID (`9NV...`) and Store URL (`https://apps.microsoft.com/detail/<ID>`).

### 4.2 Step-by-Step Sections
1. **Pricing and Availability**:
   - Base Price: Free ($0.00 USD) or desired tier.
   - Markets: All worldwide markets (240+ countries and regions).
2. **Properties**:
   - Primary & Secondary Categories (e.g., Productivity, Utilities + tools).
   - Support Contact: Public repository or helpdesk URL.
   - Privacy Policy URL: Public HTTPS link to `PRIVACY_POLICY.md`.
3. **Age Ratings (IARC)**:
   - Select "All Other App Types" for standard utilities.
   - Complete questionnaire (No violence, no profanity, no user chat, no location tracking).
   - Yields: **Everyone / 3+** rating across ESRB, PEGI, USK, IARC.
4. **Store Listings**:
   - Add English (United States) and Arabic (or other supported languages).
   - Description, Product features, Short description, Copyright.
   - Upload at least 1–3 desktop screenshots (1920 × 1080 px or 1366 × 768 px).
   - Upload 1:1 App Tile Icon (300 × 300 px PNG).
5. **Packages**:
   - Target Device Families: Check **Windows 10/11 Desktop**.
   - Upload both `win-x64` and `win-arm64` MSIX packages.
   - **Handling 50 MiB Automation Limits**: If package size exceeds 50 MiB (self-contained .NET), stage files and allow user drag & drop or click "browse your files" in the browser pane.
6. **Submission Options & `runFullTrust` Justification**:
   - Standard Warning: `The following restricted capabilities require approval before you can use them in your app: runFullTrust.`
   - In "Notes for certification", provide:
     ```text
     This is a native WinUI 3 desktop application requiring runFullTrust capability to access Windows screen clipping APIs and global hotkeys.
     ```
7. **Submit for Certification**:
   - Verify all 6 cards show **Complete**.
   - Click the blue **Submit for certification** button.
