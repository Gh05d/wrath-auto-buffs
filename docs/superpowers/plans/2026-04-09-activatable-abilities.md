# Activatable Abilities & Expanded Ability Tab — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generalize the Song system to support all resource-based ActivatableAbilities in the Ability tab, and relax the beneficial effect filter so regular abilities like Dimension Strike appear.

**Architecture:** The Song system (scan → AddSong → ValidateSong → Phase 0 Execute) becomes a general ActivatableAbility system. `IsSong` becomes a computed property (`IsActivatable && Category == Song`). A new `IsActivatable` flag drives the toggle/round-limit UI and Phase 0 execution for all activatable types. The `GetBeneficialBuffs()` filter is relaxed for `Category.Ability` to allow self-target abilities through even without detectable buff effects.

**Tech Stack:** C# / .NET Framework 4.8.1, Unity UI, Harmony, Newtonsoft.Json

**Build command:** `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/`

**Deploy command:** `./deploy.sh`

---

### Task 1: Data Model — BubbleBuff fields and BuffSourceType

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuff.cs:145-170`
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:2830-2836`

- [ ] **Step 1: Add `Activatable` to `BuffSourceType` enum**

In `BuffIt2TheLimit/BubbleBuffer.cs`, add the new enum value:

```csharp
// Line 2830-2836, replace enum
public enum BuffSourceType {
    Spell,
    Scroll,
    Potion,
    Equipment,
    Song,
    Activatable
}
```

- [ ] **Step 2: Add `IsActivatable` and `ActivatableGroup` fields, make `IsSong` computed**

In `BuffIt2TheLimit/BubbleBuff.cs`, replace lines 169-170:

```csharp
// Replace:
//   public bool IsSong;
//   public Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbility ActivatableSource;
// With:
public bool IsActivatable;
public bool IsSong => IsActivatable && Category == Category.Song;
public Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbility ActivatableSource;
public Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityGroup ActivatableGroup;
```

- [ ] **Step 3: Update the song constructor to set `IsActivatable` instead of `IsSong`**

In `BuffIt2TheLimit/BubbleBuff.cs`, the constructor at lines 145-154. Replace `this.IsSong = true;` with `this.IsActivatable = true;`:

```csharp
public BubbleBuff(Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbility activatable) {
    this.ActivatableSource = activatable;
    this.Spell = null;
    this.IsActivatable = true;
    var blueprint = activatable.Blueprint;
    this.NameLower = blueprint.Name.ToLower();
    this.Key = new BuffKey(blueprint.AssetGuid);
    this.Category = Category.Song;
    this.BuffsApplied = new AbilityCombinedEffects(Enumerable.Empty<IBeneficialEffect>());
    this.ActivatableGroup = blueprint.Group;
}
```

Note: `Category` is set to `Song` here as default — the caller (`AddActivatable`) will override it for non-song activatables.

- [ ] **Step 4: Update `SelfCastOnly` to handle `BuffSourceType.Activatable`**

In `BuffIt2TheLimit/BubbleBuff.cs`, the `SelfCastOnly` property at lines 598-601:

```csharp
public bool SelfCastOnly =>
    SourceType == BuffSourceType.Song ||
    SourceType == BuffSourceType.Activatable ||
    SourceType == BuffSourceType.Potion ||
    spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Owner;
```

- [ ] **Step 5: Build and verify no compile errors**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds. There will be warnings about `IsSong` being used as a property now instead of a field — these should resolve since all existing code reads `IsSong` (never writes except the deleted `this.IsSong = true`).

- [ ] **Step 6: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuff.cs BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: add IsActivatable field, make IsSong computed, add BuffSourceType.Activatable"
```

---

### Task 2: SavedState — Add `ActivatablesEnabled` setting

**Files:**
- Modify: `BuffIt2TheLimit/SaveState.cs:48`

- [ ] **Step 1: Add `ActivatablesEnabled` field**

In `BuffIt2TheLimit/SaveState.cs`, after line 48 (`SongsEnabled`):

```csharp
        [JsonProperty]
        [System.ComponentModel.DefaultValue(true)]
        public bool SongsEnabled = true;
        [JsonProperty]
        [System.ComponentModel.DefaultValue(true)]
        public bool ActivatablesEnabled = true;
```

- [ ] **Step 2: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add BuffIt2TheLimit/SaveState.cs
git commit -m "feat: add ActivatablesEnabled setting to SavedBufferState"
```

---

### Task 3: Scanning — Generalize `AddSong` to `AddActivatable`

**Files:**
- Modify: `BuffIt2TheLimit/BufferState.cs:351-366,690-720`

- [ ] **Step 1: Rename `SongGroups` to `PerformanceGroups`**

In `BuffIt2TheLimit/BufferState.cs`, lines 690-693:

```csharp
// Replace:
//   private static readonly HashSet<ActivatableAbilityGroup> SongGroups = new() {
// With:
private static readonly HashSet<ActivatableAbilityGroup> PerformanceGroups = new() {
    ActivatableAbilityGroup.BardicPerformance,
    ActivatableAbilityGroup.AzataMythicPerformance
};
```

- [ ] **Step 2: Rename `AddSong` to `AddActivatable` and add `category` parameter**

In `BuffIt2TheLimit/BufferState.cs`, replace the `AddSong` method (lines 695-720):

```csharp
public void AddActivatable(UnitEntityData dude, ActivatableAbility activatable, int charIndex, Category category) {
    var blueprint = activatable.Blueprint;
    var key = new BuffKey(blueprint.AssetGuid);

    if (BuffsByKey.TryGetValue(key, out var existing)) {
        return;
    }

    var buff = new BubbleBuff(activatable);
    buff.Category = category;

    var sourceType = category == Category.Song ? BuffSourceType.Song : BuffSourceType.Activatable;

    var credits = new ReactiveProperty<int>(activatable.ResourceCount ?? 1);
    var provider = new BuffProvider(credits) {
        who = dude,
        spent = 0,
        clamp = 1,
        book = null,
        spell = null,
        baseSpell = null,
        CharacterIndex = charIndex,
        SourceType = sourceType,
        SourceItem = null
    };
    buff.CasterQueue.Add(provider);

    BuffsByKey[key] = buff;
}
```

- [ ] **Step 3: Update the scan loop to handle all activatables**

In `BuffIt2TheLimit/BufferState.cs`, replace the song scan block (lines 350-367):

```csharp
try {
    for (int characterIndex = 0; characterIndex < Group.Count; characterIndex++) {
        UnitEntityData dude = Group[characterIndex];
        foreach (var activatable in dude.ActivatableAbilities.RawFacts) {
            var blueprint = activatable.Blueprint;

            // Skip activatables without resource cost (Power Attack, Wings, etc.)
            bool hasResourceLogic = blueprint.GetComponent<Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityResourceLogic>() != null;

            if (PerformanceGroups.Contains(blueprint.Group)) {
                if (!SavedState.SongsEnabled) continue;
                Main.Verbose($"      Adding song: {blueprint.Name} for {dude.CharacterName}", "state");
                AddActivatable(dude, activatable, characterIndex, Category.Song);
            } else if (hasResourceLogic) {
                if (!SavedState.ActivatablesEnabled) continue;
                Main.Verbose($"      Adding activatable: {blueprint.Name} (group={blueprint.Group}) for {dude.CharacterName}", "state");
                AddActivatable(dude, activatable, characterIndex, Category.Ability);
            }
        }
    }
} catch (Exception ex) {
    Main.Error(ex, "finding activatable abilities");
}
```

- [ ] **Step 4: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds. No references to the old `AddSong` or `SongGroups` should remain.

- [ ] **Step 5: Commit**

```bash
git add BuffIt2TheLimit/BufferState.cs
git commit -m "feat: generalize AddSong to AddActivatable, scan all resource-based activatables"
```

---

### Task 4: Execution — Generalize Phase 0 for all activatables

**Files:**
- Modify: `BuffIt2TheLimit/BuffExecutor.cs:173-209,231,438,441-475,485`

- [ ] **Step 1: Update Phase 0 in `Execute()` to use `IsActivatable` and reverse iteration**

In `BuffIt2TheLimit/BuffExecutor.cs`, replace the Phase 0 block (lines 173-209). The key changes: `b.IsSong` → `b.IsActivatable`, reverse iteration for mutual exclusivity, and updated log messages:

```csharp
            // Phase 0: Activate activatable abilities before casting buffs
            var activatedGroups = new HashSet<(ActivatableAbilityGroup, string)>();
            foreach (var actBuff in State.BuffList.Where(b => b.IsActivatable && b.InGroups.Contains(buffGroup) && b.Fulfilled > 0).Reverse()) {
                try {
                    var activatable = actBuff.ActivatableSource;
                    if (activatable == null || activatable.IsOn) {
                        Main.Verbose($"Activatable {actBuff.Name}: already active or null, skipping");
                        continue;
                    }

                    var group = activatable.Blueprint.Group;
                    var caster = actBuff.CasterQueue.FirstOrDefault()?.who;
                    if (caster == null) continue;

                    // Mutual exclusivity: only one per ActivatableAbilityGroup per caster
                    var groupKey = (group, caster.UniqueId);
                    if (activatedGroups.Contains(groupKey)) {
                        Main.Log($"Activatable {actBuff.Name}: skipped — another {group} ability already activated for {caster.CharacterName}");
                        continue;
                    }

                    if (!activatable.IsAvailable) {
                        Main.Verbose($"Activatable {actBuff.Name}: not available (resources or restrictions)");
                        continue;
                    }

                    Main.Verbose($"Activating: {actBuff.Name} on {caster.CharacterName}");
                    activatable.IsOn = true;
                    if (!activatable.IsStarted)
                        activatable.TryStart();
                    activatedGroups.Add(groupKey);
                    if (actBuff.DeactivateAfterRounds > 0)
                        GlobalBubbleBuffer.RoundLimitWatcher?.TrackActivation(activatable.Blueprint.AssetGuid);
                } catch (Exception ex) {
                    Main.Error(ex, $"activating {actBuff.Name}");
                }
            }
```

- [ ] **Step 2: Update the Phase 1 skip check**

In `BuffIt2TheLimit/BuffExecutor.cs`, line 231:

```csharp
// Replace:
//   if (buff.IsSong) continue; // Songs handled in Phase 0
// With:
if (buff.IsActivatable) continue; // Activatables handled in Phase 0
```

- [ ] **Step 3: Update combat start Phase 0 to use `IsActivatable`**

In `BuffIt2TheLimit/BuffExecutor.cs`, replace the combat start song phase (lines 441-475). Same pattern as Execute but for combat start:

```csharp
            // Phase 0: Activate activatable abilities marked for combat start
            var activatedGroups = new HashSet<(ActivatableAbilityGroup, string)>();
            int activatablesActivated = 0;
            foreach (var actBuff in combatStartBuffs.Where(b => b.IsActivatable && b.Fulfilled > 0).Reverse()) {
                try {
                    var activatable = actBuff.ActivatableSource;
                    if (activatable == null || activatable.IsOn) {
                        Main.Log($"  Activatable {actBuff.Name}: skipped (null={activatable == null}, already on={activatable?.IsOn})");
                        continue;
                    }

                    var group = activatable.Blueprint.Group;
                    var caster = actBuff.CasterQueue.FirstOrDefault()?.who;
                    if (caster == null) continue;

                    var groupKey = (group, caster.UniqueId);
                    if (activatedGroups.Contains(groupKey)) continue;

                    if (!activatable.IsAvailable) {
                        Main.Log($"  Activatable {actBuff.Name}: not available");
                        continue;
                    }

                    Main.Log($"  Activatable {actBuff.Name}: activating on {caster.CharacterName}");
                    activatable.IsOn = true;
                    if (!activatable.IsStarted)
                        activatable.TryStart();
                    activatedGroups.Add(groupKey);
                    activatablesActivated++;
                    if (actBuff.DeactivateAfterRounds > 0)
                        GlobalBubbleBuffer.RoundLimitWatcher?.TrackActivation(activatable.Blueprint.AssetGuid);
                } catch (Exception ex) {
                    Main.Error(ex, $"combat start: activating {actBuff.Name}");
                }
            }
```

- [ ] **Step 4: Update combat start Phase 1 skip and diagnostic log**

In `BuffIt2TheLimit/BuffExecutor.cs`:

Line 438 — update log:
```csharp
Main.Log($"  - {b.Name}: IsActivatable={b.IsActivatable}, Fulfilled={b.Fulfilled}, ActualCastQueue={b.ActualCastQueue?.Count ?? -1}");
```

Line 485 — update filter:
```csharp
foreach (var buff in combatStartBuffs.Where(b => !b.IsActivatable && b.Fulfilled > 0)) {
```

- [ ] **Step 5: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds. No remaining references to `IsSong` in BuffExecutor.cs except the property usage on BubbleBuff (which is now computed).

- [ ] **Step 6: Commit**

```bash
git add BuffIt2TheLimit/BuffExecutor.cs
git commit -m "feat: generalize Phase 0 execution for all activatable abilities"
```

---

### Task 5: Round Limit — Update `RoundLimitHandler` for all activatables

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:3638-3639`

- [ ] **Step 1: Update the buff lookup in `Tick()` to use `IsActivatable`**

In `BuffIt2TheLimit/BubbleBuffer.cs`, line 3638-3639:

```csharp
// Replace:
//   var buff = controller.state.BuffList.FirstOrDefault(b =>
//       b.IsSong && b.ActivatableSource?.Blueprint.AssetGuid == guid);
// With:
var buff = controller.state.BuffList.FirstOrDefault(b =>
    b.IsActivatable && b.ActivatableSource?.Blueprint.AssetGuid == guid);
```

- [ ] **Step 2: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: round limit deactivation works for all activatables"
```

---

### Task 6: UI — Render activatables in Ability tab

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:1906,2948-2954`

- [ ] **Step 1: Show round limit control for all activatables**

In `BuffIt2TheLimit/BubbleBuffer.cs`, line 1906:

```csharp
// Replace:
//   roundLimitObj.SetActive(buff.IsSong);
// With:
roundLimitObj.SetActive(buff.IsActivatable);
```

- [ ] **Step 2: Update `BindBuffToView` to handle all activatables**

In `BuffIt2TheLimit/BubbleBuffer.cs`, lines 2948-2954:

```csharp
// Replace:
//   if (buff.IsSong) {
//       view.ChildObject("School/SchoolLabel").GetComponent<TextMeshProUGUI>().text = "";
//       view.ChildObject("Metamagic").SetActive(false);
//       var songTooltip = new Kingmaker.UI.MVVM._VM.Tooltip.Templates.TooltipTemplateActivatableAbility(buff.ActivatableSource);
//       TooltipHelper.SetTooltip(button, songTooltip);
//       return;
//   }
// With:
if (buff.IsActivatable) {
    view.ChildObject("School/SchoolLabel").GetComponent<TextMeshProUGUI>().text = "";
    view.ChildObject("Metamagic").SetActive(false);
    var activatableTooltip = new Kingmaker.UI.MVVM._VM.Tooltip.Templates.TooltipTemplateActivatableAbility(buff.ActivatableSource);
    TooltipHelper.SetTooltip(button, activatableTooltip);
    return;
}
```

- [ ] **Step 3: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: render activatable abilities with song-style UI in ability tab"
```

---

### Task 7: UI — Settings toggle for activatables

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:776-784`
- Modify: `BuffIt2TheLimit/Config/en_GB.json`
- Modify: `BuffIt2TheLimit/Config/de_DE.json`
- Modify: `BuffIt2TheLimit/Config/fr_FR.json`
- Modify: `BuffIt2TheLimit/Config/ru_RU.json`
- Modify: `BuffIt2TheLimit/Config/zh_CN.json`

- [ ] **Step 1: Add settings toggle for activatable abilities**

In `BuffIt2TheLimit/BubbleBuffer.cs`, after the songs toggle block (after line 784):

```csharp
            {
                var (toggle, _) = MakeSettingsToggle(togglePrefab, panel.transform, "setting-activatables-enabled".i8());
                toggle.isOn = state.SavedState.ActivatablesEnabled;
                toggle.onValueChanged.AddListener(enabled => {
                    state.SavedState.ActivatablesEnabled = enabled;
                    state.InputDirty = true;
                    state.Save(true);
                });
            }
```

- [ ] **Step 2: Add localization keys**

In `BuffIt2TheLimit/Config/en_GB.json`, after line 108 (`"setting-songs-enabled"`):

```json
  "setting-activatables-enabled": "Enable activatable abilities",
```

In `BuffIt2TheLimit/Config/de_DE.json`, after line 100 (`"setting-songs-enabled"`):

```json
  "setting-activatables-enabled": "Aktivierbare Fähigkeiten aktivieren",
```

In `BuffIt2TheLimit/Config/fr_FR.json`, after the `"setting-songs-enabled"` line:

```json
  "setting-activatables-enabled": "Enable activatable abilities",
```

In `BuffIt2TheLimit/Config/ru_RU.json`, after the `"setting-songs-enabled"` line:

```json
  "setting-activatables-enabled": "Enable activatable abilities",
```

In `BuffIt2TheLimit/Config/zh_CN.json`, after the `"setting-songs-enabled"` line:

```json
  "setting-activatables-enabled": "Enable activatable abilities",
```

- [ ] **Step 3: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs BuffIt2TheLimit/Config/
git commit -m "feat: add settings toggle and localization for activatable abilities"
```

---

### Task 8: UI — Mutual exclusivity toggle logic

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs` (in the portrait click handler / target toggle area)

This task adds radio-button behavior: when toggling an activatable ability for a character, auto-untoggle others in the same `ActivatableAbilityGroup` for that character.

- [ ] **Step 1: Find the target toggle handler**

The target toggle handler is where clicking a portrait for a buff toggles whether that character "wants" the buff. Search for where `buff.SetWanted` or `wanted.Add`/`wanted.Remove` is called in `BubbleBuffer.cs` in the context of portrait clicks. This is inside the `MakeDetailsView` method or `ShowBuffWindow` — the exact location depends on how portrait toggles work.

Look for code that calls something like:
```csharp
buff.SetWanted(unit, !buff.UnitWants(unit));
```

- [ ] **Step 2: Add mutual exclusivity after the toggle**

After the line that adds a character to `wanted`, add:

```csharp
// Mutual exclusivity for activatable abilities
if (buff.IsActivatable && buff.UnitWants(unit)) {
    foreach (var other in state.BuffList) {
        if (other == buff) continue;
        if (!other.IsActivatable) continue;
        if (other.ActivatableGroup != buff.ActivatableGroup) continue;
        if (other.ActivatableGroup == Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityGroup.None) continue;
        if (other.UnitWants(unit)) {
            other.SetWanted(unit, false);
        }
    }
}
```

Note: `ActivatableAbilityGroup.None` (0) is excluded from mutual exclusivity — abilities with no group are independent.

- [ ] **Step 3: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: mutual exclusivity toggle for activatable abilities in same group"
```

---

### Task 9: Filter relaxation — Allow more regular abilities through

**Files:**
- Modify: `BuffIt2TheLimit/Extensions/ExtentionMethods.cs:255-274`
- Modify: `BuffIt2TheLimit/BufferState.cs:658-668`

- [ ] **Step 1: Add `skipDamageFilter` parameter to `GetBeneficialBuffs`**

In `BuffIt2TheLimit/Extensions/ExtentionMethods.cs`, modify the method signature and the heal/damage check (lines 255-274):

```csharp
public static IEnumerable<IBeneficialEffect> GetBeneficialBuffs(this BlueprintAbility spell, int level = 0, bool skipDamageFilter = false) {
    LogVerbose(level, $"getting buffs for spell: {spell.Name}");
    spell = spell.DeTouchify();
    LogVerbose(level, $"detouchified-to: {spell.Name}");
    if (spell.TryGetComponent<AbilityEffectRunAction>(out var runAction)) {
        var actions = runAction.Actions.Actions.Where(a => a != null).ToList();

        if (!skipDamageFilter) {
            var allActions = actions.FlattenAllActions();
            bool hasHealOrDamage = allActions.Any(a => a is ContextActionHealTarget || a is ContextActionDealDamage);
            if (hasHealOrDamage) {
                return new IBeneficialEffect[] { };
            }
        }

        return actions.SelectMany(a => a.GetBeneficialBuffs(level + 1));
    } else {
        return new IBeneficialEffect[] { };
    }
}
```

- [ ] **Step 2: Pass `skipDamageFilter` for `Category.Ability` in `AddBuff`**

In `BuffIt2TheLimit/BufferState.cs`, modify the `GetBeneficialBuffs` call around lines 658-668:

```csharp
if (!SpellsWithBeneficialBuffs.TryGetValue(spell.Blueprint.AssetGuid.m_Guid, out var abilityEffect)) {
    var beneficial = spell.Blueprint.GetBeneficialBuffs(skipDamageFilter: category == Category.Ability);
    abilityEffect = new AbilityCombinedEffects(beneficial);
    SpellsWithBeneficialBuffs[spell.Blueprint.AssetGuid.m_Guid] = abilityEffect;
    SpellNames[spell.Blueprint.AssetGuid.m_Guid] = spell.Name;
}

if (abilityEffect.Empty) {
    // Fallback for self-target abilities (e.g. Dimension Strike) that have no detectable buff effects
    if (category == Category.Ability && spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Owner) {
        Main.Verbose($"Allowing self-target ability {spell.Name} despite no detected effects", "state");
    } else {
        Main.Verbose($"Rejecting {spell.Name} because it has no applied effects", "rejection");
        return;
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/Extensions/ExtentionMethods.cs BuffIt2TheLimit/BufferState.cs
git commit -m "feat: relax beneficial effect filter for regular abilities, allow self-target fallback"
```

---

### Task 10: Validate — Update `ValidateSong` to work for all activatables

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuff.cs:292-296,406-431`

The `Validate()` method dispatches to `ValidateSong()` when `IsSong` is true. Since `IsSong` is now a subset of `IsActivatable`, we need to make `Validate()` dispatch based on `IsActivatable` and rename `ValidateSong` accordingly.

- [ ] **Step 1: Update `Validate()` dispatch**

In `BuffIt2TheLimit/BubbleBuff.cs`, lines 292-296:

```csharp
public void Validate() {
    if (IsActivatable) {
        ValidateActivatable();
        return;
    }
    if (IsMass) {
```

- [ ] **Step 2: Rename `ValidateSong` to `ValidateActivatable`**

In `BuffIt2TheLimit/BubbleBuff.cs`, lines 406-431:

```csharp
public void ValidateActivatable() {
    if (ActivatableSource == null) return;
    ActualCastQueue = new List<(string, BuffProvider)>();
    if (ActivatableSource.IsOn) {
        foreach (var target in wanted) {
            given.Add(target);
        }
        return;
    }
    if (!ActivatableSource.IsAvailable) {
        Main.Verbose($"Activatable {Name}: not available (resources or restrictions)");
        return;
    }
    if (CasterQueue.Count == 0) return;
    var caster = CasterQueue[0];
    foreach (var target in wanted) {
        given.Add(target);
    }
    ActualCastQueue.Add((caster.who.UniqueId, caster));
}
```

- [ ] **Step 3: Update `SortProviders` to use `IsActivatable`**

In `BuffIt2TheLimit/BubbleBuff.cs`, line 457:

```csharp
// Replace:
//   if (IsSong) return;
// With:
if (IsActivatable) return;
```

- [ ] **Step 4: Build and verify**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds. No remaining references to `ValidateSong`.

- [ ] **Step 5: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuff.cs
git commit -m "feat: rename ValidateSong to ValidateActivatable, dispatch on IsActivatable"
```

---

### Task 11: Final cleanup — Verify no stale `IsSong` writes remain

**Files:**
- All `.cs` files in `BuffIt2TheLimit/`

- [ ] **Step 1: Search for any remaining writes to `IsSong`**

Run: `grep -rn 'IsSong\s*=' BuffIt2TheLimit/ --include='*.cs'`

Expected: No results (the old `this.IsSong = true` was replaced in Task 1, and `IsSong` is now a read-only computed property). If any writes remain, they will cause compile errors.

- [ ] **Step 2: Search for any remaining references to `AddSong` or `SongGroups`**

Run: `grep -rn 'AddSong\|SongGroups' BuffIt2TheLimit/ --include='*.cs'`

Expected: No results. Both were renamed in Task 3.

- [ ] **Step 3: Search for any remaining references to `ValidateSong`**

Run: `grep -rn 'ValidateSong' BuffIt2TheLimit/ --include='*.cs'`

Expected: No results. Renamed in Task 10.

- [ ] **Step 4: Full build**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`

Expected: Build succeeds with zero errors.

- [ ] **Step 5: Commit if any cleanup was needed**

```bash
git add -A && git commit -m "chore: clean up stale song references"
```

---

### Task 12: Deploy and smoke test

**Files:** None (testing only)

- [ ] **Step 1: Deploy to Steam Deck**

Run: `./deploy.sh`

Expected: Build succeeds, DLL + Info.json copied to Steam Deck.

- [ ] **Step 2: Verify deploy**

Run: `ssh deck-direct "ls -la '/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/BuffIt2TheLimit/BuffIt2TheLimit.dll'"`

Expected: File timestamp matches local build.

- [ ] **Step 3: Smoke test checklist**

Start the game and verify:

1. **Songs tab** — Bardic performances still appear and activate correctly
2. **Ability tab** — New activatable abilities appear (Judgments, Rage, etc. depending on party composition)
3. **Ability tab** — Regular abilities that were previously missing (Dimension Strike, etc.) now appear
4. **Settings** — "Enable activatable abilities" toggle exists and works
5. **Mutual exclusivity** — Toggling two abilities from same group for same character: old one auto-untoggles
6. **Round limit** — Setting deactivation rounds on an activatable and verifying it deactivates
7. **Combat start** — Marking an activatable for combat start, entering combat, verifying it activates
8. **No regressions** — Existing buff casting still works normally
