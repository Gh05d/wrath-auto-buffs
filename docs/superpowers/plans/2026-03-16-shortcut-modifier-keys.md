# Shortcut Modifier Keys & Open Buff Menu Shortcut — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend all keyboard shortcuts to support modifier combos (Ctrl+Shift+Alt) and add a configurable shortcut to open the buff menu.

**Architecture:** New `ShortcutBinding` readonly struct replaces `KeyCode` throughout the shortcut system. A `ShortcutBindingConverter` handles backward-compatible JSON deserialization. The capture and execution logic in `BubbleBuffGlobalController` is simplified (bool + closure instead of `BuffGroup?`). An `OpenBuffMenu()` method is extracted from the HUD button lambda for reuse by the shortcut.

**Tech Stack:** C#/.NET 4.8.1, Unity (Input API), Newtonsoft.Json, HarmonyLib

**Spec:** `docs/superpowers/specs/2026-03-16-shortcut-modifier-keys-design.md`

**Build command:** `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/`

**Known limitation:** All shortcuts (group + open-menu) require the spellbook to have been opened at least once in the session, since `BufferState` (which holds the bindings) is initialized when the spellbook UI first loads. This is pre-existing behavior for group shortcuts; the open-menu shortcut inherits the same constraint.

**Commit strategy:** Tasks 2–5 modify interdependent files. Do NOT commit individually — commit once after Task 5 when the build is green. Task 1 and Task 6 are standalone commits.

---

## Chunk 1: ShortcutBinding Struct & JSON Converter

### Task 1: Create ShortcutBinding struct and ShortcutBindingConverter

**Files:**
- Create: `BuffIt2TheLimit/ShortcutBinding.cs`

- [ ] **Step 1: Create `ShortcutBinding.cs` with the readonly struct**

```csharp
using BuffIt2TheLimit.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuffIt2TheLimit {

    [JsonConverter(typeof(ShortcutBindingConverter))]
    public readonly struct ShortcutBinding {
        public readonly KeyCode Key;
        public readonly bool Ctrl;
        public readonly bool Shift;
        public readonly bool Alt;

        public ShortcutBinding(KeyCode key, bool ctrl = false, bool shift = false, bool alt = false) {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        public bool IsNone => Key == KeyCode.None;

        public bool IsPressed() {
            if (Key == KeyCode.None) return false;
            if (!Input.GetKeyDown(Key)) return false;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            return Ctrl == ctrl && Shift == shift && Alt == alt;
        }

        public static ShortcutBinding None => new(KeyCode.None);

        public static ShortcutBinding Capture(KeyCode key) {
            return new ShortcutBinding(
                key,
                ctrl: Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                shift: Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                alt: Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
            );
        }

        public string ToDisplayString() {
            if (Key == KeyCode.None) return "shortcut.none".i8();
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt) parts.Add("Alt");
            parts.Add(Key.ToString());
            return string.Join("+", parts);
        }
    }

    public class ShortcutBindingConverter : JsonConverter<ShortcutBinding> {
        public override ShortcutBinding ReadJson(JsonReader reader, Type objectType, ShortcutBinding existingValue, bool hasExistingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.String) {
                // Old format: bare KeyCode string like "F5"
                var str = (string)reader.Value;
                if (Enum.TryParse<KeyCode>(str, out var kc))
                    return new ShortcutBinding(kc);
                return ShortcutBinding.None;
            }
            if (reader.TokenType == JsonToken.StartObject) {
                var obj = JObject.Load(reader);
                var key = KeyCode.None;
                if (obj.TryGetValue("Key", out var keyToken) && Enum.TryParse<KeyCode>(keyToken.ToString(), out var parsed))
                    key = parsed;
                bool ctrl = obj.Value<bool?>("Ctrl") ?? false;
                bool shift = obj.Value<bool?>("Shift") ?? false;
                bool alt = obj.Value<bool?>("Alt") ?? false;
                return new ShortcutBinding(key, ctrl, shift, alt);
            }
            return ShortcutBinding.None;
        }

        public override void WriteJson(JsonWriter writer, ShortcutBinding value, JsonSerializer serializer) {
            writer.WriteStartObject();
            writer.WritePropertyName("Key");
            writer.WriteValue(value.Key.ToString());
            writer.WritePropertyName("Ctrl");
            writer.WriteValue(value.Ctrl);
            writer.WritePropertyName("Shift");
            writer.WriteValue(value.Shift);
            writer.WritePropertyName("Alt");
            writer.WriteValue(value.Alt);
            writer.WriteEndObject();
        }
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`
Expected: Build succeeds (new file is not yet referenced, but must compile on its own)

- [ ] **Step 3: Commit**

```bash
git add BuffIt2TheLimit/ShortcutBinding.cs
git commit -m "feat: add ShortcutBinding struct with modifier support and JSON converter"
```

---

## Chunk 2: SaveState & BufferState Changes

### Task 2: Update SavedBufferState and BufferState to use ShortcutBinding

**Files:**
- Modify: `BuffIt2TheLimit/SaveState.cs:46` — change `ShortcutKeys` type, add `OpenBuffMenuKey`
- Modify: `BuffIt2TheLimit/BufferState.cs:409-418` — update Get/Set signatures, add open-menu methods

- [ ] **Step 1: Update `SaveState.cs`**

Change line 46 from:
```csharp
public Dictionary<BuffGroup, KeyCode> ShortcutKeys = new();
```
to:
```csharp
public Dictionary<BuffGroup, ShortcutBinding> ShortcutKeys = new();
[JsonProperty]
public ShortcutBinding OpenBuffMenuKey;
```

- [ ] **Step 2: Update `BufferState.cs` methods**

Replace `GetShortcut` and `SetShortcut` (lines 409-418):

```csharp
public ShortcutBinding GetShortcut(BuffGroup group) =>
    SavedState.ShortcutKeys.TryGetValue(group, out var binding) ? binding : ShortcutBinding.None;

public void SetShortcut(BuffGroup group, ShortcutBinding binding) {
    if (binding.IsNone)
        SavedState.ShortcutKeys.Remove(group);
    else
        SavedState.ShortcutKeys[group] = binding;
    Save(true);
}

public ShortcutBinding GetOpenBuffMenuShortcut() => SavedState.OpenBuffMenuKey;

public void SetOpenBuffMenuShortcut(ShortcutBinding binding) {
    SavedState.OpenBuffMenuKey = binding;
    Save(true);
}
```

---

## Chunk 3: BubbleBuffGlobalController — Capture & Execution

### Task 3: Rewrite capture and execution logic in BubbleBuffGlobalController

**Files:**
- Modify: `BuffIt2TheLimit/BuffExecutor.cs:28-111` — rewrite capture state, modifier filter, execution loop

- [ ] **Step 1: Update capture state fields (lines 35-41)**

Replace:
```csharp
// Shortcut capture state
public static BuffGroup? CapturingFor = null;
public static Action<BuffGroup, KeyCode> OnShortcutCaptured = null;

// Cached enum arrays to avoid per-frame allocation in Update()
private static readonly KeyCode[] KeyboardKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
    .TakeWhile(kc => kc < KeyCode.Mouse0).ToArray();
private static readonly BuffGroup[] BuffGroups = (BuffGroup[])Enum.GetValues(typeof(BuffGroup));
```

With:
```csharp
// Shortcut capture state
public static bool CapturingActive = false;
public static Action<ShortcutBinding> OnShortcutCaptured = null;

// Cached enum arrays to avoid per-frame allocation in Update()
private static readonly HashSet<KeyCode> ModifierKeys = new() {
    KeyCode.LeftShift, KeyCode.RightShift,
    KeyCode.LeftControl, KeyCode.RightControl,
    KeyCode.LeftAlt, KeyCode.RightAlt,
    KeyCode.LeftCommand, KeyCode.RightCommand,
    KeyCode.LeftApple, KeyCode.RightApple,
};
private static readonly KeyCode[] KeyboardKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
    .TakeWhile(kc => kc < KeyCode.Mouse0)
    .Where(kc => !ModifierKeys.Contains(kc))
    .ToArray();
private static readonly BuffGroup[] BuffGroups = (BuffGroup[])Enum.GetValues(typeof(BuffGroup));
```

- [ ] **Step 2: Rewrite the capture and execution section of Update() (lines 89-110)**

Replace:
```csharp
// Handle keyboard shortcut capture and execution
var state = GlobalBubbleBuffer.Instance?.SpellbookController?.state;
if (state != null) {
    if (CapturingFor.HasValue) {
        foreach (KeyCode kc in KeyboardKeys) {
            if (Input.GetKeyDown(kc)) {
                var captured = kc == KeyCode.Escape ? KeyCode.None : kc;
                OnShortcutCaptured?.Invoke(CapturingFor.Value, captured);
                CapturingFor = null;
                OnShortcutCaptured = null;
                break;
            }
        }
    } else {
        foreach (BuffGroup group in BuffGroups) {
            var kc = state.GetShortcut(group);
            if (kc != KeyCode.None && Input.GetKeyDown(kc)) {
                GlobalBubbleBuffer.Execute(group);
            }
        }
    }
}
```

With:
```csharp
// Handle keyboard shortcut capture
if (CapturingActive) {
    foreach (KeyCode kc in KeyboardKeys) {
        if (Input.GetKeyDown(kc)) {
            var binding = kc == KeyCode.Escape ? ShortcutBinding.None : ShortcutBinding.Capture(kc);
            OnShortcutCaptured?.Invoke(binding);
            CapturingActive = false;
            OnShortcutCaptured = null;
            break;
        }
    }
} else {
    // Handle buff group shortcut execution
    var state = GlobalBubbleBuffer.Instance?.SpellbookController?.state;
    if (state != null) {
        foreach (BuffGroup group in BuffGroups) {
            var binding = state.GetShortcut(group);
            if (binding.IsPressed()) {
                GlobalBubbleBuffer.Execute(group);
            }
        }

        // Handle open-buff-menu shortcut
        if (state.GetOpenBuffMenuShortcut().IsPressed()) {
            instance?.OpenBuffMenu();
        }
    }
}
```

Note: `instance` is already declared on line 53 (`var instance = GlobalBubbleBuffer.Instance;`), so we reuse it.

---

## Chunk 4: BubbleBuffer.cs — Extract OpenBuffMenu, Update MakeKeybindRow, Add New Shortcut Row

### Task 4: Extract OpenBuffMenu method on GlobalBubbleBuffer

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:2420-2450` — extract method, update HUD button to call it

- [ ] **Step 1: Add `OpenBuffMenu()` method to `GlobalBubbleBuffer` class**

Add this method near the existing `ResetPendingState()` (after line 2156):

```csharp
public void OpenBuffMenu() {
    try {
        // If already in buff mode, do nothing (open only, no toggle)
        if (SpellbookController != null && SpellbookController.IsReady && SpellbookController.Buffing)
            return;

        var serviceWindow = UIHelpers.ServiceWindow;
        var spellScreen = serviceWindow != null ? serviceWindow.Find(UIHelpers.WidgetPaths.SpellScreen) : null;
        bool spellbookVisible = spellScreen != null && spellScreen.gameObject.activeInHierarchy;

        if (spellbookVisible && SpellbookController != null && SpellbookController.IsReady) {
            SpellbookController.ToggleBuffMode();
        } else {
            var staticRoot = Game.Instance.UI.Canvas.transform;
            var spellbookButton = staticRoot.Find("NestedCanvas1/IngameMenuView/ButtonsPart/Container/SpellBookButton")
                ?.GetComponentInChildren<OwlcatButton>();
            if (spellbookButton != null) {
                PendingOpenBuffMode = true;
                pendingFrameCount = 0;
                spellbookButton.OnLeftClick.Invoke();
            } else {
                Main.Log("BuffIt2TheLimit: Could not find SpellBookButton in IngameMenuView");
            }
        }
    } catch (Exception ex) {
        Main.Error(ex, "Open Buffs");
    }
}
```

- [ ] **Step 2: Update the HUD button lambda (lines 2420-2450) to call `OpenBuffMenu()`**

Replace the `AddButton` lambda body (the entire `() => { try { ... } catch ... }` block):
```csharp
AddButton("openbuffs.tooltip.header".i8(), "openbuffs.tooltip.desc".i8(), openBuffsSprites, () => {
    OpenBuffMenu();
});
```

### Task 5: Generalize MakeKeybindRow and add open-buff-menu shortcut row

**Files:**
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:618-655` — change signature to use getter/setter lambdas
- Modify: `BuffIt2TheLimit/BubbleBuffer.cs:837-842` — update call sites, add new row

- [ ] **Step 1: Update `MakeKeybindRow` signature and body (lines 618-655)**

Replace the entire method:
```csharp
private void MakeKeybindRow(Transform parent, string labelText, Func<ShortcutBinding> getter, Action<ShortcutBinding> setter) {
    var row = new GameObject($"keybind-row", typeof(RectTransform));
    row.transform.SetParent(parent, false);
    var hg = row.AddComponent<HorizontalLayoutGroup>();
    hg.childControlHeight = true;
    hg.childControlWidth = true;
    hg.childForceExpandWidth = false;
    hg.spacing = 8;

    var labelObj = new GameObject("label", typeof(RectTransform));
    labelObj.transform.SetParent(row.transform, false);
    var labelLE = labelObj.AddComponent<LayoutElement>();
    labelLE.flexibleWidth = 1;
    var lText = labelObj.AddComponent<TextMeshProUGUI>();
    lText.text = labelText;
    lText.fontSize = 14;
    lText.color = new Color(0.2f, 0.2f, 0.2f);
    lText.alignment = TextAlignmentOptions.MidlineLeft;

    var btnObj = UnityEngine.Object.Instantiate(buttonPrefab, row.transform);
    var btnLE = btnObj.GetComponent<LayoutElement>() ?? btnObj.AddComponent<LayoutElement>();
    btnLE.preferredWidth = 120;
    btnLE.flexibleWidth = 0;
    var btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
    btnText.text = getter().ToDisplayString();

    var btn = btnObj.GetComponent<OwlcatButton>();
    btn.OnLeftClick.AddListener(() => {
        if (BubbleBuffGlobalController.CapturingActive) return;
        btnText.text = "shortcut.press".i8();
        BubbleBuffGlobalController.CapturingActive = true;
        BubbleBuffGlobalController.OnShortcutCaptured = (binding) => {
            setter(binding);
            btnText.text = binding.ToDisplayString();
        };
    });
}
```

- [ ] **Step 2: Update call sites in settings panel (lines 837-842)**

Replace:
```csharp
// Keyboard shortcut per group
foreach (BuffGroup group in Enum.GetValues(typeof(BuffGroup))) {
    var groupCopy = group;
    var key = $"shortcut.{group.ToString().ToLower()}";
    MakeKeybindRow(panel.transform, key.i8(), groupCopy);
}
```

With:
```csharp
// Keyboard shortcut per group
foreach (BuffGroup group in Enum.GetValues(typeof(BuffGroup))) {
    var groupCopy = group;
    var key = $"shortcut.{group.ToString().ToLower()}";
    MakeKeybindRow(panel.transform, key.i8(),
        () => state.GetShortcut(groupCopy),
        binding => state.SetShortcut(groupCopy, binding));
}

// Open buff menu shortcut
MakeKeybindRow(panel.transform, "shortcut.openbuffmenu".i8(),
    () => state.GetOpenBuffMenuShortcut(),
    binding => state.SetOpenBuffMenuShortcut(binding));
```

- [ ] **Step 3: Build**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`
Expected: Build succeeds — all references are now consistent.

- [ ] **Step 4: Commit Tasks 2–5 together**

```bash
git add BuffIt2TheLimit/SaveState.cs BuffIt2TheLimit/BufferState.cs BuffIt2TheLimit/BuffExecutor.cs BuffIt2TheLimit/BubbleBuffer.cs
git commit -m "feat: modifier key support for shortcuts, add open-buff-menu shortcut"
```

---

## Chunk 5: Locale Files

### Task 6: Update all locale files

**Files:**
- Modify: `BuffIt2TheLimit/Config/en_GB.json:104` — update `shortcut.press`, add `shortcut.openbuffmenu`
- Modify: `BuffIt2TheLimit/Config/de_DE.json` — same
- Modify: `BuffIt2TheLimit/Config/fr_FR.json` — same
- Modify: `BuffIt2TheLimit/Config/ru_RU.json` — same
- Modify: `BuffIt2TheLimit/Config/zh_CN.json` — same

**Note:** In all locale files, `shortcut.press` is the **last entry** before `}`. The new `shortcut.openbuffmenu` becomes the new last entry. The replacement adds a comma after `shortcut.press` and the new entry has NO trailing comma.

- [ ] **Step 1: Update `en_GB.json`**

Replace:
```json
  "shortcut.press": "(press any key)"
}
```
With:
```json
  "shortcut.press": "(press shortcut)",
  "shortcut.openbuffmenu": "Open buff menu shortcut:"
}
```

- [ ] **Step 2: Update `de_DE.json`**

Replace:
```json
  "shortcut.press": "(Taste drücken)"
}
```
With:
```json
  "shortcut.press": "(Shortcut drücken)",
  "shortcut.openbuffmenu": "Open buff menu shortcut:"
}
```

- [ ] **Step 3: Update `fr_FR.json`**

Replace:
```json
  "shortcut.press": "(press any key)"
}
```
With:
```json
  "shortcut.press": "(press shortcut)",
  "shortcut.openbuffmenu": "Open buff menu shortcut:"
}
```

- [ ] **Step 4: Update `ru_RU.json`**

Replace:
```json
  "shortcut.press": "(press any key)"
}
```
With:
```json
  "shortcut.press": "(press shortcut)",
  "shortcut.openbuffmenu": "Open buff menu shortcut:"
}
```

- [ ] **Step 5: Update `zh_CN.json`**

Replace:
```json
  "shortcut.press": "(press any key)"
}
```
With:
```json
  "shortcut.press": "(press shortcut)",
  "shortcut.openbuffmenu": "Open buff menu shortcut:"
}
```

- [ ] **Step 6: Build**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`
Expected: Build succeeds.

- [ ] **Step 7: Commit**

```bash
git add BuffIt2TheLimit/Config/
git commit -m "feat: add open-buff-menu locale keys, update shortcut.press text"
```

---

## Chunk 6: Final Verification

### Task 7: Full build and deploy test

- [ ] **Step 1: Clean build**

Run: `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/ --nologo`
Expected: Build succeeds with no errors, no new warnings.

- [ ] **Step 2: Deploy to Steam Deck**

Run: `./deploy.sh`
Expected: DLL + Info.json deployed.

- [ ] **Step 3: Manual testing checklist**

Test in-game:
1. Open settings panel → verify "Open buff menu shortcut:" row appears
2. Click the new row button → press Ctrl+B → verify display shows "Ctrl+B"
3. Press Ctrl+B in-game → buff menu opens
4. Press Ctrl+B again while buff menu is open → nothing happens (no toggle)
5. Open settings → click a group shortcut → press Shift+F5 → verify display shows "Shift+F5"
6. Press Shift+F5 → verify group executes
7. Press F5 alone → verify group does NOT execute (modifier mismatch)
8. Open settings → click a shortcut → press Escape → verify display shows "(none)"
9. Load a save with old-format shortcuts → verify they still work (backward compat)
