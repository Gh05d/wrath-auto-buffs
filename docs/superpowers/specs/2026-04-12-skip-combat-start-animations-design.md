# Skip Animations on Combat Start

## Problem

`ExecuteCombatStart()` (triggered when combat begins, applying all buffs marked with `CastOnCombatStart`) currently hardcodes the `AnimatedExecutionEngine` at `BuffExecutor.cs:553`. This runs cast animations and waits out their duration even though the goal of combat-start auto-cast is to have the buffs already up by the time the player regains control. On parties with many combat-start buffs this produces a noticeable delay at the start of every fight.

The regular "Apply Buffs" flow already supports an instant mode via `InstantExecutionEngine`, chosen by the `VerboseCasting` setting. Combat start has no equivalent escape hatch — it's animated regardless of the user's global preference, and there is no way to get instant combat-start while keeping animated normal buffing.

## Goal

Add a user-facing setting that lets combat-start auto-cast run through `InstantExecutionEngine` instead of `AnimatedExecutionEngine`. Default off to preserve existing behavior.

## Non-Goals

- No changes to the normal `Execute()` flow. It keeps honoring `VerboseCasting`.
- No changes to Phase 0 (activatables/songs) of `ExecuteCombatStart()`. Those already apply instantly via `IsOn = true` / `TryStart()` — there is no animation to skip.
- No changes to `InstantExecutionEngine` itself. It is already used in production for the normal flow and works with all source types.
- No migration of existing save files. A new `bool` field defaults to `false`, which matches current behavior for every existing user.

## Design

### State

New field on `SavedBufferState` in `BuffIt2TheLimit/SaveState.cs`, placed alongside the other casting/UI toggles:

```csharp
[JsonProperty]
public bool SkipAnimationsOnCombatStart;
```

`bool` defaults to `false`, and Newtonsoft.Json will deserialize a missing key as `false`. No `[DefaultValue]` attribute or explicit migration needed.

### Property wrapper

New property on `BufferState` in `BuffIt2TheLimit/BufferState.cs`, mirroring the existing `VerboseCasting` wrapper at line 613:

```csharp
public bool SkipAnimationsOnCombatStart {
    get => SavedState.SkipAnimationsOnCombatStart;
    set {
        SavedState.SkipAnimationsOnCombatStart = value;
        Save(true);
    }
}
```

The `Save(true)` call mirrors other settings that want an immediate disk write rather than a batched save.

### Engine selection

Replace the hardcoded engine at `BuffIt2TheLimit/BuffExecutor.cs:553`:

```csharp
// before
var engine = new AnimatedExecutionEngine();

// after
IBuffExecutionEngine engine = State.SkipAnimationsOnCombatStart
    ? new InstantExecutionEngine()
    : new AnimatedExecutionEngine();
```

Both engines implement `IBuffExecutionEngine`, so `CreateSpellCastRoutine(tasks)` works unchanged on line 554.

### Settings UI

New toggle in the settings panel in `BuffIt2TheLimit/BubbleBuffer.cs`, placed immediately after the existing `setting-verbose` toggle block (current location ~line 749). Uses the existing `MakeSettingsToggle` helper:

```csharp
{
    var (toggle, _) = MakeSettingsToggle(togglePrefab, panel.transform, "setting-skip-combat-anim".i8());
    toggle.isOn = state.SkipAnimationsOnCombatStart;
    toggle.onValueChanged.AddListener(enabled => {
        state.SkipAnimationsOnCombatStart = enabled;
    });
}
```

No explicit `state.Save(...)` call in the listener — the property setter handles persistence.

### Localization

Add new key `setting-skip-combat-anim` to the locale files that are already kept in sync:

- `en_GB.json`: `"Skip animations on combat start"`
- `de_DE.json`: `"Animationen bei Combat-Start überspringen"` (technical term "Combat-Start" stays English, consistent with the de_DE convention documented in CLAUDE.md)

`fr_FR`, `ru_RU`, `zh_CN` are already incomplete relative to EN/DE — do not add the key there. The `i8()` extension falls back to EN for missing keys, so those locales will show the English string. This matches the current state of the locale files.

## Behavior

| `VerboseCasting` | `SkipAnimationsOnCombatStart` | Normal "Apply Buffs" | Combat Start auto-cast |
|---|---|---|---|
| false | false | instant | animated (current default) |
| true  | false | animated | animated |
| false | true  | instant | instant |
| true  | true  | animated | instant |

The two settings are orthogonal on purpose: a user who enjoys watching animated casting during pre-buff setup can still get an instant combat start.

## Testing

No unit tests in this repo. Verify by:

1. Build with `~/.dotnet/dotnet build BuffIt2TheLimit/BuffIt2TheLimit.csproj -p:SolutionDir=$(pwd)/`.
2. Deploy with `./deploy.sh`.
3. Restart game, open settings, confirm new toggle is visible and labeled correctly in EN and DE.
4. With toggle off (default): mark one buff with `CastOnCombatStart`, trigger combat, observe cast animation plays — behavior unchanged.
5. With toggle on: same setup, trigger combat, observe buff is already applied before the player regains control and no animation plays.
6. With toggle on and multiple combat-start buffs across the party: all apply simultaneously (batched by `InstantExecutionEngine.BATCH_SIZE = 8`).
7. Confirm the toggle state persists across save/load (value is written to `SavedBufferState` JSON on disk).

## Risks

- **`InstantExecutionEngine` compatibility edge cases**: It is already used in production for the normal `Execute()` flow, including spells, scrolls, potions, wands, extend rods and metamagic. No new source types are introduced by combat-start. Risk is near-zero.
- **Setting placement in panel**: The settings panel is a vertical layout group. Adding one toggle between existing ones will slightly shift the lower entries — expected and matches how new settings have been added historically.
- **Locale fallback**: Users on `fr_FR` / `ru_RU` / `zh_CN` will see the English label. This is already the pattern for several existing keys and is the documented approach.

## Files Touched

- `BuffIt2TheLimit/SaveState.cs` — add field
- `BuffIt2TheLimit/BufferState.cs` — add property wrapper
- `BuffIt2TheLimit/BuffExecutor.cs` — conditional engine selection
- `BuffIt2TheLimit/BubbleBuffer.cs` — new settings toggle
- `BuffIt2TheLimit/Config/en_GB.json` — new key
- `BuffIt2TheLimit/Config/de_DE.json` — new key
