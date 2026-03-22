# Multi-Group Buff Assignment

## Problem

Buffs can currently only be assigned to one BuffGroup (Normal, Important, Quick). Some buffs need to be in multiple groups — e.g., Shield should be in both Quick and Normal because short-duration characters need it recast via Quick while long-duration characters skip it (already active).

## Behavior

- A buff can belong to zero or more groups (checkboxes, not exclusive buttons).
- All groups unchecked = buff is never cast (effectively paused).
- New buffs default to Normal only.
- When executed, a buff is cast once per trigger. If already active on a target from a previous group trigger, it is skipped.
- A buff counts in the summary label of every group it belongs to.
- HUD buttons, shortcuts, and execution flow remain per-group — no changes there.

## Data Model

### BubbleBuff (BubbleBuff.cs)

Replace `InGroup` field:

```csharp
// Old
public BuffGroup InGroup = BuffGroup.Long;

// New
public HashSet<BuffGroup> InGroups = new HashSet<BuffGroup> { BuffGroup.Long };
```

### SavedBuffState (SaveState.cs)

Add new field, keep old for backward compatibility:

```csharp
[JsonProperty]
public BuffGroup InGroup; // Legacy — read during deserialization for migration

[JsonProperty]
public HashSet<BuffGroup> InGroups; // New — written on save
```

### Migration (BubbleBuff.InitialiseFromSave)

```
if InGroups is null or empty:
    InGroups = { InGroup }   // migrate from legacy single value
```

Old saves load correctly via the legacy `InGroup` field. After first save, only `InGroups` is written. Downgrade to older mod version falls back to `InGroup` default — acceptable for mod downgrades.

## Execution Filter

### BuffExecutor.Execute (BuffExecutor.cs:187)

```csharp
// Old
.Where(b => b.InGroup == buffGroup && b.Fulfilled > 0)

// New
.Where(b => b.InGroups.Contains(buffGroup) && b.Fulfilled > 0)
```

### Summary Labels (BubbleBuffer.cs:2932)

```csharp
// Old
.Where(b => b.InGroup == group)

// New
.Where(b => b.InGroups.Contains(group))
```

A buff in multiple groups is counted in each group's summary.

## UI Changes

### Buff Detail Panel (BubbleBuffer.cs:1633-1675)

Replace `ButtonGroup<BuffGroup>` (exclusive toggle buttons) with 3 independent `ToggleWorkaround` checkboxes:

- Reuse the existing `MakeSourceToggle()` pattern for consistent look with the Use Spells/Scrolls/Potions toggles below.
- Same container: HorizontalLayoutGroup in `actionBarSection`, same position and sizing.
- Each checkbox toggles its BuffGroup in `buff.InGroups` (Add/Remove) and calls `state.Save()`.

### UpdateDetailsView Sync

```
For each group checkbox:
    toggle.isOn = buff.InGroups.Contains(group)
```

## Validation & Credits

No changes. The credit system validates all buffs regardless of group membership. `Recalculate()` runs before each `Execute()` call, detecting which targets still need the buff. Multi-group assignment does not affect credit consumption — a buff is still one `BubbleBuff` object with one credit pool.

## Localization

Add checkbox labels to locale files if the existing `group.normal.btn`, `group.important.btn`, `group.short.btn` keys don't suffice. These keys should work as-is for checkbox labels.

## No Changes Required

- HUD buttons (3 buttons, one per group)
- Keyboard shortcuts (per-group bindings)
- BuffGroup enum values
- Buff scanning / RecalculateAvailableBuffs
- CasterQueue / BuffProvider logic
- Mass spell validation
