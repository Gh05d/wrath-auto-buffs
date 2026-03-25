# Song/Performance Support Design

## Overview

Add support for Bard/Skald performances and Azata songs to Buff It 2 The Limit. Songs are `ActivatableAbility` toggles — a different mechanism than spell casting. The mod will activate them as part of buff routines, with a dedicated UI tab and resource checking.

## Requirements

- **Scope:** Bard/Skald performances (Inspire Courage, Inspire Competence, Inspire Heroics, Inspire Greatness, Dirge of Doom, Frightening Tune) + Azata songs (Song of Heroic Resolve, Song of Broken Chains, Song of Defiance, Song of the Second Breath)
- **Behavior:** Activate only — songs stay on until the player manually deactivates them
- **Resource check:** Verify remaining rounds before activating; skip if none available
- **UI:** Dedicated "Songs" tab, separate from Buffs/Abilities/Equipment
- **BuffGroup assignment:** Songs are assignable to Long/Important/Quick like normal buffs

## Design

### 1. Scanning

New phase in `BufferState.RecalculateAvailableBuffs()` after the existing three phases (Spellbooks, Abilities, Items):

**Phase 4: Activatable Performances**
- Iterate `dude.ActivatableAbilities.Enumerable` (or `.RawFacts`)
- Filter on Bard/Skald performances and Azata songs via `BlueprintActivatableAbility.Group` property (e.g., `ActivatableAbilityGroup.BardicPerformance`) or known feature blueprints
- For each match: create a `BubbleBuff` entry with `Category.Song`, `BuffSourceType.Song`
- Credits = remaining rounds from the performance resource (`AbilityResourceLogic` on the blueprint, queried via `AbilityResource.GetAmount()`)
- `SelfCastOnly = true` — performances activate on the caster, effect is party-wide
- No merging with existing spell entries (songs are never simultaneously available as spells)

### 2. Data Model

**New enum values:**
- `Category.Song` — after `Equipment`
- `BuffSourceType.Song` — after `Equipment`

**BubbleBuff extensions:**
- `ActivatableAbilityData ActivatableSource` — reference to the activatable ability (analogous to `AbilityData SpellToCast` for spells)
- `IsSong` — computed property, `true` when `SourceType == Song`
- Songs always have exactly one caster (no CasterQueue with fallbacks)

**SaveState extensions:**
- `SavedBufferState`: new toggle `SongsEnabled` (default `true`)
- `SavedBuffState`: new toggle `UseSongs` (analogous to `UseSpells`, `UseScrolls`, etc.)
- No new caster-specific fields needed — songs have only one possible caster

**BuffProvider:**
- New provider type for songs, credits based on `AbilityResource.GetAmount()` (remaining rounds)

### 3. Execution

New path in `BuffExecutor.Execute()`:
- When `buff.IsSong`: instead of creating a `CastTask`, call `ActivatableAbilityData.TurnOn()` (or the game API equivalent)
- Pre-checks: (a) not already active (`IsOn`/`IsRunning`), (b) rounds available
- No `EngineCastingHandler` hook needed — songs don't use metamagic, share transmutation, etc.
- No `AnimatedExecutionEngine`/`InstantExecutionEngine` — songs use their own activation path
- Order: activate songs before normal buffs (so performance buffs are active when spells are cast)

### 4. UI

- New "Songs" tab in the tab bar (alongside Buffs/Abilities/Equipment)
- Per-song display: name, caster portrait, remaining rounds, active/inactive status
- BuffGroup assignment like normal buffs (Long/Important/Quick checkboxes)
- No target selection — songs are always `SelfCastOnly`, effect is automatically party-wide
- Portrait area shows only the single possible caster (no multi-caster dropdown)

### 5. Localization

- New keys in all locale files: tab name ("Songs"), `SongsEnabled` toggle label, tooltip "Remaining rounds: {0}"
- `en_GB` and `de_DE` complete, other locales best-effort

## Architecture Boundaries

- Song scanning is a self-contained phase in `BufferState` — no changes to existing spell/item scanning
- Song execution is a separate branch in `BuffExecutor` — no changes to `CastTask`, `EngineCastingHandler`, or execution engines
- Song UI tab follows the same pattern as existing category tabs — no structural UI changes
- Save/load extends existing `SavedBufferState`/`SavedBuffState` with new fields, backward-compatible (defaults for missing fields)
