[中文](../README.md) | EN

# Gaoshou — Slay the Spire 2 Character Mod

> **Work In Progress**: This mod is still under **active development** — bugs are likely, and **card balance is not final**. Feedback is welcome.

## About

"Gaoshou" is a character mod for **[Slay the Spire 2](https://store.steampowered.com/app/2868840/Slay_the_Spire_2/)**, built on the **RitsuLib 0.5.x** framework (C# / Harmony / Godot asset pipeline). It adds:

- A new character "Gaoshou" with its own card pool, relics and powers
- Dual-resource system: **Energy** and **Stars** costs
- Signature keywords: **Flow** (triggers when played after a card of a different color), **Storm** (replays when resource thresholds are met), **Amplify**, **Miracle** (triggers when entering hand outside the turn-start draw), **Stockpile**, and more
- Card art recolored/re-rendered from the source-game assets (gradient & radial themes)

## Gameplay Overview

- Spend **Energy** and **Stars** to play cards; some cards cost Stars, so plan your resources.
- Card colors drive **Flow** chains and **Storm** checks — build combos for repeat plays.
- **Miracle**: cards that enter your hand through generation/retention (not the turn-start draw) trigger extra effects when played.
- Mix archetypes: Red (offense) / Blue (control) / Purple (growth) / Green (survival).

## Structure

```
GaoshouCode/   C# sources (cards / relics / powers / patches)
Gaoshou/       resource root (localization, images, scenes)
```

## Credits

- **Assets**: Card art and icons are extracted from **[Diceomancer](https://store.steampowered.com/app/2501600/_/)**. **All assets belong to their original creators**; this project is a fan rework for learning purposes only.
- **AssetRipper**: asset extraction — https://github.com/AssetRipper/AssetRipper
- **RitsuLib**: mod framework — https://github.com/Ritsu-Ritsu/RitsuLib
- **LexNinja2**: framework & card-pool injection references — https://github.com/Flimsyyy/LexNinja2
- **Well Laid Plans Multiplayer**: multiplayer patch references — https://github.com/Redem714233/WellLaidPlansMultiplayer

## Feedback

- 🐛 Bugs? Please follow the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md).
- 💡 Ideas? Please follow the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md).
- ⚖️ Balance opinions are welcome in feature requests too.