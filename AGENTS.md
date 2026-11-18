# AI Agent Operating Instructions — DropCut

> **Mandate:** You are an AI agent operating inside an architect-governed repository. You MUST strictly adhere to the instructions and constraints defined herein. Failure to follow these rules is considered an architectural breach.

---

## 1. Mandatory Reading Before Any Modification

Before touching or authoring ANY code, you MUST inspect and read:
1. `docs/ARCHITECTURE.md` (System design & layer boundaries)
2. `docs/PROJECT_RULES.md` (The Constitution & development laws)
3. `docs/MODULE_CATALOG.md` (Registry of self-contained modules)
4. `docs/TASKS.md` (Active tasks and roadmap)
5. `README.md` (Project overview)

---

## 2. Mandatory Dual-Line Header in Every Prompt & Task

Every single response, plan, and task description you process or output MUST include:
```text
POLICY_REF: docs/PROJECT_RULES.md (Architecture, Modular Vertical-Slice, Clean UI, Microsoft Store Standards)
SECURITY_REF: docs/PROJECT_RULES.md §Zero-Crash (Zero-Crash Mandate, Protected Async Void, Safe Try/Catch, Non-Locking I/O)
```

---

## 3. Strict Coding Laws

1. **Modular Vertical-Slice Law (Django / ERP Pattern)**:
   - All feature logic belongs inside `src/DropCut.Modules/[ModuleName]/`.
   - Never reference internal classes across modules. All cross-module contracts and domain events must live in `DropCut.Core`.
2. **One Class = One File**:
   - Every `class`, `interface`, `record`, `struct`, or `enum` MUST reside in its own dedicated `.cs` file matching its type name.
   - Never place two classes or enums in one file.
3. **Zero-Crash Mandate**:
   - Never write `async void` except WinUI 3 event handlers wrapped in complete `try { ... } catch (Exception ex) { ... }`.
   - Never allow unhandled exceptions to crash the process.
4. **Anti-Duplication**:
   - Never create `*_v2.cs`, `*_new.cs`, `*_temp.cs`, or duplicate classes. Edit authoritative files in place.
5. **No Ghost Features**:
   - Only implement tasks that exist in `docs/TASKS.md` and **Linear.app**.
6. **Architecture STOP Protocol**:
   - If an instruction requires changing layer boundaries, adding new external NuGet packages, or switching database engines, you MUST STOP immediately and ask for approval:
```text
APPROVAL_REQUIRED:
TYPE: [ARCHITECTURE_CHANGE | DEPENDENCY]
WHY:
FILES_AFFECTED:
PROPOSED_SOLUTION:
```

---

## 4. Verification Discipline

Before declaring any task done:
1. Run `dotnet build DropCut.sln` — must succeed with zero errors and zero warnings.
2. Run relevant tests in `tests/`.
3. Check actual command outputs — never hallucinate or claim success without real evidence.
