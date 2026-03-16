# Caster Deduplication & Source Filtering

## Summary

Fix duplicate caster portraits and filter out self-only source providers from caster display. Characters should appear once in the caster portrait row regardless of how many sources they have for a spell. Potion and self-only scroll providers should not make a character appear as a caster.

## Current State

- `AddProvider()` in `BubbleBuff.cs:152-158` deduplicates by `who + book.Blueprint.AssetGuid + SourceType`. A character with the same spell in two spellbooks (e.g. merged mythic) gets two CasterQueue entries → two portraits.
- A character with the same spell as both Spell and Scroll gets two entries (different SourceType) → two portraits.
- Potion providers are added for ALL characters (no `CanUseItemWithUmd` check). Every character appears as a caster even though potions are self-only.
- `SelfCastOnly` field already exists on `BuffProvider`/`CastTask` — currently used for spells with `TargetAnchor.Owner`.

## Design

### 1. Portrait Dedup (UI)

Caster portraits in `BubbleBuffer.cs` are grouped by `who` (UnitEntityData reference). One character = one portrait, regardless of how many CasterQueue entries they have. The portrait rendering iterates distinct characters from the CasterQueue, not raw CasterQueue indices.

Where CasterQueue index is used to identify the "selected caster" in the UI, this changes to identifying the selected character. When a character is selected, all their CasterQueue entries are relevant for execution.

### 2. Self-Only Source Marking

When adding providers in `BufferState.AddBuff()`:

- **Potion providers**: Always `SelfCastOnly = true` (potions are inherently self-use)
- **Scroll providers**: `SelfCastOnly = true` when the spell blueprint is self-only (`CanTargetFriends == false && Range == AbilityRange.Personal`, or `TargetAnchor == Owner`)
- **Spellbook providers**: Never `SelfCastOnly` due to source type — even self-only spells are valid caster entries (the character actively casts)
- **Equipment providers**: Same as scroll logic — self-only if the spell is self-only

### 3. Caster Portrait Filtering

A character appears as a caster portrait only if they have at least one provider where `SelfCastOnly == false`. Characters with only self-only providers (only potions, only self-scrolls) do NOT get a portrait — they can still receive the buff as a target (the self-only provider is consumed during Validate/Execute when the character is in the target list).

### 4. CasterQueue Internals Unchanged

The CasterQueue data structure keeps multiple entries per character (different sources have different credit pools, spellbooks, etc.). This is required for:
- Source priority / toggle system ("Use Spells", "Use Scrolls", etc.)
- Fallback when primary source exhausted (spell slots empty → fall back to scroll)
- Credit tracking per source

Validate() and Execute() iterate all providers as before. The only change is which characters get portraits and how portrait selection maps to CasterQueue entries.

### 5. Self-Only Provider Execution

During Validate()/Execute(), `SelfCastOnly` providers can only target their own character. This is partially implemented already — the change ensures potion and self-only-scroll providers consistently get this flag.

## Files Changed

| File | Change |
|---|---|
| `BufferState.cs:214-234` | Set `SelfCastOnly = true` on potion providers in `AddBuff()` call |
| `BufferState.cs:235-259` | Set `SelfCastOnly = true` on scroll providers when spell is self-only |
| `BubbleBuff.cs:152-158` | No change to dedup logic — multiple entries per char are kept |
| `BubbleBuffer.cs` (caster portraits) | Group CasterQueue by `who`, show one portrait per distinct character |
| `BubbleBuffer.cs` (caster selection) | Selected caster identifies a character, not a CasterQueue index |
| `BubbleBuffer.cs` (portrait filtering) | Skip characters whose all providers are `SelfCastOnly` |
