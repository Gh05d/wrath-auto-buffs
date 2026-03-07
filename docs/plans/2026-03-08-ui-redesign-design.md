# BubbleBuffs UI Redesign & Equipment Support

## Summary

Redesign the BubbleBuffs UI to merge Spells and Consumables into a unified "Buffs" tab, add inline source-type controls in the caster box, rename Items to "Equipment" with activatable item support, add tab icons from game blueprints, and rename buff group labels.

## Tab Structure

### Before -> After

| Before | After |
|--------|-------|
| Spells | **Buffs** (Spells + Scrolls + Potions merged) |
| Abilities | **Abilities** (unchanged) |
| Items | **Equipment** (activatable quickslot items) |
| Consumables | **removed** (merged into Buffs) |

### Category Enum

```csharp
enum Category {
    Buff,       // was: Spell + Consumable
    Ability,
    Equipment,  // was: Item
}
```

- Spells, Scrolls, and Potions all get `Category.Buff`
- A buff that exists as both Spell and Scroll appears once; its CasterQueue contains providers from all source types
- Checkboxes for source types are always visible if a Blueprint exists for that source type, regardless of current inventory

### Tab Icons

Each tab button gets a small game-blueprint icon (approx 24x24) to the left of the text label. Icons are loaded via `BlueprintAbility.Icon` or `BlueprintItem.Icon` from known blueprint GUIDs:

- **Buffs:** Icon from a well-known buff spell (e.g. Mage Armor)
- **Equipment:** Icon from a well-known staff or wand
- **Abilities:** Icon from a well-known ability (e.g. Smite Evil)

## Caster Box Inline Controls

### Source Controls Row

A compact control row directly above the caster portraits replaces the toggles currently hidden in the spell popout:

```
[x Spells] [x Scrolls] [x Potions]  Prio: Spells > Scrolls > Potions [>]
```

- **Three checkboxes** enabling/disabling source types for this buff
- Always visible if a scroll/potion blueprint exists for the buff (even if none in inventory)
- If enabled but nothing in inventory at cast time: log message
- **Priority cycle button** `[>]` cycles through 6 orderings + "Global Default"
- Current priority displayed as text next to the button
- These controls move OUT of the spell popout; the popout retains only "Ignore effects when checking overwrite" toggles

### Caster Portrait Source Overlay

Each caster portrait gets a small icon overlay (approx 16x16) in the bottom-right corner:

- **Spell:** no overlay (default, needs no marker)
- **Scroll:** small scroll blueprint icon
- **Potion:** small potion blueprint icon
- **Equipment:** small equipment blueprint icon

This replaces the current `[Scroll]` / `[Potion]` text labels under the portrait.

## Labels

### Buff Group Names

| Before | After |
|--------|-------|
| "Normal" | "Normal Buffs" |
| "Short" | "Quick Buffs" |
| "Important" | "Important Buffs" |

Applies to:
- Summary cards at top of UI
- Group assignment buttons in detail view
- Log messages when casting

### Log Messages

| Before | After |
|--------|-------|
| "Buffed Normal!" | "Normal Buffs!" |
| "Buffed Short!" | "Quick Buffs!" |
| "Buffed Important!" | "Important Buffs!" |

### New Log Messages

- `"No [Scroll/Potion] of [Buff Name] in inventory"` — source enabled but item not available
- `"Last [Scroll/Potion] of [Buff Name] used"` — last item consumed (already exists)
- `"[Item Name] is out of charges"` — equipment item has no charges left
- `"[Item Name] no longer available"` — equipment item removed/unequipped

## Equipment Feature

### New Source Type

```csharp
enum BuffSourceType { Spell, Scroll, Potion, Equipment }
```

### Scope

**Phase 1:** Activatable items in character quickslots (staves, wands, similar items with `BlueprintItemEquipmentUsable` that have buff effects).

**Phase 2 (future, optional):** Equipment with activatable special abilities (armor abilities, ring abilities, etc.).

### Inventory Scanning

- Scan quickslots of all party members for activatable items
- Check if item ability has known buff effects via `GetBeneficialBuffs()`
- Create BuffProviders with `Category.Equipment` and `SourceType = Equipment`
- Caster = the character who has the item in their quickslot

### Charge Handling

| Type | Example | Behavior |
|------|---------|----------|
| Permanent charges | Wand of Mage Armor (50 charges) | Credits = remaining charges. Item destroyed after last charge. |
| Daily charges | Staff of Power (X/day) | Credits = remaining daily charges. Restored on rest. |

- Read charges from `ItemEntity` charge properties (exact API to be verified during implementation)
- After cast: game handles charge deduction automatically
- Display in portrait text: `2/5 daily` for daily, `47/50` for permanent
- Log when item depleted or no longer available

### Feature Interactions

- Azata Zippy Magic, Share Transmutation, Powerful Change: disabled for Equipment providers (same as Scroll/Potion)
- No UMD check needed for equipment — if the item is in a character's quickslot, they can use it

## Edge Cases

- **Same buff from spell + scroll + potion:** Single entry in Buffs tab, CasterQueue has all providers sorted by priority
- **Source enabled but no inventory:** Log warning, skip to next provider in queue
- **Items exhausted mid-cycle:** Next provider in queue takes over
- **Equipment unequipped between buff cycles:** Detected at cast time, log warning, skip
- **Checkbox always visible:** As long as a blueprint exists for the source type, checkbox is shown even without inventory
