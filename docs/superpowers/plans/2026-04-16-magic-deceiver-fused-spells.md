# Magic Deceiver Fused Spell Support — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Magic Deceiver fused spells (Magic Fusion / MagicHack) appear in the mod's buff list when at least one component spell has beneficial effects.

**Architecture:** Two surgical changes. (1) In `AddBuff()`, detect fused spells via `AbilityData.MagicHackData` and check component spell blueprints for beneficial effects instead of the empty template blueprint. (2) In `BubbleBuff.Icon`, use `AbilityData.Icon` for fused spells since the template blueprint has no icon.

**Tech Stack:** C# / .NET Framework 4.8.1, Unity, Harmony, WotR game engine APIs

**Spec:** `docs/superpowers/specs/2026-04-16-magic-deceiver-fused-spells-design.md`

---

## File Map

| File | Responsibility | Change |
|---|---|---|
| `BuffIt2TheLimit/BufferState.cs` | Buff scanning + filtering | MagicHackData bypass in `AddBuff()` before `GetBeneficialBuffs()` |
| `BuffIt2TheLimit/BubbleBuff.cs` | Buff data model + display | Icon property: route fused spells through `AbilityData.Icon` |

---

### Task 1: MagicHackData bypass in AddBuff()

**Files:**
- Modify: `BuffIt2TheLimit/BufferState.cs:750-754`

The current code at line 750-754 caches and checks beneficial effects using only `spell.Blueprint`. For fused spells, the blueprint is a template shell with empty actions. We need to detect `spell.MagicHackData != null` and check the component spells instead.

- [ ] **Step 1: Add MagicHackData check before GetBeneficialBuffs cache lookup**

In `BuffIt2TheLimit/BufferState.cs`, replace lines 750-754:

```csharp
                if (!SpellsWithBeneficialBuffs.TryGetValue(spell.Blueprint.AssetGuid.m_Guid, out var abilityEffect)) {
                    var beneficial = spell.Blueprint.GetBeneficialBuffs(skipDamageFilter: isAbilityCategory);
                    abilityEffect = new AbilityCombinedEffects(beneficial);
                    SpellsWithBeneficialBuffs[spell.Blueprint.AssetGuid.m_Guid] = abilityEffect;
                    SpellNames[spell.Blueprint.AssetGuid.m_Guid] = spell.Name;
                }
```

with:

```csharp
                if (!SpellsWithBeneficialBuffs.TryGetValue(spell.Blueprint.AssetGuid.m_Guid, out var abilityEffect)) {
                    IEnumerable<IBeneficialEffect> beneficial;
                    if (spell.MagicHackData != null) {
                        // Fused spells (Magic Deceiver): template blueprint has empty actions.
                        // Check component spells instead.
                        var spell1Effects = spell.MagicHackData.Spell1?.GetBeneficialBuffs(skipDamageFilter: isAbilityCategory)
                            ?? Enumerable.Empty<IBeneficialEffect>();
                        var spell2Effects = spell.MagicHackData.Spell2?.GetBeneficialBuffs(skipDamageFilter: isAbilityCategory)
                            ?? Enumerable.Empty<IBeneficialEffect>();
                        beneficial = spell1Effects.Concat(spell2Effects);
                        Main.Verbose($"        Fused spell {spell.Name}: checking components {spell.MagicHackData.Spell1?.Name} + {spell.MagicHackData.Spell2?.Name}", "state");
                    } else {
                        beneficial = spell.Blueprint.GetBeneficialBuffs(skipDamageFilter: isAbilityCategory);
                    }
                    abilityEffect = new AbilityCombinedEffects(beneficial);
                    SpellsWithBeneficialBuffs[spell.Blueprint.AssetGuid.m_Guid] = abilityEffect;
                    SpellNames[spell.Blueprint.AssetGuid.m_Guid] = spell.Name;
                }
```

Key details:
- `GetBeneficialBuffs` is an extension method on `BlueprintAbility` (defined in `ExtentionMethods.cs:262`). `MagicHackData.Spell1` and `Spell2` are `BlueprintAbility` instances, so this call works directly.
- Null-guard `Spell1`/`Spell2` with `?.` — defensive against partially configured fusions.
- The `SpellsWithBeneficialBuffs` cache key is the template blueprint GUID. This is safe because all fused spells in the same slot share the same template, and the CLAUDE.md notes the cache key is GUID-only. If the player reconfigures the fusion, the cache is stale but gets rebuilt on the next `RecalculateAvailableBuffs()` call (which clears `BuffsByKey` — though not the `SpellsWithBeneficialBuffs` static cache).

- [ ] **Step 2: Build and verify no compile errors**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` (plus the harmless `findstr` warning on Linux).

If `MagicHackData` causes CS0122 (inaccessible), check that `Assembly-CSharp.dll` has `Publicize="true"` in the csproj. It should — the mod already accesses many private fields.

- [ ] **Step 3: Commit**

```bash
git add BuffIt2TheLimit/BufferState.cs
git commit -m "feat: support Magic Deceiver fused spells in buff scanning

Check MagicHackData component spells for beneficial effects instead of
the empty template blueprint. Fused spells now appear in the buff list
when at least one component spell has beneficial effects."
```

---

### Task 2: Icon fix for fused spells

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuff.cs:123`

The current `Icon` property accesses `Spell?.Blueprint?.Icon` which returns null for fused spells (template blueprint has `m_Icon: null`). `AbilityData.Icon` (the virtual property) goes through `GetDeliverBlueprint().Icon` which returns the component spell's icon for MagicHack spells.

- [ ] **Step 1: Update Icon property**

In `BuffIt2TheLimit/BubbleBuff.cs`, replace line 123:

```csharp
        public Sprite Icon => IsActivatable ? ActivatableSource.Blueprint.Icon : Spell?.Blueprint?.Icon;
```

with:

```csharp
        public Sprite Icon => IsActivatable ? ActivatableSource.Blueprint.Icon
            : (Spell?.MagicHackData != null ? Spell.Icon : Spell?.Blueprint?.Icon);
```

This only changes behavior when `MagicHackData != null`. For all other spells, the existing `Spell?.Blueprint?.Icon` path is preserved.

- [ ] **Step 2: Build and verify**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuff.cs
git commit -m "fix: show correct icon for Magic Deceiver fused spells

Use AbilityData.Icon for fused spells, which routes through
GetDeliverBlueprint() to the component spell's icon instead of
the template blueprint's null icon."
```

---

### Task 3: Deploy and test on Steam Deck

**Files:** None (testing only)

This requires a Magic Deceiver character with configured fused buff spells.

- [ ] **Step 1: Deploy to Steam Deck**

```bash
./deploy.sh
```

Verify timestamps match:
```bash
ls -la BuffIt2TheLimit/bin/Debug/BuffIt2TheLimit.dll
ssh deck-direct "ls -la '/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/BuffIt2TheLimit/BuffIt2TheLimit.dll'"
```

- [ ] **Step 2: Test with Magic Deceiver character**

Create or load a Magic Deceiver character. Configure fused spells that include at least one buff spell (e.g., Haste + any other spell). Open the spellbook UI and check the mod's buff window.

Verify:
1. Fused buff spells appear in the buff list
2. Name displays correctly (user-given name or auto-generated)
3. Icon displays (not blank/missing)
4. Can assign party member targets
5. Casting executes without crash

- [ ] **Step 3: Verify non-fused spells unaffected**

Switch to a non-Magic-Deceiver character and verify:
1. Regular buffs still appear and work
2. No new errors in Player.log

Check logs for the fused spell verbose output:
```bash
ssh deck-direct "grep 'Fused spell' '/home/deck/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Player.log'"
```
