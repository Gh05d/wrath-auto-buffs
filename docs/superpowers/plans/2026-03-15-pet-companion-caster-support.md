# Pet/Companion Caster Support Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand `Bubble.Group` to include pet/companion units so they participate as both buff casters and targets.

**Architecture:** The `Bubble` static class holds the canonical `Group` list. Change it from a computed property (`ActualGroup`) to a cached field populated by `RefreshGroup()`, which appends pets from each party member. All three redundant `Group` properties are consolidated into `Bubble.Group`. A group-size-change check in `ShowBuffWindow()` triggers UI rebuild when pets join/leave.

**Tech Stack:** C#/.NET Framework 4.8.1, Unity UI, Kingmaker game API (`UnitPartPetMaster`, `EntityPartRef`)

**Spec:** `docs/superpowers/specs/2026-03-15-pet-companion-caster-support-design.md`

---

## File Structure

No new files. Changes to 2 existing files:

| File | Responsibility |
|------|---------------|
| `BuffIt2TheLimit/BubbleBuffer.cs` | `Bubble.Group` + `RefreshGroup()`, remove redundant `Group` properties, UI rebuild logic |
| `BuffIt2TheLimit/BufferState.cs` | Remove `GroupById` population (moved to `RefreshGroup()`), add QuickSlots null check |

---

## Chunk 1: Core Implementation

### Task 1: Implement `Bubble.RefreshGroup()` and convert `Group` to cached field

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:1-49` (add `using Kingmaker.UnitLogic.Parts;`)
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:3088-3091` (Bubble class)

- [ ] **Step 1: Add the using directive**

In `BubbleBuffer.cs`, add after the existing using directives (line 49):

```csharp
using Kingmaker.UnitLogic.Parts;
```

- [ ] **Step 2: Replace `Bubble` class with cached group + `RefreshGroup()`**

Replace the `Bubble` class at lines 3088-3091:

```csharp
// OLD:
static class Bubble {
    public static List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;
    public static Dictionary<string, UnitEntityData> GroupById = new();
}
```

With:

```csharp
static class Bubble {
    public static List<UnitEntityData> Group = new();
    public static Dictionary<string, UnitEntityData> GroupById = new();

    public static void RefreshGroup() {
        var baseGroup = Game.Instance.SelectionCharacter.ActualGroup;
        var result = new List<UnitEntityData>(baseGroup);

        foreach (var unit in baseGroup) {
            var petMaster = unit.Get<UnitPartPetMaster>();
            if (petMaster == null) continue;

            var pets = new List<UnitEntityData>();
            foreach (var petRef in petMaster.Pets) {
                var pet = petRef.Entity;
                if (pet != null && pet.IsInGame && !result.Contains(pet)) {
                    pets.Add(pet);
                }
            }
            pets.Sort((a, b) => string.Compare(a.UniqueId, b.UniqueId, StringComparison.Ordinal));
            result.AddRange(pets);
        }

        Group = result;

        GroupById.Clear();
        foreach (var u in Group) {
            GroupById[u.UniqueId] = u;
        }
    }
}
```

- [ ] **Step 3: Build to verify compilation**

Run:
```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo
```

Expected: Build succeeds (the old `Group` usages still compile because we changed from property to field, same type).

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: add Bubble.RefreshGroup() with pet/companion support"
```

---

### Task 2: Remove redundant `Group` properties and update call sites

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:457` (remove `BubbleBuffSpellbookController.Group`)
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:2495` (remove `GlobalBubbleBuffer.Group`)
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs` lines 524, 525, 1346-1360, 1811, 1813, 1821, 1826, 1887 (update unqualified `Group` to `Bubble.Group`)

- [ ] **Step 1: Remove `BubbleBuffSpellbookController.Group` property**

Delete line 457:

```csharp
// DELETE:
private List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;
```

- [ ] **Step 2: Replace all unqualified `Group` references in `BubbleBuffSpellbookController`**

These are all in `BubbleBuffSpellbookController` (they were resolving to the now-deleted private property). Replace `Group` with `Bubble.Group` at these locations:

Line 524: `for (int i = 0; i < Bubble.Group.Count; i++) {`
Line 525: `totalCasters += Bubble.Group[i].Spellbooks?.Count() ?? 0;`
Line 1346: `for (int i = 0; i < Bubble.Group.Count; i++) {`
Line 1347: `if (view.targets[i].Button.Interactable && !buff.UnitWants(Bubble.Group[i])) {`
Line 1348: `buff.SetUnitWants(Bubble.Group[i], true);`
Line 1358: `for (int i = 0; i < Bubble.Group.Count; i++) {`
Line 1359: `if (buff.UnitWants(Bubble.Group[i])) {`
Line 1360: `buff.SetUnitWants(Bubble.Group[i], false);`
Line 1811: `view.targets = new Portrait[Bubble.Group.Count];`
Line 1813: `for (int i = 0; i < Bubble.Group.Count; i++) {`
Line 1821: `portrait.Image.sprite = Bubble.Group[i].Portrait.SmallPortrait;`
Line 1826: `UnitEntityData me = Bubble.Group[personIndex];`
Line 1887: `if (state.GroupIsDirty(Bubble.Group)) {`

- [ ] **Step 3: Remove `GlobalBubbleBuffer.Group` property**

Delete line 2495:

```csharp
// DELETE:
public static List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;
```

This property is unused — all call sites already reference `Bubble.Group`.

- [ ] **Step 4: Build to verify compilation**

Run:
```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo
```

Expected: Build succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "refactor: consolidate Group properties into Bubble.Group"
```

---

### Task 3: Wire up `RefreshGroup()` and add UI rebuild + bounds guards

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:1851-1864` (ShowBuffWindow)
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:2798-2803` (ReorderTargetPortraits)
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:2956` (PreviewReceivers)

- [ ] **Step 1: Update `ShowBuffWindow()` to call `RefreshGroup()` and detect group size changes**

Replace the `ShowBuffWindow()` method at lines 1851-1864:

```csharp
// OLD:
private void ShowBuffWindow() {

    if (!WindowCreated) {
        try {
            CreateWindow();
        } catch (Exception ex) {
            Main.Error(ex, "Creating window?");
        }
    }
    state.Recalculate(true);
    RefreshFiltering();
    Root.SetActive(true);
    FadeIn(Root);
}
```

With:

```csharp
private void ShowBuffWindow() {
    Bubble.RefreshGroup();

    if (WindowCreated && view.targets.Length != Bubble.Group.Count) {
        Main.Verbose("Group size changed, rebuilding window");
        foreach (Transform child in Root.transform) {
            GameObject.Destroy(child.gameObject);
        }
        WindowCreated = false;
    }

    if (!WindowCreated) {
        try {
            CreateWindow();
        } catch (Exception ex) {
            Main.Error(ex, "Creating window?");
        }
    }
    state.Recalculate(true);
    RefreshFiltering();
    Root.SetActive(true);
    FadeIn(Root);
}
```

- [ ] **Step 2: Add bounds guard to `ReorderTargetPortraits()`**

At line 2800, add `&& i < targets.Length` to prevent `IndexOutOfRangeException` if `Bubble.Group` is refreshed (via `Recalculate()`) while `targets[]` hasn't been rebuilt yet:

```csharp
// OLD:
for (int i = 0; i < group.Count; i++) {
```

```csharp
// NEW:
for (int i = 0; i < group.Count && i < targets.Length; i++) {
```

- [ ] **Step 3: Add bounds guard to `PreviewReceivers()`**

At line 2956, same pattern:

```csharp
// OLD:
for (int p = 0; p < Bubble.Group.Count; p++)
```

```csharp
// NEW:
for (int p = 0; p < Bubble.Group.Count && p < targets.Length; p++)
```

- [ ] **Step 4: Build to verify compilation**

Run:
```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo
```

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: wire RefreshGroup into ShowBuffWindow with UI rebuild and bounds guards"
```

---

### Task 4: Add `RefreshGroup()` to `Recalculate()`, move `GroupById`, add QuickSlots null check

**Files:**
- Modify: `BuffIt2TheLimit/BufferState.cs:376-377` (add RefreshGroup call)
- Modify: `BuffIt2TheLimit/BufferState.cs:298` (QuickSlots null check)
- Modify: `BuffIt2TheLimit/BufferState.cs:360-363` (remove GroupById population)

- [ ] **Step 1: Add `RefreshGroup()` call at start of `Recalculate()`**

At line 376-377 in `BufferState.cs`, add `Bubble.RefreshGroup()` before the existing group access:

```csharp
// OLD:
internal void Recalculate(bool updateUi) {
    var group = Bubble.Group;
```

```csharp
// NEW:
internal void Recalculate(bool updateUi) {
    Bubble.RefreshGroup();
    var group = Bubble.Group;
```

This ensures the cached group is always fresh when any caller triggers recalculation, not just `ShowBuffWindow()`. The call is cheap (iterates ActualGroup + pets) and `Recalculate()` is not a hot path.

- [ ] **Step 2: Add QuickSlots null check**

At line 298 in `BufferState.cs`, wrap the QuickSlots iteration with a null check. Change:

```csharp
// OLD:
foreach (var slot in dude.Body.QuickSlots) {
```

To:

```csharp
if (dude.Body.QuickSlots == null) continue;
foreach (var slot in dude.Body.QuickSlots) {
```

Note: the `continue` here continues the outer `for` loop (characterIndex loop at line 296), so if a pet has no QuickSlots, it skips the whole QuickSlots scan for that unit.

- [ ] **Step 2: Remove `GroupById` population from `RecalculateAvailableBuffs`**

Delete lines 362-363:

```csharp
// DELETE:
foreach (var u in Group)
    Bubble.GroupById[u.UniqueId] = u;
```

This is now handled by `Bubble.RefreshGroup()`.

- [ ] **Step 3: Build to verify compilation**

Run:
```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/BufferState.cs
git commit -m "feat: add RefreshGroup to Recalculate, QuickSlots null check, move GroupById"
```

---

### Task 5: Build release and deploy for testing

**Files:** None modified (build + deploy only)

- [ ] **Step 1: Full release build**

Run:
```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo
```

Expected: Build succeeds with 0 errors.

- [ ] **Step 2: Deploy to Steam Deck for testing**

Run:
```bash
./deploy.sh
```

Expected: DLL + Info.json copied to Steam Deck.

- [ ] **Step 3: Commit all changes if not already committed**

Verify with `git status` that working tree is clean.

---

## Manual Testing Checklist

After deployment, test on Steam Deck:

1. **Aivu (Azata Dragon)**: Open spellbook → buff setup. Verify Aivu appears as target portrait AND her spells appear in the buff list with her as a caster.
2. **Hag of Gyronna**: Verify the Hag appears as target portrait and her at-will spells appear in the buff list (should show "At-will" in caster credits).
3. **Animal Companion**: If available, verify animal companion appears as target portrait. Most animal companions have no spells, so they should appear as targets only.
4. **Personal spells**: Select a pet's Personal spell. Verify only the pet itself is selectable as target (other portraits grayed out / red overlay).
5. **Buff execution**: Assign a pet buff, execute the buff group. Verify the pet actually casts the spell.
6. **Window rebuild**: Open buff window, then change party composition (add/remove a companion with a pet). Reopen buff window. Verify portrait count updates correctly without crash.
7. **Save/Load**: Configure pet buff assignments, save, reload. Verify assignments persist.
8. **Party without pets**: Test with a party that has no pets. Verify no regressions — behavior should be identical to before.
