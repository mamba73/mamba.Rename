# 🚀 Space Engineers: Advanced Block Renaming (mamba.Rename)

An advanced renaming utility for **Space Engineers** (ModAPI) that streamlines grid organization through directional thruster templates, sequential numbering, and powerful Regex pattern matching.

[![Steam Workshop](https://img.shields.io)](https://steamcommunity.com/sharedfiles/filedetails/?id=3630278266)

## 🆕 Latest Update (v1.5.30)
- **Fixes:** Resolved conflicts with 'Precision Mode'. Aggressive UI refresh implemented for 'grayed out' controls.
- **UX:** Added 'Tips & Tricks' tooltip on the visibility checkbox.
- **Cleanup:** Removed debug telemetry for better performance.
- Full history available in [CHANGELOG.md](CHANGELOG.md).

## 🌟 Key Features
- **Directional Thruster Renaming**: Renames based on thrust direction (F, B, U, D, L, R) using "{0}" template.
- **Sequential Numbering**: Advanced numbering with adjustable digit format (e.g., "001").
- **Auto-Continue & Grouping**: Smart counters per grid/block type.
- **Regex Renaming**: Powerful bulk-replace using .NET syntax.

## ⚠️ Important: Terminal Input Limitations
**Note for non-US Keyboard users (e.g., Croatian, German, etc.):** The SE terminal **does not support** characters requiring **AltGr** (e.g., `\ | [ ] { } < >`).
- **Workaround:** Type patterns in Notepad and use **Ctrl+V** to paste them into the game's textbox.

## ⚙️ Configuration & Usage
Select one or more blocks in the terminal. Click **"Show Rename Panel"** to reveal controls:
1. **New Naming**: Basic Prefix/Suffix/Replace.
2. **Counter Setup**: Define format (e.g., `001`) and separator.
3. **Regex**: Enter pattern in 'Find' and new string in 'Replace'.
> **Tip:** If the UI feels 'stuck' or grayed out, select another block and click back to force a refresh.

## 🛠 Technical Details
- **Language**: C# 6.0 / .NET Framework 4.8
- **Sync**: Client-side optimized; compatible with Singleplayer, Torch+Pulsar, and MP.
- **API**: Uses `Sandbox.ModAPI` and `VRage.Game.ModAPI`.

## 🚀 Upcoming Features (Roadmap)
- [ ] **Undo Last Operation**: Revert accidental mass changes.
- [ ] **Sort & Number**: Sort blocks by world position (X/Y/Z) or Name.
- [ ] **Preview / Test Mode**: See "Old -> New" list before applying.
- [ ] **Global Grid Filter**: Rename everything matching a term without manual selection.

---
See [CHANGELOG.md](CHANGELOG.md) for version history.

---
*Developed by [mamba73](https://github.com/mamba73). Feel free to submit issues or pull requests!*

[Buy Me a Coffee ☕](https://buymeacoffee.com/mamba73)