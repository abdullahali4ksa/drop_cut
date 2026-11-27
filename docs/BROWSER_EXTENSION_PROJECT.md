# DropCut — Cross-Browser Extension (Chrome, Edge, Firefox)
# (مشروع ملحق المتصفحات المستقل لـ DropCut)

> **Governance Notice:**  
> This document governs the independent sibling project **DropCut Browser Extension** (`DropCut.BrowserExtension`), tracked under Linear Project **`P-JMD-5`** (Team `JMD`).  
> **Status:** `STANDBY / PROPOSED` — No implementation code is to be executed until explicitly authorized by the developer in a dedicated task/conversation.

---

## 1. Project Overview & Target Platforms

| Attribute | Specification |
| :--- | :--- |
| **Project Type** | WebExtension (Cross-Browser Extension) |
| **Specification** | Manifest V3 (Google Chrome, Microsoft Edge, Mozilla Firefox) |
| **Linear Project** | `DropCut — Browser Extension (Chrome, Edge, Firefox)` (`P-JMD-5`) |
| **Linear Tracking** | `JMD-154` (`[WebExtension] تطوير ملحق المتصفحات لكروم وإيدج وفايرفوكس (Manifest V3)`) |
| **Technology Stack**| Vanilla JavaScript / TypeScript, HTML5 Canvas, WebExtension APIs |
| **Privacy & Security**| 100% Offline-First, Zero External Scripts, Zero Tracking/Analytics |
| **Desktop Bridge** | Local Custom URI Scheme (`dropcut://snip?id=...`) & System Clipboard |

---

## 2. Core Capabilities & Proposed Tasks

### [EXT-T001] Manifest V3 Cross-Browser Scaffold & Universal Packaging
- **Status:** `PROPOSED`
- Unified manifest configuration compatible with Chrome Web Store, Microsoft Edge Add-ons, and Firefox Add-ons (AMO).
- Zero remote scripts (eval-free, strict Content Security Policy).

### [EXT-T002] Visible Viewport Snip (التقاط المساحة المرئية الفورية)
- **Status:** `PROPOSED`
- 1-click capture of the currently displayed viewport using `chrome.tabs.captureVisibleTab`.
- Immediate high-DPI rasterization.

### [EXT-T003] DOM Element Snip & Hover Inspector (التقاط العنصر المختار من الصفحة)
- **Status:** `PROPOSED`
- Interactive visual overlay on hover that highlights DOM elements (cards, tweets, charts, code snippets).
- Computes element bounding box (`getBoundingClientRect`) and crops precisely without background clutter.

### [EXT-T004] Full-Page Panoramic Scrolling Capture (التقاط الصفحة بالكامل مع إزالة القوائم الثابتة)
- **Status:** `PROPOSED`
- Programmatic smooth window scrolling with automatic viewport stitching.
- Smart detection and suppression of `position: fixed` and `position: sticky` headers/banners during intermediate passes to prevent repeated bars.

### [EXT-T005] Native Desktop Handshake & Bridge (جسر الإرسال الفوري لتطبيق DropCut)
- **Status:** `PROPOSED`
- Seamless 1-click handoff of the captured image directly into DropCut Windows Desktop:
  - Primary: Invokes local OS protocol `dropcut://new-tab?source=browser` passing the image payload or staged temp path.
  - Secondary fallback: Direct PNG clipboard injection with desktop window foreground activation.

---

## 3. Directory Layout (When Activated)

```text
extensions/
└── dropcut-browser-extension/
    ├── manifest.json            # Manifest V3
    ├── src/
    │   ├── background/          # Service worker lifecycle
    │   ├── content/             # DOM element inspector & scroll engine
    │   ├── popup/               # Quick action popup UI
    │   └── utils/               # Native bridge & canvas stitching
    ├── icons/                   # Laughing Scissors mascot icons
    └── package.json
```
