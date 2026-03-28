# Caster Source Tooltip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make buff source types (Spell, Scroll, Potion, Wand/Equipment) clearly visible on caster portraits in the detail view — larger overlay icon with background + hover tooltip with source details.

**Architecture:** Two changes to the existing caster portrait system in `BubbleBuffer.cs`: (1) enlarge the source overlay icon and add a semi-transparent background image behind it, (2) bind a `TooltipTemplateSimple` to each caster portrait's `OwlcatButton` in `UpdateCasterDetails` using a new `BuildCasterTooltip` helper method. New locale keys in `en_GB.json` and `de_DE.json`.

**Tech Stack:** C#/.NET 4.81, Unity UI, Kingmaker tooltip system (`TooltipTemplateSimple`, `SetTooltip`)

**Spec:** `docs/superpowers/specs/2026-03-28-caster-source-tooltip-design.md`

---

### Task 1: Add Locale Keys

**Files:**
- Modify: `BuffIt2TheLimit/Config/en_GB.json` (add keys before closing `}`)
- Modify: `BuffIt2TheLimit/Config/de_DE.json` (add keys before closing `}`)

- [ ] **Step 1: Add tooltip locale keys to en_GB.json**

Add these keys before the closing `}` in `BuffIt2TheLimit/Config/en_GB.json` (after the `"song.rounds-remaining"` line):

```json
  "tooltip.source.spell": "Spell — {0} (Level {1})",
  "tooltip.source.scroll": "Scroll — {0}",
  "tooltip.source.potion": "Potion — {0}",
  "tooltip.source.equipment": "Equipment — {0}",
  "tooltip.source.charges": "{0} charges remaining",
  "tooltip.source.stacks": "{0} remaining",
  "tooltip.source.umd": "Requires Use Magic Device (DC {0})"
```

- [ ] **Step 2: Add tooltip locale keys to de_DE.json**

Add these keys before the closing `}` in `BuffIt2TheLimit/Config/de_DE.json` (after the `"song.rounds-remaining"` line):

```json
  "tooltip.source.spell": "Spell — {0} (Level {1})",
  "tooltip.source.scroll": "Scroll — {0}",
  "tooltip.source.potion": "Potion — {0}",
  "tooltip.source.equipment": "Equipment — {0}",
  "tooltip.source.charges": "{0} Ladungen verbleibend",
  "tooltip.source.stacks": "{0} verbleibend",
  "tooltip.source.umd": "Benötigt Use Magic Device (DC {0})"
```

Note: Technical gaming/UI terms (Spell, Scroll, Potion, Equipment, Use Magic Device, DC) stay in English for de_DE per project convention.

- [ ] **Step 3: Build to verify JSON is valid**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/`
Expected: Build succeeds (locale JSON files are embedded resources — malformed JSON would cause runtime errors, but valid JSON compiles fine).

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/Config/en_GB.json BuffIt2TheLimit/Config/de_DE.json
git commit -m "feat: add locale keys for caster source tooltips"
```

---

### Task 2: Add Source Overlay Background to Portrait

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:2681-2704` (add `SourceOverlayBg` field to `Portrait` class)
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:387-394` (enlarge overlay + add background image in `CreatePortrait`)

- [ ] **Step 1: Add `SourceOverlayBg` field to `Portrait` class**

In `BuffIt2TheLimit/BubbleBuffer.cs`, find the `Portrait` class at line 2681. Add a new field after `SourceOverlay`:

```csharp
class Portrait {

    public Image Image;
    public OwlcatButton Button;
    public GameObject GameObject;
    public TextMeshProUGUI Text;
    public OwlcatButton Expand;
    public Image Overlay;
    public Image FullOverlay;
    public Image SourceOverlay;
    public Image SourceOverlayBg;
    public bool State = false;
```

- [ ] **Step 2: Add background image and enlarge overlay in `CreatePortrait`**

In `BuffIt2TheLimit/BubbleBuffer.cs`, find the source overlay creation block at line 387-394. Replace it with:

```csharp
            var (sourceOverlayBgObj, sourceOverlayBgRect) = UIHelpers.Create("source-overlay-bg", pRect);
            sourceOverlayBgRect.anchorMin = new Vector2(0.5f, 0.0f);
            sourceOverlayBgRect.anchorMax = new Vector2(1.0f, 0.45f);
            sourceOverlayBgRect.offsetMin = Vector2.zero;
            sourceOverlayBgRect.offsetMax = Vector2.zero;
            portrait.SourceOverlayBg = sourceOverlayBgObj.AddComponent<Image>();
            portrait.SourceOverlayBg.color = new Color(0, 0, 0, 0.6f);
            sourceOverlayBgObj.SetActive(false);

            var (sourceOverlayObj, sourceOverlayRect) = UIHelpers.Create("source-overlay", pRect);
            sourceOverlayRect.anchorMin = new Vector2(0.5f, 0.0f);
            sourceOverlayRect.anchorMax = new Vector2(1.0f, 0.45f);
            sourceOverlayRect.offsetMin = Vector2.zero;
            sourceOverlayRect.offsetMax = Vector2.zero;
            portrait.SourceOverlay = sourceOverlayObj.AddComponent<Image>();
            portrait.SourceOverlay.preserveAspect = true;
            sourceOverlayObj.SetActive(false);
```

Key changes from original:
- Anchors enlarged from `(0.55, 0.0)-(1.0, 0.35)` to `(0.5, 0.0)-(1.0, 0.45)`
- New `source-overlay-bg` sibling created BEFORE `source-overlay` (so it renders behind it)
- Background uses `Color(0, 0, 0, 0.6f)` — semi-transparent black

- [ ] **Step 3: Update `UpdateCasterDetails` to show/hide background with overlay**

In `BuffIt2TheLimit/BubbleBuffer.cs`, find the overlay display logic in `UpdateCasterDetails` at line 3094-3111. Replace the entire `if (casterPortraits[i].SourceOverlay != null)` block:

```csharp
                    if (casterPortraits[i].SourceOverlay != null) {
                        if (who.SourceType == BuffSourceType.Spell) {
                            casterPortraits[i].SourceOverlay.gameObject.SetActive(false);
                            casterPortraits[i].SourceOverlayBg?.gameObject.SetActive(false);
                        } else {
                            var overlaySprite = who.SourceType switch {
                                BuffSourceType.Scroll => GlobalBubbleBuffer.scrollOverlayIcon,
                                BuffSourceType.Potion => GlobalBubbleBuffer.potionOverlayIcon,
                                BuffSourceType.Equipment => GlobalBubbleBuffer.equipmentOverlayIcon,
                                _ => null
                            };
                            if (overlaySprite != null) {
                                casterPortraits[i].SourceOverlay.sprite = overlaySprite;
                                casterPortraits[i].SourceOverlay.gameObject.SetActive(true);
                                casterPortraits[i].SourceOverlayBg?.gameObject.SetActive(true);
                            } else {
                                casterPortraits[i].SourceOverlay.gameObject.SetActive(false);
                                casterPortraits[i].SourceOverlayBg?.gameObject.SetActive(false);
                            }
                        }
                    }
```

- [ ] **Step 4: Build to verify compilation**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/`
Expected: Build succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: enlarge source overlay icon with semi-transparent background"
```

---

### Task 3: Add Hover Tooltip to Caster Portraits

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs` — add `BuildCasterTooltip` method to `BufferView` class, update `UpdateCasterDetails` to bind tooltips

- [ ] **Step 1: Add `BuildCasterTooltip` method to `BufferView`**

In `BuffIt2TheLimit/BubbleBuffer.cs`, add this method to the `BufferView` class, directly before the `UpdateCasterDetails` method (before line 3062):

```csharp
        private string BuildCasterTooltip(BuffProvider provider) {
            var lines = new List<string>();

            switch (provider.SourceType) {
                case BuffSourceType.Spell:
                    var spellLevel = provider.spell?.SpellLevel ?? 0;
                    var bookName = provider.book?.Blueprint.Name ?? "";
                    lines.Add(string.Format("tooltip.source.spell".i8(), bookName, spellLevel));
                    if (provider.AvailableCredits < 100)
                        lines.Add(string.Format("tooltip.source.stacks".i8(), provider.AvailableCredits));
                    break;
                case BuffSourceType.Scroll:
                    var scrollName = provider.SourceItem?.Blueprint.Name ?? provider.spell?.Blueprint.Name ?? "";
                    lines.Add(string.Format("tooltip.source.scroll".i8(), scrollName));
                    lines.Add(string.Format("tooltip.source.stacks".i8(), provider.AvailableCredits));
                    break;
                case BuffSourceType.Potion:
                    var potionName = provider.SourceItem?.Blueprint.Name ?? provider.spell?.Blueprint.Name ?? "";
                    lines.Add(string.Format("tooltip.source.potion".i8(), potionName));
                    lines.Add(string.Format("tooltip.source.stacks".i8(), provider.AvailableCredits));
                    break;
                case BuffSourceType.Equipment:
                    var equipName = provider.SourceItem?.Blueprint.Name ?? provider.spell?.Blueprint.Name ?? "";
                    lines.Add(string.Format("tooltip.source.equipment".i8(), equipName));
                    lines.Add(string.Format("tooltip.source.charges".i8(), provider.AvailableCredits));
                    break;
            }

            // UMD hint for scroll/wand sources not on class spell list
            if (provider.SourceType == BuffSourceType.Scroll || provider.SourceType == BuffSourceType.Equipment) {
                bool onClassList = provider.who.Spellbooks.Any(b =>
                    b.Blueprint.SpellList?.SpellsByLevel?.Any(level =>
                        level.Spells.Any(s => s == provider.spell?.Blueprint)) == true);
                if (!onClassList) {
                    lines.Add(string.Format("tooltip.source.umd".i8(), provider.ScrollDC));
                }
            }

            return string.Join("\n", lines);
        }
```

This method:
- Builds a multi-line tooltip body based on `BuffProvider.SourceType`
- Uses the existing locale keys with `string.Format` for parameterized text
- Reuses the class-list check pattern from `BuffProvider.RequiresUmdCheck` (line 603-609) but extends it to also cover Equipment/wands
- Reuses `BuffProvider.ScrollDC` which works for both scrolls and wands (it reads from `SourceItem.Blueprint`)
- Does not handle `BuffSourceType.Song` — songs get a different tooltip type (see step 3)

- [ ] **Step 2: Bind tooltips in `UpdateCasterDetails`**

In `BuffIt2TheLimit/BubbleBuffer.cs`, in the `UpdateCasterDetails` method, add tooltip binding after the existing source overlay block and before `casterPortraits[i].Text.fontSize = 12;` (line 3112). Insert after the closing `}` of the `if (casterPortraits[i].SourceOverlay != null)` block:

Find this code (around line 3111-3112):
```csharp
                    }
                    casterPortraits[i].Text.fontSize = 12;
```

Insert between those lines:

```csharp
                    // Bind hover tooltip
                    if (who.SourceType == BuffSourceType.Song && buff.ActivatableSource != null) {
                        var songTooltip = new Kingmaker.UI.MVVM._VM.Tooltip.Templates.TooltipTemplateActivatableAbility(buff.ActivatableSource);
                        casterPortraits[i].Button.SetTooltip(songTooltip, new TooltipConfig {
                            InfoCallPCMethod = InfoCallPCMethod.None
                        });
                    } else {
                        var tooltipBody = BuildCasterTooltip(who);
                        casterPortraits[i].Button.SetTooltip(
                            new TooltipTemplateSimple(who.who.CharacterName, tooltipBody),
                            new TooltipConfig { InfoCallPCMethod = InfoCallPCMethod.None });
                    }
```

This uses `TooltipTemplateActivatableAbility` for songs (the game's native activatable ability tooltip, already used in `BindBuffToView` at line 2739) and `TooltipTemplateSimple` for all other source types.

- [ ] **Step 3: Build to verify compilation**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/`
Expected: Build succeeds with no errors.

- [ ] **Step 4: Commit**

```bash
git add BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: add hover tooltip with source details to caster portraits"
```

---

### Task 4: Deploy and Verify

**Files:** None modified — deployment and manual testing only.

- [ ] **Step 1: Deploy to Steam Deck**

Run: `./deploy.sh`
Expected: Build succeeds, DLL + Info.json SCP'd to Steam Deck.

- [ ] **Step 2: Verify deploy**

Compare timestamps:
```bash
ls -la BuffIt2TheLimit/bin/Debug/BuffIt2TheLimit.dll
ssh deck-direct "ls -la '/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/BuffIt2TheLimit/BuffIt2TheLimit.dll'"
```
Expected: File sizes and dates match.

- [ ] **Step 3: Manual test checklist**

Start the game on Steam Deck, open the buff menu, and verify:

1. Select a buff that has a **spell** caster — portrait should have NO overlay icon. Hover should show tooltip with spellbook name, spell level, slots remaining.
2. Select a buff with a **scroll** caster — portrait should show enlarged scroll icon with dark background. Hover should show "Scroll — {item name}" + count + UMD line if applicable.
3. Select a buff with a **wand/equipment** caster — same as scroll but with equipment icon and "charges remaining".
4. Select a buff with a **potion** caster — potion icon overlay, hover shows "Potion — {item name}" + count.
5. If a **song** is available — hover on caster portrait should show the game's native activatable ability tooltip.
6. Overlay background is visible and provides contrast against portrait artwork.

- [ ] **Step 4: Commit any fixes if needed, or confirm done**
