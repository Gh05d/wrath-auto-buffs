# Extend Rod Support Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add automatic Extend Spell metamagic rod support to buff casting, with per-buff toggle, weakest-rod-first selection from shared inventory, and graceful fallback.

**Architecture:** New `UseExtendRod` field on save state and runtime buff. Rod discovery via GUID-based inventory scan. Rod metamagic applied by modifying `SpellToCast.MetamagicData` in `EngineCastingHandler` before cast, charge consumed after cast. UI toggle in source controls section.

**Tech Stack:** C#/.NET Framework 4.81, Unity UI, Harmony, Newtonsoft.Json

**Spec:** `docs/superpowers/specs/2026-03-15-extend-rod-support-design.md`

---

## Chunk 1: Data Model & Rod Discovery

### Task 1: Add UseExtendRod to SavedBuffState and BubbleBuff

**Files:**
- Modify: `BuffIt2TheLimit/SaveState.cs:63-92` — add field to `SavedBuffState`
- Modify: `BuffIt2TheLimit/BubbleBuff.cs:141-144` — add runtime field to `BubbleBuff`
- Modify: `BuffIt2TheLimit/BubbleBuff.cs:187-202` — add to `InitialiseFromSave()`
- Modify: `BuffIt2TheLimit/BufferState.cs:438-441` — add to `Save()` in `updateSavedBuff`

- [ ] **Step 1: Add `UseExtendRod` field to `SavedBuffState`**

In `SaveState.cs`, after the `UseEquipment` field (line 91):

```csharp
[JsonProperty]
public bool UseEquipment = true;
// ADD:
[JsonProperty]
public bool UseExtendRod;
```

- [ ] **Step 2: Add `UseExtendRod` runtime field to `BubbleBuff`**

In `BubbleBuff.cs`, after `UseEquipment` (line 144):

```csharp
public bool UseEquipment = true;
// ADD:
public bool UseExtendRod;
```

- [ ] **Step 3: Load from save in `InitialiseFromSave()`**

In `BubbleBuff.cs`, after `UseEquipment = state.UseEquipment;` (line 201):

```csharp
UseEquipment = state.UseEquipment;
// ADD:
UseExtendRod = state.UseExtendRod;
```

- [ ] **Step 4: Save in `BufferState.Save()`**

In `BufferState.cs`, after `save.UsePotions = buff.UsePotions;` (line 441):

```csharp
save.UsePotions = buff.UsePotions;
// ADD (UseEquipment was missing from save — pre-existing bug, fix it here):
save.UseEquipment = buff.UseEquipment;
save.UseExtendRod = buff.UseExtendRod;
```

- [ ] **Step 5: Build to verify no errors**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

Expected: Build succeeds. No errors.

- [ ] **Step 6: Commit**

```bash
git add BuffIt2TheLimit/SaveState.cs BuffIt2TheLimit/BubbleBuff.cs BuffIt2TheLimit/BufferState.cs
git commit -m "feat(extend-rod): add UseExtendRod to data model and save/load"
```

### Task 2: Extract Greater Extend Rod GUID

**Files:** None modified — research task

- [ ] **Step 1: Extract Greater Extend Rod blueprint from game data**

```bash
ssh deck-direct "unzip -l '/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/blueprints.zip'" | grep -i "MetamagicRodGreater.*Extend\|ExtendGreater\|GreaterExtend"
```

Look for the `.jbp` path, then extract it:

```bash
ssh deck-direct "unzip -p '/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/blueprints.zip' '<found-path>.jbp'"
```

Record the `AssetId` GUID from the extracted JSON. Also verify the Lesser and Normal GUIDs from the spec:
- Lesser: `1cf04842d5dbd0f49946b1af1022cd1a`
- Normal: `1b2a09528da9e9948aa9026037bada90`

```bash
ssh deck-direct "unzip -p '/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/blueprints.zip' 'Blueprints/Items/Rods/MetamagicRodExtendLesser.jbp'" | head -20
ssh deck-direct "unzip -p '/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/blueprints.zip' 'Blueprints/Items/Rods/MetamagicRodExtendNormal.jbp'" | head -20
```

- [ ] **Step 2: Document all three GUIDs**

Record the three verified GUIDs for use in Task 3. If the spec GUIDs are wrong, update the spec.

### Task 3: Implement rod discovery method

**Files:**
- Modify: `BuffIt2TheLimit/BufferState.cs` — add `FindBestExtendRod()` method and GUID constants

- [ ] **Step 1: Add Extend Rod GUID constants and discovery method**

In `BufferState.cs`, add near the top of the `BufferState` class (after existing static fields around line 467-469):

```csharp
// Extend Rod blueprint GUIDs (Lesser → Normal → Greater, sorted by MaxSpellLevel)
private static readonly (string guid, int maxSpellLevel)[] ExtendRodBlueprints = {
    ("1cf04842d5dbd0f49946b1af1022cd1a", 3),  // Lesser Extend Rod
    ("1b2a09528da9e9948aa9026037bada90", 6),  // Normal Extend Rod
    ("<GREATER_GUID_FROM_TASK_2>", 9),          // Greater Extend Rod
};

/// <summary>
/// Find the weakest Extend Rod in party inventory that can affect a spell of the given level.
/// Uses remainingCharges to track charges within a single Execute() pass.
/// Returns the item, or null if no suitable rod is available.
/// </summary>
public static Kingmaker.Items.ItemEntity FindBestExtendRod(int spellLevel, Dictionary<Kingmaker.Items.ItemEntity, int> remainingCharges) {
    foreach (var (guid, maxSpellLevel) in ExtendRodBlueprints) {
        if (spellLevel > maxSpellLevel) continue;

        foreach (var item in Game.Instance.Player.Inventory) {
            if (item.Blueprint.AssetGuidThreadSafe != guid) continue;

            int charges;
            if (remainingCharges.TryGetValue(item, out charges)) {
                if (charges <= 0) continue;
            } else {
                charges = item.Charges;
                if (charges <= 0) continue;
                remainingCharges[item] = charges;
            }

            return item;
        }
    }
    return null;
}
```

- [ ] **Step 2: Build to verify no errors**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

Expected: Build succeeds. The `using Kingmaker;` import should already be present in `BufferState.cs`.

- [ ] **Step 3: Commit**

```bash
git add BuffIt2TheLimit/BufferState.cs
git commit -m "feat(extend-rod): add rod discovery with GUID-based inventory scan"
```

## Chunk 2: Casting Pipeline Integration

### Task 4: Add MetamagicRodItem to CastTask

**Files:**
- Modify: `BuffIt2TheLimit/BuffExecutor.cs:304-316` — add field to `CastTask`

- [ ] **Step 1: Add rod field to CastTask**

In `BuffExecutor.cs`, after `public Kingmaker.Items.ItemEntity SourceItem;` (line 316):

```csharp
public Kingmaker.Items.ItemEntity SourceItem;
// ADD:
public Kingmaker.Items.ItemEntity MetamagicRodItem;
```

- [ ] **Step 2: Build to verify**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

- [ ] **Step 3: Commit**

```bash
git add BuffIt2TheLimit/BuffExecutor.cs
git commit -m "feat(extend-rod): add MetamagicRodItem field to CastTask"
```

### Task 5: Wire rod lookup into BuffExecutor.Execute()

**Files:**
- Modify: `BuffIt2TheLimit/BuffExecutor.cs:106-301` — add rod lookup in Execute()

- [ ] **Step 1: Add remainingRodCharges dictionary**

In `BuffExecutor.Execute()`, after the `remainingArcanistPool` declaration (line 134):

```csharp
Dictionary<UnitEntityData, int> remainingArcanistPool = new Dictionary<UnitEntityData, int>();
// ADD:
Dictionary<Kingmaker.Items.ItemEntity, int> remainingRodCharges = new();
```

Also, inside the per-buff `foreach` loop (line 137), after the existing local variables (line 144):

```csharp
                    var thisBuffSourceCounts = new Dictionary<BuffSourceType, int>();
                    // ADD:
                    bool anyExtendRod = false;
```

- [ ] **Step 2: Add rod lookup when creating CastTask**

In `BuffExecutor.Execute()`, after the `CastTask` is constructed (around line 248-261), before `tasks.Add(task)` (line 263), add:

```csharp
                        var task = new CastTask {
                            // ... existing fields ...
                        };

                        // ADD: Extend Rod lookup
                        // Only for spell-source casts — scroll/wand/equipment casts don't have a
                        // spellbook to determine spell level from. Future extension possible.
                        if (buff.UseExtendRod && caster.SourceType == BuffSourceType.Spell) {
                            int spellLevel = caster.spell.Spellbook.GetSpellLevel(caster.spell);
                            var rod = BufferState.FindBestExtendRod(spellLevel, remainingRodCharges);
                            if (rod != null) {
                                task.MetamagicRodItem = rod;
                                remainingRodCharges[rod] = remainingRodCharges[rod] - 1;
                                anyExtendRod = true;
                                Main.Verbose($"Extend Rod applied: {rod.Name} for {buff.Name}");
                            } else {
                                Main.Log($"Extend Rod unavailable for {buff.Name}, casting normally");
                            }
                        }

                        tasks.Add(task);
```

- [ ] **Step 3: Build to verify**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/BuffExecutor.cs
git commit -m "feat(extend-rod): wire rod lookup into BuffExecutor.Execute()"
```

### Task 6: Apply Extend metamagic and consume rod charge in EngineCastingHandler

**Files:**
- Modify: `BuffIt2TheLimit/Handlers/EngineCastingHandler.cs:80-96` — apply metamagic in constructor
- Modify: `BuffIt2TheLimit/Handlers/EngineCastingHandler.cs:112-138` — consume charge in HandleExecutionProcessEnd

- [ ] **Step 1: Add using for Metamagic enum**

Check if `Kingmaker.UnitLogic.Abilities` is already imported (it is, line 10). The `Metamagic` enum lives in this namespace. No additional import needed.

- [ ] **Step 2: Add fields for rod state tracking**

In `EngineCastingHandler`, after the existing fields (line 23):

```csharp
private ModifiableValue.Modifier _casterLevelModifier;
// ADD:
private bool _rodMetamagicApplied;
private bool _metamagicWasNull;
private Metamagic _originalMetamagicMask;
```

- [ ] **Step 3: Apply Extend metamagic in constructor**

In the constructor, after `RemoveSpellResistance();` (line 90), add:

```csharp
            RemoveSpellResistance();

            // ADD: Apply Extend Rod metamagic
            if (_castTask.MetamagicRodItem != null) {
                try {
                    if (_castTask.SpellToCast.MetamagicData == null) {
                        _metamagicWasNull = true;
                        _castTask.SpellToCast.MetamagicData = new MetamagicData(_castTask.SpellToCast.Blueprint, null) {
                            MetamagicMask = Metamagic.Extend
                        };
                    } else {
                        _metamagicWasNull = false;
                        _originalMetamagicMask = _castTask.SpellToCast.MetamagicData.MetamagicMask;
                        _castTask.SpellToCast.MetamagicData.MetamagicMask |= Metamagic.Extend;
                    }
                    _rodMetamagicApplied = true;
                    Main.Verbose($"Applied Extend metamagic via rod for {_castTask.SpellToCast.Name}");
                } catch (Exception ex) {
                    Main.Error(ex, "Applying Extend Rod metamagic");
                }
            }
```

**Note:** The `MetamagicData` constructor signature needs verification during implementation. If `new MetamagicData(blueprint, null)` doesn't compile, check `ilspycmd` output for the actual constructor:

```bash
DOTNET_ROOT=/home/pascal/.dotnet ~/.dotnet/tools/ilspycmd /home/pascal/Code/wrath-mods/wrath-epic-buffing/GameInstall/Wrath_Data/Managed/Assembly-CSharp.dll -t Kingmaker.UnitLogic.Abilities.MetamagicData
```

- [ ] **Step 4: Consume rod charge and restore metamagic in HandleExecutionProcessEnd**

In `HandleExecutionProcessEnd()`, after the existing item consumption block (line 128-131), add:

```csharp
                        } else if (_castTask.SourceType == BuffSourceType.Equipment && _castTask.SourceItem.IsSpendCharges) {
                            _castTask.SourceItem.Charges--;
                        }
                    } catch (Exception itemEx) {
                        Main.Error(itemEx, "Consuming item after cast");
                    }

                    // ADD: Consume Extend Rod charge and restore metamagic
                    if (_rodMetamagicApplied) {
                        try {
                            // Restore original MetamagicData state
                            if (_metamagicWasNull) {
                                _castTask.SpellToCast.MetamagicData = null;
                            } else {
                                _castTask.SpellToCast.MetamagicData.MetamagicMask = _originalMetamagicMask;
                            }
                            // Consume rod charge
                            if (_castTask.MetamagicRodItem != null && _castTask.MetamagicRodItem.IsSpendCharges) {
                                _castTask.MetamagicRodItem.Charges--;
                            }
                        } catch (Exception rodEx) {
                            Main.Error(rodEx, "Consuming Extend Rod charge");
                        }
                    }
```

- [ ] **Step 5: Build to verify**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

If `MetamagicData` constructor fails, use `ilspycmd` to find the correct signature and adjust.

- [ ] **Step 6: Commit**

```bash
git add BuffIt2TheLimit/Handlers/EngineCastingHandler.cs
git commit -m "feat(extend-rod): apply Extend metamagic and consume rod charge"
```

## Chunk 3: UI & Localization

### Task 7: Add localization keys

**Files:**
- Modify: `BuffIt2TheLimit/Config/en_GB.json`
- Modify: `BuffIt2TheLimit/Config/de_DE.json`
- Modify: `BuffIt2TheLimit/Config/fr_FR.json`
- Modify: `BuffIt2TheLimit/Config/ru_RU.json`
- Modify: `BuffIt2TheLimit/Config/zh_CN.json`

- [ ] **Step 1: Add `use.extendrod` key to en_GB.json**

After `"use.equipment": "Use Equipment"` (line 86):

```json
  "use.equipment": "Use Equipment",
  "use.extendrod": "Use Extend Rod",
  "log.extend-rod-applied": "Extend Rod",
```

- [ ] **Step 2: Add to de_DE.json**

```json
  "use.extendrod": "Extend-Stab verwenden",
  "log.extend-rod-applied": "Extend-Stab",
```

- [ ] **Step 3: Add to fr_FR.json**

```json
  "use.extendrod": "Utiliser Baguette d'Extension",
  "log.extend-rod-applied": "Baguette d'Extension",
```

- [ ] **Step 4: Add to ru_RU.json**

```json
  "use.extendrod": "Использовать жезл Продления",
  "log.extend-rod-applied": "Жезл Продления",
```

- [ ] **Step 5: Add to zh_CN.json**

```json
  "use.extendrod": "使用延长法术权杖",
  "log.extend-rod-applied": "延长权杖",
```

- [ ] **Step 6: Build to verify**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

- [ ] **Step 7: Commit**

```bash
git add BuffIt2TheLimit/Config/
git commit -m "feat(extend-rod): add localization keys for all 5 locales"
```

### Task 8: Add UI toggle in source controls section

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:1436-1527` — restructure left side, add toggle
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:1622-1678` — update `UpdateDetailsView` to set toggle state

- [ ] **Step 1: Restructure prioSideObj to VLG and add Extend Rod toggle**

In `BubbleBuffer.cs`, replace the `prioSideObj` setup (lines 1437-1441) and `prioLabelObj` creation (lines 1499-1516) with a VLG-based layout. The key changes:

After `var (prioSideObj, prioSideRect) = UIHelpers.Create("prio-side", sourceControlObj.transform);` and its anchor setup (lines 1437-1441), add a VLG:

```csharp
            // Left side — priority (left 55%)
            var (prioSideObj, prioSideRect) = UIHelpers.Create("prio-side", sourceControlObj.transform);
            prioSideRect.anchorMin = new Vector2(0, 0);
            prioSideRect.anchorMax = new Vector2(0.55f, 1);
            prioSideRect.offsetMin = new Vector2(20, 2);
            prioSideRect.offsetMax = new Vector2(0, -2);
            // ADD: VLG for stacking priority text + extend rod toggle
            var prioVLG = prioSideObj.AddComponent<VerticalLayoutGroup>();
            prioVLG.childForceExpandHeight = false;
            prioVLG.childForceExpandWidth = true;
            prioVLG.childControlHeight = true;
            prioVLG.childControlWidth = true;
            prioVLG.spacing = 4;
```

Then replace the anchor-based `prioLabelObj` creation (lines 1499-1516). The `prioLabelObj` must become LayoutElement-driven instead of anchor-based:

```csharp
            // Priority row — clickable text that cycles through priority options
            var prioLabelObj = new GameObject("prio-label", typeof(RectTransform));
            var prioLabelRect = prioLabelObj.GetComponent<RectTransform>();
            prioLabelRect.SetParent(prioSideObj.transform, false);
            // REMOVE anchor-based sizing:
            // prioLabelRect.anchorMin = Vector2.zero;
            // prioLabelRect.anchorMax = Vector2.one;
            // prioLabelRect.offsetMin = Vector2.zero;
            // prioLabelRect.offsetMax = Vector2.zero;
            // ADD LayoutElement-based sizing:
            var prioLE = prioLabelObj.AddComponent<LayoutElement>();
            prioLE.preferredHeight = 24;
            prioLE.flexibleWidth = 1;
```

Then after the `prioButton.onClick.AddListener(...)` block (after line 1526), add the Extend Rod toggle:

```csharp
            // Extend Rod toggle — on left side, below priority
            var useExtendRodObj = MakeSourceToggle("use.extendrod".i8());
            useExtendRodObj.transform.SetParent(prioSideObj.transform, false);
            var useExtendRodToggle = useExtendRodObj.GetComponentInChildren<ToggleWorkaround>();

            useExtendRodToggle.onValueChanged.AddListener(val => {
                var b = view.Selected;
                if (b != null) { b.UseExtendRod = val; if (b.SavedState != null) b.SavedState.UseExtendRod = val; state.Save(); }
            });
```

Note: `MakeSourceToggle` is defined at line 1458 and creates toggles parented to `toggleSideObj`. Since we re-parent the Extend Rod toggle to `prioSideObj`, the initial parent doesn't matter — `SetParent` moves it.

- [ ] **Step 2: Update `UpdateDetailsView` to set Extend Rod toggle state**

In the `UpdateDetailsView` lambda (around line 1672-1678), after the `useEquipmentToggle.isOn` block, add:

```csharp
                useEquipmentObj.SetActive(hasEquipmentProviders);
                if (hasEquipmentProviders)
                    useEquipmentToggle.isOn = buff.SavedState?.UseEquipment ?? true;

                // ADD: Extend Rod toggle — always visible when source controls are shown
                useExtendRodToggle.isOn = buff.UseExtendRod;
```

- [ ] **Step 3: Build to verify**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat(extend-rod): add Use Extend Rod toggle to source controls UI"
```

## Chunk 4: Tooltip & Logging Integration

### Task 9: Add Extend Rod indicator to casting tooltip

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:1922-2001` — add `ExtendRodUsed` to `BuffResult`, render in tooltip
- Modify: `BuffIt2TheLimit/BuffExecutor.cs:278-282` — set `ExtendRodUsed` on good result

- [ ] **Step 1: Add `ExtendRodUsed` field to BuffResult**

In `BubbleBuffer.cs`, in the `BuffResult` class (line 1923-1931):

```csharp
        public class BuffResult {
            public BubbleBuff buff;
            public List<string> messages;
            public int count;
            public Dictionary<BuffSourceType, int> sourceCounts = new();
            // ADD:
            public bool ExtendRodUsed;
            public BuffResult(BubbleBuff buff) {
                this.buff = buff;
            }
        };
```

- [ ] **Step 2: Render `[Extend]` suffix in tooltip**

In `AddResultsNoMessages()` (line 1977-2000), after the source counts label construction (line 1995), add:

```csharp
                        label += $" ({string.Join(", ", parts)})";
                    }
                    // ADD: Extend Rod suffix
                    if (r.ExtendRodUsed)
                        label += $" [{"log.extend-rod-applied".i8()}]";
                    elements.Add(new TooltipBrickIconAndName(r.buff.Spell.Icon, label, TooltipBrickElementType.Small));
```

- [ ] **Step 3: Set ExtendRodUsed in BuffExecutor**

In `BuffExecutor.Execute()`, where `goodResult` is created (line 278-281), add:

```csharp
                    if (thisBuffGood > 0) {
                        var goodResult = tooltip.AddGood(buff);
                        goodResult.count = thisBuffGood;
                        goodResult.sourceCounts = thisBuffSourceCounts;
                        // ADD: tracked via anyExtendRod bool set during rod lookup above
                        goodResult.ExtendRodUsed = anyExtendRod;
                    }
```

- [ ] **Step 4: Build to verify**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

- [ ] **Step 5: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs BuffIt2TheLimit/BuffExecutor.cs
git commit -m "feat(extend-rod): add [Extend] indicator to casting tooltip"
```

## Chunk 5: Deploy & Manual Test

### Task 10: Build, deploy, and test on Steam Deck

**Files:** None modified — testing task

- [ ] **Step 1: Build release**

```bash
~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/
```

- [ ] **Step 2: Deploy to Steam Deck**

```bash
./deploy.sh
```

- [ ] **Step 3: Manual test checklist**

On Steam Deck, start Pathfinder WotR and test:

1. **Toggle visibility:** Open buff setup → select a buff → verify "Use Extend Rod" toggle is visible in left side of source controls
2. **Toggle persistence:** Enable toggle → close and reopen buff setup → verify toggle is still enabled
3. **Rod consumption:** Put Lesser Extend Rod in inventory → enable toggle on a level 1-3 buff → cast → verify rod charge decremented
4. **Tier selection:** Have both Lesser and Normal rods → cast level 1 buff → verify Lesser is consumed (not Normal)
5. **Fallback:** Enable toggle but remove all Extend Rods from inventory → cast → verify spell casts normally, check Player.log for "Extend Rod unavailable" message
6. **Charges exhausted mid-batch:** Have rod with 1 charge → enable Extend on two buffs → cast both → verify first gets Extend, second falls back to normal
7. **Tooltip:** After casting with Extend Rod, check combat log tooltip for `[Extend]` suffix
8. **Duration:** Cast a buff with Extend Rod → verify buff duration is doubled compared to without rod

- [ ] **Step 4: Check Player.log for errors**

```bash
ssh deck-direct "grep -i 'extend\|error\|exception' '/home/deck/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Player.log' | tail -30"
```

- [ ] **Step 5: If MetamagicData approach doesn't work**

If the buff duration is NOT doubled despite `MetamagicData` being set, the game may check metamagic at a different point. Fallback approach:
- Use `ilspycmd` to decompile `RuleCastSpell` and trace where Extend metamagic affects duration
- May need to patch `RuleCalculateDuration` or modify the buff's `EndTime` post-cast instead

- [ ] **Step 6: Final commit (if any fixes needed)**

Stage only the specific files that were changed, then commit:

```bash
git add <changed-files>
git commit -m "fix(extend-rod): adjustments from manual testing"
```
