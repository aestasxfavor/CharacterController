# Empathy Hierarchy

A Hierarchy that reads at a glance: colored rows by rule, bookmarks, a scene selector and a memo desk with pictures.
Editor-only. Nothing is added to your scenes or builds.

- **Unity:** 6.3 – 6.7. The classic Hierarchy works on every supported version. The new UI Toolkit Hierarchy is supported from 6.5.
- **Theme:** Dark Editor theme recommended. The tool's own windows always use their dark look; Unity's window frames follow your Editor theme.

---

## Getting started

1. Import the package. Everything lives in `Assets/EmpathyHierarchy`.
2. Open your scene. Rows that match the built-in **Naming Rules** (Manager, Camera, Light, Level, UI…) are colored right away.
3. Open **Tools > Empathy Hierarchy > Rules** to pick a rule set, a theme and a fill style.
4. Select an object and press **Alt + E** (or **Alt + right-click** the row) to give it its own look.

Settings: **Edit > Project Settings > Empathy Hierarchy**.

---

## Features

### Rules
Objects get a look automatically. An object's own style always wins over a rule.

- **Naming Rules Base:** matches words in the object's name (`Manager`, `Light`, `Level`…).
- **Component Rules Base:** matches components (`Camera`, `Light`, `Canvas`, `AudioSource`…). `Name*` matches every component whose name starts with `Name`.
- **Custom Rules:** only your own rules.

Only one set is in use at a time. Every rule can match a name **or** a component, and has its own colors, gradient, icon and bold option.

**Inside a matched parent** sets what happens to matches below a matched object:
- Top match only
- Match all
- Match all · icon only
- Match all · own tint

**Make Rule** in the Edit Style window turns an object's look into a rule.

### Edit Style (Alt + E)
Per-object color, gradient, fill (Soft / Solid / Outline), bold, icon (Default / Auto / any built-in or project icon) and "color the children too".
Works on several selected objects at once.

### Separators
Name an object `---`, `===` or `//` (prefixes are set in Settings) to turn it into a separator bar. **GameObject > Empathy Hierarchy > Create Separator** makes one tagged EditorOnly, so it is left out of builds.

### Bookmarks
- **Alt + Click** a row to add or remove a bookmark. Bookmarked rows show a ★ after the name.
- **Alt + 1…9** jumps to bookmark 1…9.
- **Bar:** inside the new Hierarchy.
- **Tab:** a separate Bookmarks window, used with the classic Hierarchy.
- Click a bookmark to jump to its object: its parents unfold and it gets selected. Drag bookmarks to reorder them, or drag one into a memo.

### Scene Selector (Alt + Shift + S)
Open, add or switch scenes from the scene row's ▾ button.

### Memo Desk
Notes attached to objects, with pages and two columns.

- Type `/` for formatting:
  - Title
  - Subtitle
  - Quote
  - Callout
  - Code
  - Checklist
  - Paste picture
  - Picture file
  - Divider
- Typing `- ` or `1. ` starts a list.
- **Pictures:**
  - Add them with Ctrl/Cmd + V, by dropping image files on a memo, or from the `/` menu.
  - Click a picture to align it, copy it (Ctrl/Cmd + C) or delete it. Deleting asks first.
- **Undo:** Ctrl/Cmd + Z undoes inside the memo. Ctrl + Y or Ctrl/Cmd + Shift + Z redoes.

### Row widgets
- Child count
- Tag / layer chips
- Active toggle
- Tree lines
- Striped rows

Each one can be turned on or off in Settings.

### Presets
Take your look to another project, or share it: **Export** / **Import** at the top of the Rules window
(or **Tools > Empathy Hierarchy > Preset**). One `.ehpreset` file, with:

- **Rules:**
  - Naming, Component and Custom rule sets
  - The set in use, the fill, and Inside a matched parent
  - Theme
  - Tone
- **Settings:** everything in **Project Settings > Empathy Hierarchy**, for example:
  - Tag & Layer
  - Child count badge
  - Tree lines
  - Active checkbox
  - Bookmark bar mode
  - Separators
  - Accents
  - Icon lists

Memos, bookmarks and per-object styles stay with their project.
Import replaces the current values; a copy of them is kept first in `Library/EmpathyHierarchy/Backups`.
Inside one project nothing needs exporting: everything is in `ProjectSettings/EmpathyHierarchy/`, so version control shares it with the team.

---

## Shortcuts

All of these can be remapped in **Edit > Shortcuts > Empathy Hierarchy**.

| Action | Default |
|---|---|
| Edit Style | Alt + E (or Alt + right-click a row) |
| Toggle Bold | Alt + B |
| Collapse All | Alt + Shift + C |
| Bookmark 1…9 | Alt + 1…9 |
| Add / remove bookmark | Alt + Click a row |
| Scene Selector | Alt + Shift + S |

---

## Where things are saved

Everything is saved in `ProjectSettings/EmpathyHierarchy/`, so it can go into version control and be shared with your team:

| File | Contents |
|---|---|
| `Settings.asset` | Settings |
| `Rules.asset` | Rule sets |
| `Scenes/<scene>.json` | Per-object styles, one file per scene |
| `Bookmarks.json` | Bookmarks |
| `Memos.asset` | Memos |
| `MemoImages/` | Memo pictures (PNG, scaled to 1600 px at most) |

Scenes, prefabs and builds are never modified.

---

## Help

- **Tools > Empathy Hierarchy > Help > Create Diagnostics Report** writes `Logs/EmpathyHierarchy/hierarchy_diag.txt`. Please attach it when reporting a problem.
- **Tools > Empathy Hierarchy > Use New Hierarchy…** explains how to switch to the new Hierarchy (Unity 6.5+).

## Uninstall

Delete `Assets/EmpathyHierarchy`. To remove saved data too, delete `ProjectSettings/EmpathyHierarchy`.
