# UI Vertical Split Redesign — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Restructure the buff configuration window from horizontal layout (wide grid on top, cramped details below) to vertical split (buff list on left, spacious details on right).

**Architecture:** Create two panel containers (left-panel, right-panel) as children of Root. Re-parent buff grid + filters to left panel, details + targets to right panel. Adjust all anchor positions. Shrink target portraits from 166px to 60px. Replace text buttons with icon buttons.

**Tech Stack:** C# / Unity UI / RectTransform anchor-based layout

---

## Key References

- **Design doc:** `docs/plans/2026-03-08-ui-vertical-split-design.md`
- **Main UI file:** `BubbleBuffs/BubbleBuffer.cs` (~2800 lines)
- **UI helpers:** `BubbleBuffs/UIHelpers.cs` (Create, AddTo, SetAnchor extensions)
- **Build:** `~/.dotnet/dotnet build BubbleBuffs/BubbleBuffs.csproj -p:SolutionDir=$(pwd)/`
- **Deploy:** `scp BubbleBuffs/bin/Debug/BubbleBuffs.dll deck-direct:"/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/BubbleBuffs/"`

## Current Layout (anchor coordinates within Root)

| Component | anchorMin | anchorMax | Method |
|---|---|---|---|
| Summary bar | (0.15, 0.88) | (0.85, 0.93) | MakeSummary:2777 |
| Buff grid | (0.125, 0.47) | (0.875, 0.87) | MakeBuffsList:2528 |
| Filter controls | (0.13, 0.1) | (0.215, 0.4) | MakeFilters:870 |
| Category tabs | (0.785, 0.1) | (0.87, 0.4) | MakeFilters:897 |
| Details panel | (0.25, 0.225) | (0.75, 0.475) | MakeDetailsView:987 |
| Target portraits | (0.5, 0.08) single anchor | 166px height | MakeGroupHolder:1592 |

## Target Layout (anchor coordinates within Root)

| Component | anchorMin | anchorMax | Parent |
|---|---|---|---|
| Summary bar | (0.05, 0.90) | (0.95, 0.97) | Root (unchanged) |
| **Left panel** | (0.05, 0.05) | (0.38, 0.89) | Root (NEW) |
| Buff grid | (0, 0.18) | (1, 1) | left-panel |
| Filters | (0, 0.05) | (0.55, 0.17) | left-panel |
| Category tabs | (0.55, 0.05) | (1, 0.17) | left-panel |
| **Right panel** | (0.40, 0.05) | (0.95, 0.89) | Root (NEW) |
| Spell info + hide | top ~12% | — | right-panel |
| Source controls | ~12-30% | — | right-panel |
| Caster portraits | ~30-55% | — | right-panel |
| Target portraits | ~55-78% | — | right-panel |
| Action bar | bottom ~22% | — | right-panel |

---

### Task 1: Create panel containers + adjust summary

**Files:**
- Modify: `BubbleBuffs/BubbleBuffer.cs:448-495` (CreateWindow)
- Modify: `BubbleBuffs/BubbleBuffer.cs:2777-2798` (MakeSummary)

**Step 1: Add left/right panel fields to BubbleBuffSpellbookController**

Add after the existing `content` field references (around line 86 where other fields are):

```csharp
private Transform leftPanel;
private Transform rightPanel;
```

**Step 2: Create panels in CreateWindow()**

After `view.content = content;` (line 483), before `view.MakeSummary()` (line 486), insert:

```csharp
// Create left/right panel containers for vertical split layout
var (leftPanelObj, leftPanelRect) = UIHelpers.Create("left-panel", content);
leftPanelRect.anchorMin = new Vector2(0.05f, 0.05f);
leftPanelRect.anchorMax = new Vector2(0.38f, 0.89f);
leftPanelRect.offsetMin = Vector2.zero;
leftPanelRect.offsetMax = Vector2.zero;
leftPanel = leftPanelObj.transform;

var (rightPanelObj, rightPanelRect) = UIHelpers.Create("right-panel", content);
rightPanelRect.anchorMin = new Vector2(0.40f, 0.05f);
rightPanelRect.anchorMax = new Vector2(0.95f, 0.89f);
rightPanelRect.offsetMin = Vector2.zero;
rightPanelRect.offsetMax = Vector2.zero;
rightPanel = rightPanelObj.transform;

view.leftPanel = leftPanel;
```

Also add to BufferView class (around line 2510):
```csharp
public Transform leftPanel;
```

**Step 3: Adjust summary bar**

In MakeSummary() line 2797, change:
```csharp
// OLD
rect.Rect().SetAnchor(0.15, 0.85, 0.88, 0.93);
// NEW
rect.Rect().SetAnchor(0.05, 0.95, 0.90, 0.97);
```

**Step 4: Pass panels to Make* methods**

In CreateWindow(), change the method calls (lines 490-495):
```csharp
// OLD
MakeFilters(togglePrefab, content);
MakeGroupHolder(portraitPrefab, expandButtonPrefab, buttonPrefab, content);
MakeDetailsView(portraitPrefab, framePrefab, nextPrefab, prevPrefab, togglePrefab, expandButtonPrefab, content);

// NEW
MakeFilters(togglePrefab, leftPanel);
MakeGroupHolder(portraitPrefab, expandButtonPrefab, buttonPrefab, rightPanel);
MakeDetailsView(portraitPrefab, framePrefab, nextPrefab, prevPrefab, togglePrefab, expandButtonPrefab, rightPanel);
```

**Step 5: Build and verify**

```bash
~/.dotnet/dotnet build BubbleBuffs/BubbleBuffs.csproj -p:SolutionDir=$(pwd)/
```

Expected: compiles with 0 errors. UI will look broken (elements positioned wrong relative to new parents) — that's expected, fixed in subsequent tasks.

**Step 6: Commit**

```bash
git add BubbleBuffs/BubbleBuffer.cs
git commit -m "refactor: scaffold left/right panel containers for vertical split layout"
```

---

### Task 2: Move buff grid to left panel

**Files:**
- Modify: `BubbleBuffs/BubbleBuffer.cs:2528-2560` (MakeBuffsList)

**Step 1: Change buff list parent from `content` to `leftPanel`**

In MakeBuffsList() around line 2537, change the destroy lookup:
```csharp
// OLD
GameObject.Destroy(content.Find("AvailableBuffList")?.gameObject);

// NEW
var oldList = content.Find("AvailableBuffList") ?? leftPanel.Find("AvailableBuffList");
GameObject.Destroy(oldList?.gameObject);
```

Line 2543, change instantiation parent:
```csharp
// OLD
var availableBuffs = GameObject.Instantiate(listPrefab.gameObject, content);

// NEW
var availableBuffs = GameObject.Instantiate(listPrefab.gameObject, leftPanel ?? content);
```

**Step 2: Adjust grid anchors and column count**

Lines 2547-2553, change:
```csharp
// OLD
availableBuffs.GetComponentInChildren<GridLayoutGroupWorkaround>().constraintCount = 5;
// ...
listRect.anchorMin = new Vector2(0.125f, 0.47f);
listRect.anchorMax = new Vector2(0.875f, 0.87f);

// NEW — fill top portion of left panel, 3 columns
availableBuffs.GetComponentInChildren<GridLayoutGroupWorkaround>().constraintCount = 3;
// ...
listRect.anchorMin = new Vector2(0f, 0.18f);
listRect.anchorMax = new Vector2(1f, 1f);
```

**Step 3: Adjust content scale**

Line 2559, change from 0.8 to 0.75 (slightly smaller to fit narrower panel):
```csharp
// OLD
scrollContent.localScale = new Vector3(0.8f, 0.8f, 0.8f);

// NEW
scrollContent.localScale = new Vector3(0.75f, 0.75f, 0.75f);
```

**Step 4: Build, deploy, verify**

```bash
~/.dotnet/dotnet build BubbleBuffs/BubbleBuffs.csproj -p:SolutionDir=$(pwd)/
scp BubbleBuffs/bin/Debug/BubbleBuffs.dll deck-direct:"/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/BubbleBuffs/"
```

Expected: Buff grid appears on left side, 3 columns, scrollable. Right side and filters still broken.

**Step 5: Commit**

```bash
git add BubbleBuffs/BubbleBuffer.cs
git commit -m "refactor: move buff grid to left panel with 3-column layout"
```

---

### Task 3: Move filters + category tabs to left panel

**Files:**
- Modify: `BubbleBuffs/BubbleBuffer.cs:870-916` (MakeFilters)

**Step 1: Adjust filter anchors relative to left panel**

The `content` parameter already changed to `leftPanel` in Task 1. Now adjust positions:

```csharp
// OLD
filterRect.anchorMin = new Vector2(0.13f, 0.1f);
filterRect.anchorMax = new Vector2(0.215f, .4f);

// NEW — left half of left panel bottom area
filterRect.anchorMin = new Vector2(0f, 0.0f);
filterRect.anchorMax = new Vector2(0.55f, 0.17f);
```

**Step 2: Adjust category tabs position**

```csharp
// OLD
categoryRect.anchorMin = new Vector2(1 - filterRect.anchorMax.x, 0.1f);
categoryRect.anchorMax = new Vector2(1 - filterRect.anchorMin.x, 0.4f);

// NEW — right half of left panel bottom area
categoryRect.anchorMin = new Vector2(0.57f, 0.0f);
categoryRect.anchorMax = new Vector2(1f, 0.17f);
```

**Step 3: Build, deploy, verify**

Expected: Filters and category tabs appear below the buff grid in the left panel.

**Step 4: Commit**

```bash
git add BubbleBuffs/BubbleBuffer.cs
git commit -m "refactor: move filters and category tabs to left panel"
```

---

### Task 4: Restructure right panel — details view

**Files:**
- Modify: `BubbleBuffs/BubbleBuffer.cs:987-1585` (MakeDetailsView)

This is the largest task. The `content` parameter already changed to `rightPanel` in Task 1.

**Step 1: Expand detailsRect to fill most of right panel**

Around line 1004-1005:
```csharp
// OLD
detailsRect.anchorMin = new Vector2(0.25f, 0.225f);
detailsRect.anchorMax = new Vector2(0.75f, 0.475f);

// NEW — fill right panel fully
detailsRect.anchorMin = new Vector2(0f, 0f);
detailsRect.anchorMax = new Vector2(1f, 1f);
```

**Step 2: Reposition spell info/name display**

Around line 1015:
```csharp
// OLD
currentSpellRect.SetAnchor(.5, .8);

// NEW — top area of right panel
currentSpellRect.SetAnchor(.5, .92);
```

**Step 3: Reposition hide toggle**

Around line 1162:
```csharp
// OLD
var hideSpell = MakeToggle(togglePrefab, detailsRect.transform, 0.03f, 0.8f, "hideability".i8(), "hide-spell");

// NEW
var hideSpell = MakeToggle(togglePrefab, detailsRect.transform, 0.03f, 0.92f, "hideability".i8(), "hide-spell");
```

**Step 4: Reposition source controls**

Around line 1325-1327 (current source-controls anchors):
```csharp
// OLD
sourceControlRect.anchorMin = new Vector2(0.02f, 0.48f);
sourceControlRect.anchorMax = new Vector2(0.78f, 0.78f);

// NEW — below spell info, generous space
sourceControlRect.anchorMin = new Vector2(0.02f, 0.72f);
sourceControlRect.anchorMax = new Vector2(0.78f, 0.88f);
```

**Step 5: Reposition caster portraits**

Around line 1430-1432:
```csharp
// OLD
castersRect.anchorMin = new Vector2(0.5f, 0.22f);
castersRect.anchorMax = new Vector2(0.5f, 0.57f);
castersRect.SetAnchor(0.5f, 0.2f);
castersRect.sizeDelta = new Vector2(300, groupHeight);

// NEW — middle section, slightly smaller
const float groupHeight = 70f;
castersRect.SetAnchor(0.5f, 0.48f);
castersRect.sizeDelta = new Vector2(300, groupHeight);
```

Note: `const float groupHeight = 90f;` at line 1424 needs to change to 70f.

**Step 6: Reposition expand button for spell popout**

Find expandSpellPopout positioning (around line 1090-1100):
```csharp
// Adjust the expand button's anchor to match new spell info position
// This should already work since it's relative to detailsRect
```

**Step 7: Reposition buff group selector**

Around line 1460-1462:
```csharp
// OLD
groupRect.SetAnchor(0.9f, 0.6f);
groupRect.anchoredPosition = new Vector2(-20, 0);
groupRect.sizeDelta = new Vector2(140, 100);

// NEW — bottom-right of right panel
groupRect.SetAnchor(0.75f, 0.05f);
groupRect.anchoredPosition = new Vector2(0, 0);
groupRect.sizeDelta = new Vector2(140, 80);
```

**Step 8: Reposition add/remove buttons**

Around line 1173-1180:
```csharp
// OLD
addToAllRect.sizeDelta = new Vector2(180, 50);
addToAllRect.SetAnchor(0.03f, 0.1f);
// ...
removeFromAllRect.SetAnchor(0.03f, 0.3f);

// NEW — bottom-left, compact
addToAllRect.sizeDelta = new Vector2(140, 40);
addToAllRect.SetAnchor(0.03f, 0.05f);
// ...
removeFromAllRect.SetAnchor(0.03f, 0.12f);
```

**Step 9: Build, deploy, verify**

Expected: Right panel shows all detail elements with proper spacing. Source controls no longer overlap.

**Step 10: Commit**

```bash
git add BubbleBuffs/BubbleBuffer.cs
git commit -m "refactor: restructure details view to fill right panel"
```

---

### Task 5: Shrink target portraits + move to right panel

**Files:**
- Modify: `BubbleBuffs/BubbleBuffer.cs:1592-1653` (MakeGroupHolder)

**Step 1: Reduce portrait height and reposition**

```csharp
// OLD (line 1602-1606)
const float groupHeight = 166.25f;
groupRect.SetAnchor(0.5f, 0.08f);
groupRect.sizeDelta = new Vector2(300, groupHeight);
groupRect.pivot = new Vector2(0.5f, 0);

// NEW — much smaller, positioned in right panel middle area
const float groupHeight = 60f;
groupRect.SetAnchor(0.5f, 0.25f);
groupRect.sizeDelta = new Vector2(300, groupHeight);
groupRect.pivot = new Vector2(0.5f, 0);
```

The `content` parameter already points to `rightPanel` from Task 1.

**Step 2: Build, deploy, verify**

Expected: Target portraits are much smaller and positioned in the right panel.

**Step 3: Commit**

```bash
git add BubbleBuffs/BubbleBuffer.cs
git commit -m "refactor: shrink target portraits from 166px to 60px"
```

---

### Task 6: Replace buff group selector with icons

**Files:**
- Modify: `BubbleBuffs/BubbleBuffer.cs:1458-1468` (buff group ButtonGroup creation)

The HUD already uses icon sprites for Normal/Important/Quick (apply_buffs, apply_buffs_important, apply_buffs_short). We can reuse those as Sprites in the ButtonGroup.

**Step 1: Load sprites for group selector icons**

The sprites are already loaded as ButtonSprites in GlobalBubbleBuffer (line 1980-1985). We need to access the `_normal` sprite from each ButtonSprites. Check how ButtonSprites stores them:

```csharp
// If ButtonSprites has a Normal property/field for the sprite:
// Use applyBuffsSprites.Normal, applyBuffsImportantSprites.Normal, applyBuffsShortSprites.Normal
```

If direct sprite access isn't available, load them via AssetLoader:
```csharp
var normalIcon = AssetLoader.LoadInternal("icons", "apply_buffs_normal.png", new Vector2Int(24, 24));
var importantIcon = AssetLoader.LoadInternal("icons", "apply_buffs_important_normal.png", new Vector2Int(24, 24));
var quickIcon = AssetLoader.LoadInternal("icons", "apply_buffs_short_normal.png", new Vector2Int(24, 24));
```

**Step 2: Use icon-based ButtonGroup.Add**

```csharp
// OLD
buffGroup.Add(BuffGroup.Long, "group.normal".i8());
buffGroup.Add(BuffGroup.Important, "group.important".i8());
buffGroup.Add(BuffGroup.Quick, "group.short".i8());

// NEW — icons with short labels
buffGroup.Add(BuffGroup.Long, "N", normalIcon);
buffGroup.Add(BuffGroup.Important, "I", importantIcon);
buffGroup.Add(BuffGroup.Quick, "Q", quickIcon);
```

**Step 3: Reduce group selector size**

```csharp
groupRect.sizeDelta = new Vector2(100, 80);
```

**Step 4: Build, deploy, verify**

Expected: Buff group selector shows 3 icon buttons instead of text buttons.

**Step 5: Commit**

```bash
git add BubbleBuffs/BubbleBuffer.cs
git commit -m "refactor: use icon buttons for buff group selector"
```

---

### Task 7: Final layout tuning + deploy

**Files:**
- Modify: `BubbleBuffs/BubbleBuffer.cs` (various anchor adjustments)

**Step 1: Visual testing pass**

Build, deploy, and test every element:
- [ ] Buff grid scrolls correctly in left panel
- [ ] Search and filter toggles work
- [ ] Category tabs (Buffs/Abilities/Equipment) switch correctly
- [ ] Clicking a buff shows details in right panel
- [ ] Source controls (Use Spells/Scrolls/Potions/Equipment) don't overlap
- [ ] Priority label and cycle button visible and functional
- [ ] Caster portraits show with expand buttons
- [ ] Target portraits show and can be toggled
- [ ] Add/Remove all buttons work
- [ ] Buff group selector works
- [ ] Spell effects popout opens correctly
- [ ] Caster popout opens correctly
- [ ] Quick Open button still works (portraits hidden)

**Step 2: Adjust anchor values based on visual testing**

Tune any overlapping or misaligned elements. Common adjustments:
- Source toggle Y positions within sourceControlRect
- Priority text position
- Caster portrait row Y position
- Target portrait row Y position

**Step 3: Final build + deploy**

**Step 4: Commit**

```bash
git add BubbleBuffs/BubbleBuffer.cs
git commit -m "feat: vertical split UI layout with spacious detail panel"
```

---

## Risk Areas

1. **MakeBuffsList() re-creates on every recalculate** — it destroys and recreates the buff list by finding "AvailableBuffList" in its parent. After reparenting to leftPanel, the Find() call needs to search leftPanel, not content. Handle both paths for safety.

2. **Popouts (spellPopout, casterPopout)** — these are overlay panels. spellPopout is parented to detailsRect (fine). casterPopout is parented to content (Root). Since content is still Root.transform, this should work, but verify Z-order.

3. **Settings panel** — MakeSettings uses `content` as parent. This was NOT changed to rightPanel (it's a separate HUD popup). Verify it still works.

4. **view.content references** — MakeBuffsList uses `view.content` (set to Root.transform). We need `view.leftPanel` for the buff list but `view.content` for other things. The leftPanel field added in Task 1 handles this.
