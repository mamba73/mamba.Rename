## CHANGELOG v1.5.30
 - [FIXED] Resolved compatibility conflicts with other terminal mods (e.g., 'Precision Mode').
 - [FIXED] Aggressive UI refresh implemented to bypass game engine caching issues 
           that caused terminal controls to appear 'grayed out' or unresponsive.
 - [NEW]   Added 'Tips & Tricks' tooltip to the visibility checkbox; hover over 
           it for a quick guide on how to force a layout refresh if needed.
 - [CLEANUP] Removed all internal debug telemetry to ensure a cleaner log file 
             and improved overall mod performance.

## CHANGELOG v1.5.21
 - [FIXED] Show/Hide checkbox now fully hides/shows the panel instantly.
           Replaced the unreliable temporary name-change hack with a strict `b.UpdateVisual()` 
           call to natively force the Space Engineers terminal grid to refresh control visibility.
 - [FIXED] Terminal dividers and separators are now fully visible and consistent.
           Replaced broken `IMyTerminalControlSeparator` elements (which frequently render with 0px height) 
           with dedicated text-based `IMyTerminalControlLabel` boundaries.
 - [FIXED] Resolved UI rendering issues caused by Keen's custom game font restrictions.
           Swapped out non-ASCII box-drawing characters (`═`) with safe, standard ASCII divider lines 
           (`---`) to eliminate unreadable square blocks and UI render glitches.
 - [CHANGED] Decoupled telemetry from hardcoded values. The debugging subsystem now dynamically 
             inherits the mod's target version string directly from the core file's public properties.

## CHANGELOG v1.5.17
 - [FIXED] Show/Hide checkbox now properly refreshes UI when toggled.
           Replaced no-op refresh code with actual temporary name change
           to trigger terminal control visibility re-evaluation.

## CHANGELOG v1.5.16
 - [FIXED] Show/Hide toggle now correctly hides controls instead of disabling them.
           Changed .Enabled to .Visible for all conditional controls.
 - [FIXED] Main separator is now placed BEFORE the Show/Hide checkbox to properly
           separate it from the rest of the renaming controls.
 - [FIXED] Removed trailing spaces from control IDs to prevent potential UI issues.
 - [FIXED] Cleaned up syntax errors and stray spaces in the codebase.

## CHANGELOG v1.5.15
 - [FIXED] Terminal controls now safely append without overriding other mods' controls.
           Controls are created with unique IDs and checked for duplicates before adding.
 - [NEW] Added "Show Rename Panel" checkbox to hide/show all custom renaming controls.
           This allows users to clean up their terminal UI when not using the rename features.
 - [FIXED] Rename controls now actually hidden by default (visible defaulted to true,
           which did not fix the reported Services Terminal / Precision Mode conflict).
           Also replaced two duplicated C# 7 local functions (IsPanelVisible) — not
           supported under this project's C# 6.0/.NET 4.8 target — with a single
           class-level method.

## CHANGELOG v1.5.14
 - [NEW] ApplyAction() method extracted from OnMessageReceived to avoid code duplication.
 - [FIXED] SendNetworkRequest() now always applies actions directly via ApplyAction()
           instead of routing through a custom network layer.
           Root cause: Pulsar is a client-side plugin — the server never has the mod's
           NETWORK_ID handler registered, so all network messages were silently dropped.
           Space Engineers natively syncs CustomName between client and server, so
           direct application works correctly in singleplayer, Torch+Pulsar, and MP.
 - [CHANGED] OnMessageReceived() now delegates to ApplyAction() instead of
             containing its own switch block. Kept registered but effectively unused.

## CHANGELOG v1.5.12
- [NEW] Extracted logic into `ApplyAction()` to eliminate code duplication
between network events and local execution.
- [FIXED] Fixed Pulsar/Singleplayer compatibility: `SendNetworkRequest()` now
detects host status (`IsServer`). Actions are applied locally
bypassing the network layer, preventing commands from being
dropped by the Keen network handler.
- [CHANGED] Refactored `OnMessageReceived()` to delegate all logic to
`ApplyAction()`, ensuring consistent behavior across all
deployment environments.

## CHANGELOG v1.5.11
- [IMPROVED] Number Separator now supports empty strings, allowing for
compact sequential naming without delimiters (e.g., "Battery01"
instead of "Battery 01").

## CHANGELOG v1.5.4
- [CHANGED] Updated workshop description to include documentation for newly
implemented features and improved usage examples.

## CHANGELOG v1.5.3
- [NEW] Auto-Continue Numbering: Added support to toggle between continuous
numbering and resetting to format when starting a new batch.
- [NEW] Group by Block Type: Counters now operate independently per block
type (e.g., Batteries and Thrusters maintain separate counts).
- [NEW] Custom Number Separator: Users can now define custom delimiters
between the base name and the number (space, dash, underscore, etc.).
- [NEW] Counter Status Label: Introduced a real-time UI element displaying
the next projected number for the selected grid and block type.

## CHANGELOG v1.4.2
 - [FIXED]   Regex Replace: Resolved pattern matching logic and corrected a 
             networking issue that prevented regex operations from executing.
 - [NEW]     Number Prefix: Added a new function to allow sequential numbering 
             before the block name, rather than just as a suffix.