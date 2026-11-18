# DropCut — Database Schema & Persistence Blueprint

> **Engine:** SQLite (File-based local storage via Microsoft.EntityFrameworkCore.Sqlite)  
> **Database File Path:** `%LocalAppData%\DropCut\dropcut.db`  
> **Migration Strategy:** EF Core Code-First Migrations

---

## 1. Tables & Entity Schema

### 1.1 `Snippets` (Metadata Index for Captured Images)
Stores metadata for all captures, active drafts, and archived snippets.

```sql
CREATE TABLE IF NOT EXISTS "Snippets" (
    "Id" TEXT NOT NULL PRIMARY KEY,            -- UUID (Guid)
    "Title" TEXT NOT NULL,                     -- Snippet title or auto-generated name
    "FilePath" TEXT NOT NULL,                  -- Local absolute path to saved image
    "CaptureMode" INTEGER NOT NULL,            -- 0=Rectangular, 1=Window, 2=Fullscreen, 3=Freeform
    "Width" INTEGER NOT NULL,                  -- Image width in pixels
    "Height" INTEGER NOT NULL,                 -- Image height in pixels
    "DpiScale" REAL NOT NULL DEFAULT 1.0,      -- High-DPI monitor scale factor
    "IsPinned" INTEGER NOT NULL DEFAULT 0,     -- Boolean (0=False, 1=True)
    "IsFavorite" INTEGER NOT NULL DEFAULT 0,   -- Boolean (0=False, 1=True)
    "CreatedAt" TEXT NOT NULL,                 -- ISO 8601 UTC timestamp
    "UpdatedAt" TEXT NOT NULL                  -- ISO 8601 UTC timestamp
);

CREATE INDEX "IX_Snippets_CreatedAt" ON "Snippets" ("CreatedAt" DESC);
CREATE INDEX "IX_Snippets_IsFavorite" ON "Snippets" ("IsFavorite");
```

---

### 1.2 `Annotations` (Serialized Vector & Redaction Actions)
Persists individual annotation strokes and redact masks for non-destructive re-editing.

```sql
CREATE TABLE IF NOT EXISTS "Annotations" (
    "Id" TEXT NOT NULL PRIMARY KEY,            -- UUID (Guid)
    "SnippetId" TEXT NOT NULL,                 -- Foreign Key to Snippets.Id
    "Type" INTEGER NOT NULL,                   -- 0=Pen, 1=Highlighter, 2=Shape, 3=Text, 4=Blur/Redact, 5=Crop
    "StrokeDataJson" TEXT NOT NULL,            -- Serialized coordinates, colors, thickness, or text
    "ZIndex" INTEGER NOT NULL DEFAULT 0,       -- Stacking order
    "CreatedAt" TEXT NOT NULL,                 -- ISO 8601 UTC timestamp
    CONSTRAINT "FK_Annotations_Snippets_SnippetId" FOREIGN KEY ("SnippetId") 
        REFERENCES "Snippets" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Annotations_SnippetId" ON "Annotations" ("SnippetId");
```

---

### 1.3 `AuditLogs` (Complete History & Undo/Redo Records)
Tracks all modifications, user operations, and auto-save events for compliance and rollback.

```sql
CREATE TABLE IF NOT EXISTS "AuditLogs" (
    "Id" TEXT NOT NULL PRIMARY KEY,            -- UUID (Guid)
    "SnippetId" TEXT NULL,                     -- Optional FK to Snippet
    "EventType" INTEGER NOT NULL,              -- 0=Capture, 1=Draw, 2=Crop, 3=Redact, 4=AutoSave, 5=Export
    "Description" TEXT NOT NULL,               -- Human-readable activity description
    "StateSnapshotJson" TEXT NULL,             -- Serialized state for Undo/Redo recovery
    "Timestamp" TEXT NOT NULL,                 -- ISO 8601 UTC timestamp
    "ActorRole" TEXT NOT NULL DEFAULT 'User'   -- Actor role associated with event
);

CREATE INDEX "IX_AuditLogs_Timestamp" ON "AuditLogs" ("Timestamp" DESC);
CREATE INDEX "IX_AuditLogs_SnippetId" ON "AuditLogs" ("SnippetId");
```

---

### 1.4 `Permissions` (Role-Based Access Control Policies)
Defines enterprise permissions and capability restrictions.

```sql
CREATE TABLE IF NOT EXISTS "Permissions" (
    "Id" TEXT NOT NULL PRIMARY KEY,            -- UUID (Guid)
    "RoleName" TEXT NOT NULL,                  -- 'Admin', 'Editor', 'Viewer'
    "FeatureKey" TEXT NOT NULL,                -- e.g., 'Export.External', 'Redact.Bypass', 'AutoSave.Remap'
    "IsAllowed" INTEGER NOT NULL DEFAULT 1,    -- Boolean (0=Denied, 1=Allowed)
    "UpdatedAt" TEXT NOT NULL
);

CREATE UNIQUE INDEX "IX_Permissions_Role_Feature" ON "Permissions" ("RoleName", "FeatureKey");
```

---

### 1.5 `UserPreferences` (Persistent Settings)
Stores key-value configurations, themes, selected language, and hotkeys.

```sql
CREATE TABLE IF NOT EXISTS "UserPreferences" (
    "Key" TEXT NOT NULL PRIMARY KEY,           -- Preference key (e.g., 'PreferredLanguage', 'Theme')
    "Value" TEXT NOT NULL,                     -- Serialized string/JSON value
    "UpdatedAt" TEXT NOT NULL
);
```

---

## 2. Integrity & Concurrency Rules

1. **WAL Mode (Write-Ahead Logging)**:
   - SQLite connection executes `PRAGMA journal_mode = WAL;` and `PRAGMA synchronous = NORMAL;` on connection setup for high concurrency and zero UI blocking.
2. **Transaction Scoping via `IUnitOfWork`**:
   - Multiple entity updates (e.g. creating a snippet + creating an initial audit record) are always committed within an atomic database transaction.
3. **Safe Storage Isolation**:
   - Database resides strictly in `%LocalAppData%\DropCut\dropcut.db`, ensuring 100% compliance with MSIX sandboxing rules.
