# Privacy Policy for DropCut (سياسة الخصوصية لتطبيق DropCut)

**Last Updated:** November 20, 2026  
**Effective Date:** November 20, 2026  
**Product Name:** DropCut — Advanced Snipping Tool & Drag-to-Share Workspace  
**Developer:** Abdullah Ali (abdullahali4ksa)  

---

## 1. Overview & 100% Offline Guarantee
DropCut is engineered with a strict **Privacy-by-Design** and **100% Offline-First** philosophy. DropCut functions entirely on your local Windows PC without requiring an internet connection, account creation, or cloud connectivity.

> **Zero Cloud Collection Mandate:**  
> DropCut **DOES NOT** collect, transmit, store, sell, or share any personal information, screen captures, images, annotations, OCR text, or usage telemetry with any external servers, third-party services, or analytics providers.

---

## 2. Information Handled Locally on Your Device

### A. Screen Captures and Snapshots
- When you initiate a screen snip (Rectangle, Window, Full Screen, or Freeform), the snapshot is captured directly via Windows native APIs (`GraphicsCaptureItem` / GDI+) and held securely in local machine memory (RAM).
- Snapshots are only saved to persistent storage if you explicitly click the **Save** button or configure local auto-saving. All saved files are placed strictly in your personal local folder:
  `%USERPROFILE%\Pictures\DropCut`
- No images or pixel data ever leave your device.

### B. Inking, Annotations, and Drawings
- Any digital ink, pen strokes, highlights, and annotations drawn within the workspace are rendered locally using native WinUI 3 graphics components.
- When committing changes, the composition is baked into the local bitmap stream on your machine.

### C. Optical Character Recognition (OCR)
- Text extraction functions entirely on-device using local Windows OCR capabilities (`Windows.Media.Ocr.OcrEngine`). No extracted text, clipboard content, or tokens are sent to external servers or AI endpoints.

### D. Drag & Drop Direct Sharing
- When dragging a tab or snapshot to external applications (such as WhatsApp, Telegram, Microsoft Outlook, or Windows File Explorer), the file transfer occurs directly via the Windows OS local OLE / `DataPackage` clipboard pipeline. DropCut does not monitor, track, or intercept the target application.

---

## 3. Telemetry and Analytics
- **Zero Telemetry:** DropCut contains no telemetry SDKs, Google Analytics, Firebase, App Center, or third-party tracking cookies.
- **Diagnostic Logging:** Any runtime diagnostic logs (such as error traces or crash prevention records) are written exclusively to a local file (`crash.log` / `diagnostics.log`) in the application's local sandbox folder and are never automatically uploaded.

---

## 4. Microsoft Store Certification Compliance
DropCut adheres strictly to Microsoft Store Policies:
- **Section 10.5 (Personal Information):** DropCut does not collect or transmit any personally identifiable information (PII).
- **Section 10.8 (Software Security):** DropCut does not contain malware, backdoors, hidden network calls, or unsolicited modifications.
- **Section 10.1 (Data Protection):** All local files created by DropCut belong exclusively to the operating system user and can be inspected, moved, or deleted at any time through Windows File Explorer.

---

## 5. Third-Party Libraries & Dependencies
DropCut utilizes verified, open-source, Microsoft-authored frameworks:
- **Windows App SDK / WinUI 3**
- **CommunityToolkit.Mvvm**

None of these libraries include tracking beacons, remote ads, or unauthorized external network calls.

---

## 6. Children's Privacy
DropCut does not address or collect information from children or any individuals of any age. Because no data is collected, DropCut is completely safe for all audiences, students, and enterprise environments.

---

## 7. Contact Information
If you have any questions or feedback regarding this Privacy Policy or DropCut's security design, please open an issue on the official GitHub repository:
- **Repository:** https://github.com/abdullahali4ksa/drop_cut
- **Developer:** Abdullah Ali (`abdullahali4ksa`)
