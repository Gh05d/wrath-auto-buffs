# Extend Rod Support — Design Spec

**Date:** 2026-03-15
**Status:** Approved

## Summary

Add support for automatically applying Extend Spell metamagic rods when casting buffs. Users enable a per-buff toggle; the mod finds the weakest suitable rod in the shared party inventory, applies the Extend metamagic to the spell, and consumes a rod charge. If no rod is available, the spell is cast normally with a log message.

## Requirements

- Per-buff checkbox to toggle "Use Extend Rod"
- Automatic rod tier selection: Lesser (≤3) → Normal (≤6) → Greater (≤9), weakest first
- Rods sourced from shared party inventory (not QuickSlot-restricted)
- Graceful fallback: no rod available → cast normally + log warning
- Logging: verbose log on success, standard log on fallback, tooltip suffix `[Extend]`
- Extensible design for future metamagic rod types (Empower, Maximize, etc.)

## Design

### Data Model

**`SavedBuffState`** — new persisted field:

```csharp
[JsonProperty]
public bool UseExtendRod;
```

Per-buff, not per-caster — rods come from the shared inventory and are not bound to a specific caster.

**`BubbleBuff`** — corresponding runtime field (following existing pattern for `UseSpells`, `UseScrolls`, etc.):

```csharp
public bool UseExtendRod;
```

- Set in `BubbleBuff.InitialiseFromSave()`: `UseExtendRod = state.UseExtendRod`
- Saved in `BufferState.Save()`: `save.UseExtendRod = buff.UseExtendRod`

**No `MetamagicRodType` enum in this iteration.** The bool is sufficient for Extend-only scope. If future rod types are added, a migration to an enum or flags field will be done at that point. Avoids dead code now.

### Rod Discovery & Selection

New method in `BufferState` (or dedicated helper): `FindBestExtendRod(int spellLevel, Dictionary<ItemEntity, int> remainingCharges)`

**Game architecture for rods:**
- Rods are `BlueprintItemEquipmentUsable` with `Type = Other`
- The rod item has an `m_ActivatableAbility` field
- The activatable ability's buff contains the `MetamagicRodMechanics` component, which has `Metamagic` and `MaxSpellLevel` fields
- The rod component is NOT on the item blueprint itself

**Discovery approach — GUID-based lookup:**

Known Extend Rod blueprint GUIDs (extracted from game data):
- Lesser Extend Rod: `1cf04842d5dbd0f49946b1af1022cd1a` (MaxSpellLevel 3)
- Normal Extend Rod: `1b2a09528da9e9948aa9026037bada90` (MaxSpellLevel 6)
- Greater Extend Rod: GUID to be extracted during implementation from `Items/Rods/MetamagicRodExtendGreater`

Static lookup table mapping GUID → MaxSpellLevel. This is simpler and more reliable than traversing `item → m_ActivatableAbility → buff → MetamagicRodMechanics` at runtime.

**Selection logic:**

1. Scan `Game.Instance.Player.Inventory` for items matching known Extend Rod GUIDs
2. Filter by `MaxSpellLevel >= spellLevel` (the caster's base spell level for this specific spell, not a global value)
3. Filter by remaining charges > 0 (from `remainingCharges` dict, initialized from `item.Charges`)
4. Sort ascending by `MaxSpellLevel` — pick the weakest rod that fits
5. Return the item, or `null` if none available

**Charge tracking:** `BuffExecutor` maintains a local `Dictionary<ItemEntity, int> remainingRodCharges` (analogous to the existing `remainingArcanistPool`). Initialized from `ItemEntity.Charges` (runtime value, not blueprint default). Decremented per cast within the `Execute()` pass.

**Spell level:** Uses the base spell level from the specific caster's spellbook (via `caster.spell.Spellbook.GetSpellLevel(caster.spell)`), without metamagic cost increase. Different casters may know the same spell at different levels.

### CastTask Extension

New field on `CastTask`:

```csharp
public Kingmaker.Items.ItemEntity MetamagicRodItem; // null = no rod
```

### BuffExecutor.Execute() Changes

When creating a `CastTask`:

1. Check if `buff.UseExtendRod == true`
2. If yes, determine the spell level for this specific caster
3. Call `FindBestExtendRod(spellLevel, remainingRodCharges)`
4. If rod found: set `task.MetamagicRodItem`, decrement `remainingRodCharges`
5. If no rod: continue with normal cast, log warning

### EngineCastingHandler Changes

**Constructor** (before actual cast):
- If `_castTask.MetamagicRodItem != null`, apply Extend metamagic to `_castTask.SpellToCast`:
  - If `SpellToCast.MetamagicData` is null: create a new `MetamagicData(SpellToCast.Blueprint, null)` and set `MetamagicMask = Metamagic.Extend`
  - If `SpellToCast.MetamagicData` is not null: OR in `Metamagic.Extend` to the existing mask (e.g., for an already-Empowered spell)
  - The rod-applied Extend must NOT increase the spell level cost (rods bypass the level increase)

**`HandleExecutionProcessEnd()`:**
- Consume rod charge via `_castTask.MetamagicRodItem.Charges--` (analogous to existing equipment charge consumption at line 126-127, within its own try/catch)
- Restore original `MetamagicData` state if it was modified (to avoid persisting the change on the `AbilityData`)

Direct MetamagicData modification is more reliable than activating the rod's ability and relying on the game's internal buff timing — especially in `InstantExecutionEngine` where no frame delay is possible.

**Implementation verification needed:** Confirm exact `MetamagicData` constructor signature and whether modifying it on `AbilityData` is safe to do temporarily (set before cast, restore after). First build test will resolve this.

### UI

Toggle placed in the **left side of the Source Controls Section** (`prioSideObj`), below the Source Priority text.

```
Source Controls Section
├── Left (55%): VLG (restructured from anchor-based to layout-driven)
│     Source Priority: Spells > Scrolls > Potions (clickable)
│     ☐ Use Extend Rod   ← NEW
└── Right (45%): VLG with toggles
      ☑ Use Spells
      ☑ Use Scrolls
      ☑ Use Potions
      ☑ Use Equipment
```

**Restructure details:** The left side (`prioSideObj`) currently has a single anchor-positioned `prioLabelObj` child (anchors 0,0→1,1). This must be converted to LayoutElement-driven sizing:
1. Add a VLG to `prioSideObj` with `childControlHeight=true`, `childForceExpandHeight=false`
2. Convert `prioLabelObj` from anchor-based to LayoutElement-based (remove stretch anchors, add `LayoutElement` with `preferredHeight`)
3. Add the Extend Rod toggle below using the `MakeSourceToggle` pattern (0.7f scale)
4. Per CLAUDE.md guideline: "prefer one approach per container" — do not mix anchors and LayoutGroups within `prioSideObj`

**Behavior:**
- State read from `buff.UseExtendRod`, saved on change via `buff.UseExtendRod = val; buff.SavedState.UseExtendRod = val; state.Save()`
- Always visible when a buff is selected (like other source toggles)
- `UpdateDetailsView` sets toggle value on buff change

**Localization:** New key `"use.extendrod"` in all 5 locale files (`en_GB`, `de_DE`, `fr_FR`, `ru_RU`, `zh_CN`).

### Logging

| Situation | Method | Message |
|---|---|---|
| Rod assigned to cast | `Main.Verbose` | `"Extend Rod applied: {rodItem.Name} for {buff.Name}"` |
| No rod available, fallback | `Main.Log` | `"Extend Rod unavailable for {buff.Name}, casting normally"` |
| Rod charge consumption error | `Main.Error` | Exception details |
| Tooltip (CombatLog) | `BuffResult` | New `bool ExtendRodUsed` field on `BuffResult`, rendered as `[Extend]` suffix |

### Save/Load Integration

Follow the existing pattern for source toggles (`UseSpells`, `UseScrolls`, `UsePotions`, `UseEquipment`):

1. **`BubbleBuff.UseExtendRod`** — runtime field, default `false`
2. **`BubbleBuff.InitialiseFromSave(SavedBuffState state)`** — add `UseExtendRod = state.UseExtendRod`
3. **`BufferState.Save()`** — add `save.UseExtendRod = buff.UseExtendRod` in the buff serialization loop
4. **`SavedBuffState.UseExtendRod`** — persisted via JSON, defaults to `false` for existing saves (Newtonsoft default)

## Out of Scope

- Other metamagic rod types (Empower, Maximize, etc.) — deferred to a future iteration
- Rod availability indicator in the UI — checked only at cast time
- QuickSlot restriction — rods from shared inventory, not QuickSlot-bound
- Dynamic rod discovery via `MetamagicRodMechanics` traversal — using GUID lookup instead
