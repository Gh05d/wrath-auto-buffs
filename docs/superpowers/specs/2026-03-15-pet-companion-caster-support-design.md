# Pet/Companion Caster Support

**Date:** 2026-03-15
**Status:** Draft

## Problem

Buff It 2 The Limit only scans main party members (`ActualGroup`) for available buff spells. Pet and companion units — such as the Hag from Hag of Gyronna (at-will spells) or Aivu the Azata Dragon (full spellbook) — are not recognized as buff casters. Some pets (like Aivu) already appear as targets if the game includes them in `ActualGroup`, but their spells are not consistently surfaced as castable buffs, and pets outside `ActualGroup` (like the Hag) are invisible entirely.

## Goal

All pet/companion units that belong to party members should be treated as first-class participants in the buff system — both as casters (their spells/abilities are scanned) and as targets (they appear in the portrait row and can receive buffs).

## Design

### Approach: Expand `Bubble.Group` to include pets

The mod uses `Bubble.Group` as the single source of truth for which units participate in the buff system. All scanning, targeting, portrait rendering, and save/load flows derive from this list. By expanding it to include pets, every downstream system picks them up automatically.

### Core Change: `Bubble.RefreshGroup()`

**Current state:** `Bubble.Group` is a computed property returning `Game.Instance.SelectionCharacter.ActualGroup` on every access. Two redundant copies exist in `BubbleBuffSpellbookController.Group` (line 457) and `GlobalBubbleBuffer.Group` (line 2495).

**New state:** `Bubble.Group` becomes a cached `List<UnitEntityData>` field. A new `RefreshGroup()` method builds the list:

1. Clear `GroupById`
2. Copy `ActualGroup` into a new list
3. For each unit in `ActualGroup`, iterate `unit.Pets`
4. For each pet: skip if null or not in game (`pet == null || !pet.IsInGame`), skip if already in list, otherwise append
5. Sort appended pets by UniqueId within each owner for stable ordering (prevents `GroupIsDirty` false positives)
6. Store the result in `Bubble.Group`

The two redundant `Group` properties in `BubbleBuffSpellbookController` and `GlobalBubbleBuffer` are removed. All call sites use `Bubble.Group` instead.

### Refresh Trigger

`Bubble.RefreshGroup()` is called at the start of `BufferState.Recalculate()`, before the `GroupIsDirty` check. This ensures the cached list is current whenever the mod recalculates. The existing `GroupIsDirty` logic (which compares UniqueIds against the last known group) will detect pet additions/removals as group changes and trigger a full `RecalculateAvailableBuffs`.

### UI: Portrait Rebuild on Group Size Change

**Problem:** The portrait array `view.targets` is allocated once in `MakeGroupHolder()` at `CreateWindow()` time with `new Portrait[Group.Count]`. If the group size changes after window creation (pets joining/leaving), `PreviewReceivers` (line 2956) and `UpdateTargetBuffColor` (line 2961) iterate `Bubble.Group.Count` but index into the fixed-size `targets[]` array → `IndexOutOfBoundsException`. Similarly, `UpdateCasterDetails` (line 3010) uses `targets[who.CharacterIndex]` for sprite lookup, which will be out of bounds for pets added after window creation.

**Fix:** In `ShowBuffWindow()`, before showing the window, check if `Bubble.Group.Count != view.targets.Length`. If they differ, destroy `Root`, set `WindowCreated = false`, and let the method fall through to `CreateWindow()`. This rebuilds the entire window with the correct portrait count. This is safe because:

- `CreateWindow()` is already idempotent (creates fresh UI from prefabs)
- The window is only shown when opening the spellbook screen, so rebuild latency is invisible
- Save state is preserved in `BufferState` independently of the UI

### `unit.Pets` API

The game API `UnitEntityData.Pets` needs to be verified via ilspycmd before implementation. Expected to return a collection of pet entities. Implementation must handle:

- Null entries or disposed units (guard with `pet != null && pet.IsInGame`)
- All companion types: animal companions, Aivu, Hag, familiars, eidolons

If `Pets` is not available or has a different shape, the fallback is iterating `Game.Instance.Player.PartyAndPets` and filtering for units whose master is in `ActualGroup`.

### Scroll/Wand/Potion Behavior for Pets

The inventory scan (BufferState.cs lines 189-291) creates providers for ALL group members. With pets in the group:

- **Scrolls/Wands**: `CanUseItemWithUmd()` checks class spell lists and UMD skill. Pets typically have no UMD ranks → they'll fail the check and be skipped. No bug, just no scroll/wand access for most pets. This is correct behavior.
- **Potions**: Created for all group members with `creditClamp: 1` (self-only). Pets will get potion providers. The game's `CanTarget()` determines if a potion is valid for a pet. This is also correct.
- **QuickSlots**: `dude.Body.QuickSlots` iteration for pets needs verification — pet bodies may not have QuickSlots. Add a null check: `if (dude.Body.QuickSlots == null) continue`.

### Save/Load Compatibility

No changes needed. The save system uses `UniqueId` strings for buff targets (`SavedBuffState.Wanted`), caster priority (`CasterPriority`), and caster state (`CasterKey`). Pet units have stable UniqueIds across save/load. When a pet is removed (e.g., mythic path change), its orphaned IDs are harmlessly skipped during restore — same behavior as when a regular companion is dismissed.

### Casting Execution

No changes needed. `CastTask` and `EngineCastingHandler` operate on `UnitEntityData` — the game API treats pets and party members identically for spell casting. Pet-specific behaviors are handled automatically:

- **Personal spells** (TargetAnchor.Owner): `BuffProvider.SelfCastOnly` = true, `CanTarget()` restricts to the pet's own UniqueId
- **At-will abilities** (no resource): Already receive 500 credits in `RecalculateAvailableBuffs` (BufferState.cs line 147)
- **Arcanist/PowerfulChange/ShareTransmutation**: Check caster-specific facts via `AbilityCache.CasterCache`. Pets without these features skip the checks naturally.

### Settings

No new toggle. Pets are always included when present. This matches the user's expectation that pets are normal party participants.

## Files Changed

| File | Change |
|------|--------|
| `BubbleBuffer.cs` (Bubble class, ~3088) | `Group` from property to cached field, add `RefreshGroup()` with null checks and stable ordering |
| `BubbleBuffer.cs` (~457) | Remove `BubbleBuffSpellbookController.Group`, replace usages with `Bubble.Group` |
| `BubbleBuffer.cs` (~2495) | Remove `GlobalBubbleBuffer.Group`, replace usages with `Bubble.Group` |
| `BubbleBuffer.cs` (ShowBuffWindow, ~1851) | Add group-size-change detection, trigger window rebuild |
| `BufferState.cs` (~376) | Call `Bubble.RefreshGroup()` at start of `Recalculate()` |
| `BufferState.cs` (QuickSlots scan, ~298) | Add null check for `dude.Body.QuickSlots` |

## What Does NOT Change

- `BufferState.RecalculateAvailableBuffs()` — already iterates over the `Group` parameter
- `AddBuff()` filter logic — no pet-specific filtering needed
- Credit system — at-will abilities already handled
- Save/Load — UniqueId-based, pet IDs work transparently
- `CastTask` / `EngineCastingHandler` — unit-agnostic
- Localization — no new UI strings

## Estimated Scope

~80-120 lines changed across 2 files. No new files. The portrait rebuild detection is the main addition beyond the original estimate.
