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

> **Developer Scope Mandate:**  
> This project operates as an **independent standalone browser tool**. Upon capture, it immediately displays the snippet card inside the browser, allowing the user to **share it, save it, copy it, drag & drop it directly** into web chats (WhatsApp, Telegram, Discord), or optionally bridge it to the DropCut desktop app.

### [EXT-T001] Manifest V3 Cross-Browser Scaffold & Universal Packaging
- **Status:** `PROPOSED`
- Unified manifest configuration compatible with Chrome Web Store, Microsoft Edge Add-ons, and Firefox Add-ons (AMO).
- Zero remote scripts (eval-free, strict Content Security Policy).

### [EXT-T002] Full-Page Panoramic Scrolling Capture with Sticky Header Cleanup (التقاط الصفحة الطويلة بالكامل مع تنظيف الأشرطة الثابتة)
- **Status:** `PROPOSED`
- Programmatic smooth window scrolling with automatic viewport stitching.
- Smart detection and temporary suppression of `position: fixed` and `position: sticky` headers/banners during intermediate passes to prevent repeated bars.

### [EXT-T003] DOM Element Snip & Hover Inspector (التقاط العنصر المختار من بنية الصفحة)
- **Status:** `PROPOSED`
- Interactive visual overlay on hover that highlights DOM elements (cards, tweets, charts, code snippets).
- Computes element bounding box (`getBoundingClientRect`) and crops precisely without background clutter.

### [EXT-T004] Visible Viewport Instant Snip & Shortcuts (`Alt+Shift+S`) (التقاط المساحة المعروضة فوراً)
- **Status:** `PROPOSED`
- 1-click capture of the currently displayed viewport using `chrome.tabs.captureVisibleTab`.
- Global configurable hotkey (`Alt + Shift + S`) for rapid web snips.

### [EXT-T005] Standalone In-Browser Snippet Card: Save, Copy, Share & Direct Drag & Drop (نافذة القصاصة المستقلة: الحفظ، النسخ، المشاركة، والسحب والإفلات المباشر)
- **Status:** `PROPOSED`
- Displays a clean floating card / viewer modal in the browser immediately upon capture:
  - **Direct Outward Drag & Drop:** Users can drag the captured image directly from the browser card into WhatsApp Web, Telegram, Discord, Gmail, Slack, or File Explorer.
  - **1-Click Actions:** Download as PNG/JPEG, Copy image to clipboard, Web Share API (`navigator.share`).
  - **Quick Annotation:** In-browser mini drawing tools (Crop, Arrow, Text, Redaction blur).

### [EXT-T006] Web Article to Clean Markdown Extractor (تفريغ مقالات الويب إلى ماركداون نظيف)
- **Status:** `PROPOSED`
- 1-click article reader engine: extracts main article content, strips ads and navigation clutter, and converts text, links, and code blocks into clean Markdown ready to copy or download.

### [EXT-T007] Seamless Desktop Bridge to DropCut Windows App (`dropcut://`) (جسر الربط الاختياري بتطبيق سطح المكتب)
- **Status:** `PROPOSED`
- Optional 1-click button "Open in DropCut Desktop" for users who have the Windows app installed:
  - Invokes local OS protocol `dropcut://new-tab?source=browser` passing the image payload or staged temp path.
  - Fallback: Direct PNG clipboard injection with desktop window activation.

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
