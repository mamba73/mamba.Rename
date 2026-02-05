# 🚀 Space Engineers: Advanced Block Renaming (mamba.Rename)

An advanced renaming utility for **Space Engineers** (ModAPI) that streamlines grid organization through directional thruster templates, sequential numbering, and powerful Regex pattern matching.

[![Steam Workshop](https://img.shields.io)](https://steamcommunity.com/sharedfiles/filedetails/?id=3630278266)
[![API](https://img.shields.io)](https://github.com)

## 🌟 Key Features

- **Directional Thruster Renaming**: Automatically detects thruster orientation and renames them using a template (e.g., `Thruster {0}` → `Thruster F`, `Thruster B`).
- **Smart Sequential Numbering**: Supports per-grid or per-block-type numbering with custom separators and auto-continue logic.
- **Regex Search & Replace**: Bulk-rename blocks using .NET Regular Expressions for complex naming schemes.
- **Multi-Selection Support**: Apply any action to all selected blocks in the terminal simultaneously.
- **Dedicated Server Compatible**: Fully synchronized via networking (Client-Server architecture) to prevent desync.

## 🛠 Technical Details

- **Language**: C# 6.0 / .NET Framework 4.6
- **Context**: Session Component (MySessionComponentBase)
- **API**: Uses `Sandbox.ModAPI` and `VRage.Game.ModAPI`.
- **Synchronization**: Implements secure message handlers for multiplayer stability.

## 📋 How to Install

1. **Steam**: Subscribe to the mod on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3630278266).
2. **Local Dev**: Clone this repository into your `%AppData%\SpaceEngineers\Mods` folder for local testing.

## ⚙️ Configuration & Usage

Once installed, select one or more blocks in the terminal to reveal the **Renaming Controls** at the bottom of the panel:

1. **New Naming**: Basic Prefix/Suffix/Replace.
2. **Counter Setup**: Define format (e.g., `001`) and separator (e.g., `_`).
3. **Directional Template**: Use `{0}` as a placeholder for direction (F, B, L, R, U, D).
4. **Regex**: Enter your pattern in 'Find' and the new string in 'Replace'.

> **Note**: For directional renaming, you must be seated in a cockpit/remote control to establish the grid's forward vector.

## 🚀 Upcoming Features (Roadmap)

- [ ] **Undo Last Operation**: Revert accidental mass changes instantly.
- [ ] **Sort & Number**: Sort blocks by world position (X/Y/Z) before numbering.
- [ ] **Preview Mode**: See changes in a popup before applying them.
- [ ] **Global Filter**: Apply renaming to all blocks on a grid matching a specific keyword.

## ⚠️ Known Limitations
- The SE Terminal does not support **AltGr** input for non-US layouts. Use **Ctrl+V** to paste special characters like `\ | [ ] { }`.

---
*Developed by [mamba73](https://github.com/mamba73). Feel free to submit issues or pull requests!*

[Buy Me a Coffee ☕](https://buymeacoffee.com/mamba73)
