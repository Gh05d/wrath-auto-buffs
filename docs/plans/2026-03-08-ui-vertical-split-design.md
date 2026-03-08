# UI Redesign: Vertical Split Layout

## Problem

The detail panel (detailsRect) occupies only 25% height x 50% width of the buff window. Source controls, caster portraits, target portraits, buff group selector, and action buttons are all crammed into this small area, causing overlaps. Target portraits are oversized (166px) for no good reason.

## Design

### Layout Structure

```
+-----------------------------------------------------+
|  Summary Bar (Normal 68/72 | Quick 24/24 | Imp 0/0) |
+----------------------+------------------------------+
|                      |  [Icon] Mage Armor            |
|  Buff Grid           |  Abjuration                   |
|  2-3 columns         |  [ ] Hide                     |
|  full height         |-------------------------------|
|  scrollable          |  [x] Spells  [x] Scrolls      |
|                      |  [x] Potions [x] Equipment    |
|  [Search...]         |  Priority: Global Default [>]  |
|  [ ] Show hidden     |-------------------------------|
|  [x] Show requested  |  Casters: [P1][P2][P3]...     |
|  ...                 |-------------------------------|
|                      |  Targets: [T1][T2][T3]...      |
|  [Buffs][Abi][Equip] |  [+][-]  [N][I][Q]            |
+----------------------+------------------------------+
```

### Anchor Layout (within Root)

| Component | anchorMin | anchorMax | Notes |
|---|---|---|---|
| Summary bar | (0.05, 0.88) | (0.95, 0.95) | Top strip, unchanged |
| Left panel | (0.05, 0.05) | (0.38, 0.87) | Buff grid + filters + tabs |
| Right panel | (0.40, 0.05) | (0.95, 0.87) | All detail/config content |

### Left Panel Contents

- **Buff Grid** (top ~75%): 2-3 column grid with scroll, scaled 0.75x
- **Search bar**: Below grid
- **Filter toggles**: Below search (Show hidden, Show short, Show requested, Show NOT requested, Sort by name)
- **Category tabs** (Buffs/Abilities/Equipment): Bottom of left panel

### Right Panel Contents (top to bottom)

1. **Spell info** (~15%): Icon + name + school + hide toggle
2. **Source controls** (~20%): 2x2 toggle grid + priority row
3. **Caster portraits** (~25%): Horizontal row, ~70px height, with expand buttons
4. **Target portraits** (~25%): Horizontal row, ~60px height
5. **Action bar** (~15%): Add/Remove all icons + Buff Group icons (Normal/Important/Quick)

### Size Reductions

- **Target portraits**: 166px -> 60px height (with proportional width)
- **Caster portraits**: 90px -> 70px height
- **Buff Group selector**: Text buttons -> icon-only buttons with tooltip on hover
- **Add/Remove all**: Text buttons -> icon buttons (+/-) with tooltip on hover

### Unchanged

- Summary bar position and content
- Caster popout (overlay, triggered by expand button)
- Spell effects popout (overlay, triggered by expand button)
- Settings panel (HUD button popup)
- Source control toggle behavior and priority cycling
