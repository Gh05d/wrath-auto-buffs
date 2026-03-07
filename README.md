# BubbleBuffs (Fork)

A fork of [factubsio's BubbleBuffs](https://github.com/factubsio/BubbleBuffs) — the buff automation mod for **Pathfinder: Wrath of the Righteous**.

## What's Different

This fork continues development of BubbleBuffs with new features:

- **Unified buff sources** — Spells, scrolls, and potions merged into a single "Buffs" tab. One entry per buff regardless of source, with inline source-type controls (checkboxes + priority).
- **Equipment support** — Activatable quickslot items (staves, wands) as buff sources in a dedicated "Equipment" tab.
- **Source-type overlays** — Game-native icons on caster portraits showing whether they're casting from spell, scroll, potion, or equipment.
- **Quick open button** — HUD button to directly open the buff configuration menu without navigating through the spellbook screen first.
- **Renamed buff groups** — "Normal Buffs", "Quick Buffs", "Important Buffs" (clearer labels).

## About

BubbleBuffs adds an in-game option to spellbooks to create buff routines. Configure which buffs to cast on which party members, then execute entire buff sequences with a single click.

Original mod by [factubsio](https://github.com/factubsio/BubbleBuffs) — download the original from [Nexus Mods](https://www.nexusmods.com/pathfinderwrathoftherighteous/mods/195).

## Development Setup

1. Clone this repository
2. Provide game DLLs — either:
   - Set `WrathInstallDir` environment variable to your game install path, or
   - Create `GamePath.props` in the repo root (see [CLAUDE.md](CLAUDE.md) for format)
3. Build:
   ```bash
   dotnet build BubbleBuffs/BubbleBuffs.csproj
   ```
4. Output goes to `BubbleBuffs/bin/Debug/` — copy contents to `{GameDir}/Mods/BubbleBuffs/`

### Debugging

See [Owlcat's modding wiki](https://github.com/spacehamster/OwlcatModdingWiki/wiki/Debugging#debugging-with-visual-studio) for Visual Studio debugging setup with Unity.

## License

[MIT](LICENSE) — originally by Sean Petrie (Vek17) and factubsio (Bubbles).

## Acknowledgments

- [@factubsio](https://github.com/factubsio) for the original BubbleBuffs mod
- [@Balkoth](https://github.com/Balkoth) for Buffbot, the direct inspiration
- [@Vek17](https://github.com/Vek17) for the codebase foundation
- The Pathfinder WotR Discord community
