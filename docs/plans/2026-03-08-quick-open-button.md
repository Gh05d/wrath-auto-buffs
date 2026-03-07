# Quick Open Buff Menu Button Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a 4th HUD button (with gap separator) that directly opens the BubbleBuffs menu without having to navigate through the spellbook screen first.

**Architecture:** Add a spacer and a new button to the existing HUD button container (`buttonsContainer`) in `TryInstallUI()`. The button triggers the same logic as the existing toggle button inside the spellbook screen: opens the spellbook view, then activates the buff window. Reuse the existing `ButtonSprites` pattern with new PNG assets.

**Tech Stack:** C# / Unity UI / Owlcat WotR API

---

### Task 1: Create Button Sprites

**Files:**
- Create: `BubbleBuffs/Assets/icons/open_buffs_normal.png`
- Create: `BubbleBuffs/Assets/icons/open_buffs_hover.png`
- Create: `BubbleBuffs/Assets/icons/open_buffs_down.png`
- Modify: `BubbleBuffs/BubbleBuffs.csproj` (add Content entries)

**Step 1: Create placeholder PNG files**

We need 3 PNG files (95x95 pixels, same as existing button sprites). For now, copy the existing `apply_buffs` sprites as placeholders:

```bash
cp BubbleBuffs/Assets/icons/apply_buffs_normal.png BubbleBuffs/Assets/icons/open_buffs_normal.png
cp BubbleBuffs/Assets/icons/apply_buffs_hover.png BubbleBuffs/Assets/icons/open_buffs_hover.png
cp BubbleBuffs/Assets/icons/apply_buffs_down.png BubbleBuffs/Assets/icons/open_buffs_down.png
```

**Step 2: Add Content entries to csproj**

In `BubbleBuffs/BubbleBuffs.csproj`, after the existing `show_map_normal.png` Content entry (around line 93-95), add:

```xml
<Content Include="Assets\icons\open_buffs_down.png">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
<Content Include="Assets\icons\open_buffs_hover.png">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
<Content Include="Assets\icons\open_buffs_normal.png">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

**Step 3: Build**

```bash
$HOME/.dotnet/dotnet build BubbleBuffs/BubbleBuffs.csproj
```

**Step 4: Commit**

```bash
git add -A && git commit -m "chore: add open_buffs button sprite placeholders

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 2: Add the Quick Open Button to the HUD

**Files:**
- Modify: `BubbleBuffs/BubbleBuffer.cs:1864-1870` (fields — add openBuffsSprites)
- Modify: `BubbleBuffs/BubbleBuffer.cs:1922-1930` (TryInstallUI — load sprites)
- Modify: `BubbleBuffs/BubbleBuffer.cs:2067-2073` (TryInstallUI — add spacer + button)
- Modify: `BubbleBuffs/Config/en_GB.json`
- Modify: `BubbleBuffs/Config/de_DE.json`

**Step 1: Add sprite field**

In `GlobalBubbleBuffer`, alongside existing `ButtonSprites` fields (around line 1864):

```csharp
private ButtonSprites openBuffsSprites;
```

**Step 2: Load the sprites**

In `TryInstallUI()`, after the existing sprite loads (around line 1930):

```csharp
if (openBuffsSprites == null)
    openBuffsSprites = ButtonSprites.Load("open_buffs", new Vector2Int(95, 95));
```

**Step 3: Add gap and button**

After the existing `AddButton` calls for the 3 buff group buttons (after line 2073, after the DungeonController block), add:

```csharp
// Add spacer gap before the Open Buffs button
var spacer = new GameObject("button-spacer", typeof(RectTransform));
spacer.transform.SetParent(buttonsContainer.transform, false);
spacer.AddComponent<LayoutElement>().preferredWidth = 20;
spacer.SetActive(true);

// Add Open Buffs quick button
AddButton("openbuffs.tooltip.header".i8(), "openbuffs.tooltip.desc".i8(), openBuffsSprites, () => {
    // Open the spellbook screen if not already open
    if (SpellbookController == null) return;

    // Simulate clicking the spellbook button to open it, then toggle to buff view
    var spellScreen = UIHelpers.SpellbookScreen?.gameObject;
    if (spellScreen == null) return;

    // Check if spellbook screen is active
    var serviceWindow = spellScreen.transform.parent;
    bool spellbookOpen = serviceWindow != null && serviceWindow.gameObject.activeSelf;

    if (!spellbookOpen) {
        // Open the spellbook via the game's service window system
        Game.Instance.UI.Canvas.transform
            .Find("NestedCanvas1/ActionBarPcView")
            ?.GetComponent<ActionBarPCView>()
            ?.m_SpellbookButton?.OnLeftClick?.Invoke();
    }

    // Now toggle to buff mode
    if (!SpellbookController.Buffing) {
        SpellbookController.ToggleBuffMode();
    }
});
```

**IMPORTANT**: The above logic for opening the spellbook is a best guess. The implementer MUST read the code to find how the spellbook screen is actually opened. Key things to check:
- `BubbleBuffSpellbookController` has a `PartyView` field of type `PartyPCView` — the `HideAnimation` method toggles the party view
- The existing toggle button calls `PartyView.HideAnimation(!Buffing)` to enter buff mode
- The spellbook screen must be open first for the `BubbleBuffSpellbookController` to exist

A simpler approach might be: the button calls the same code as the IngameMenu spellbook button to open the spellbook, waits a frame, then triggers the buff toggle. Or even simpler: if `SpellbookController` exists, just call the toggle directly.

Read `BubbleBuffSpellbookController` class (around line 78-280 in BubbleBuffer.cs) to understand the full lifecycle.

**Step 4: Add a ToggleBuffMode helper**

If `BubbleBuffSpellbookController` doesn't already have a public method to enter buff mode directly, add one:

```csharp
public void ToggleBuffMode() {
    PartyView.HideAnimation(!Buffing);
    if (Buffing) {
        WasMainShown = MainContainer.activeSelf;
        if (WasMainShown)
            FadeOut(MainContainer);
        else
            FadeOut(NoSpellbooksContainer);
        MainContainer.SetActive(false);
        NoSpellbooksContainer.SetActive(false);
        ShowBuffWindow();
    } else {
        Hide();
        if (WasMainShown) {
            FadeIn(MainContainer);
            MainContainer.SetActive(true);
        } else {
            FadeIn(NoSpellbooksContainer);
            NoSpellbooksContainer.SetActive(true);
        }
    }
}
```

This is essentially the same code as the existing ToggleButton's OnLeftClick listener.

**Step 5: Add localization keys**

In `en_GB.json`:
```json
"openbuffs.tooltip.header": "Open Buff Menu",
"openbuffs.tooltip.desc": "Directly open the BubbleBuffs configuration menu"
```

In `de_DE.json`:
```json
"openbuffs.tooltip.header": "Buff-Menu offnen",
"openbuffs.tooltip.desc": "BubbleBuffs-Konfigurationsmenu direkt offnen"
```

Add the same keys to `fr_FR.json`, `ru_RU.json`, `zh_CN.json` with appropriate translations.

**Step 6: Build**

```bash
$HOME/.dotnet/dotnet build BubbleBuffs/BubbleBuffs.csproj
```

**Step 7: Commit**

```bash
git add -A && git commit -m "feat: add quick open button to directly open buff menu from HUD

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### Task 3: Build, Deploy, and Verify

**Step 1: Full build**

```bash
$HOME/.dotnet/dotnet build BubbleBuffs/BubbleBuffs.csproj
```

**Step 2: Deploy to Steam Deck**

```bash
scp BubbleBuffs/bin/Debug/BubbleBuffs.dll "deck-direct:/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/BubbleBuffs/BubbleBuffs.dll"
```

Also deploy the new icon assets:
```bash
scp BubbleBuffs/Assets/icons/open_buffs_*.png "deck-direct:/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/BubbleBuffs/Assets/icons/"
```

**Step 3: Verify**

```bash
ssh deck-direct "ls -la '/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/BubbleBuffs/BubbleBuffs.dll'"
```

---

## Testing Checklist (Manual, In-Game)

1. **Button visible:** 4th button appears in HUD with gap separator from the 3 buff group buttons
2. **Tooltip:** Hovering shows "Open Buff Menu" tooltip
3. **Click opens buff menu:** Clicking the button opens the buff configuration menu directly
4. **Click from any screen:** Button works even when spellbook is not currently open
5. **Toggle behavior:** Clicking again closes the buff menu
6. **Existing buttons unaffected:** Normal/Quick/Important buff buttons still work as before
