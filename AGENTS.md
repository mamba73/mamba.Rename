# SE Mod Development AGENTS.md

You're an experienced Space Engineers Mod developer.

This is a mod for the Space Engineers game, strictly adhering to ModAPI and Sandbox constraints. This is NOT a plugin and does NOT use the Pulsar framework.

## General instructions
- Always strive for precise, clean, and minimal code changes.
- Do not change any unrelated code, XML definitions, or comments.
- Follow standard Space Engineers naming conventions (PascalCase for classes/methods, camelCase for variables).
- Do NOT write excessive source code comments. Add comments only if extra clarification is needed on WHY a specific architectural choice or workaround was made. Do NOT repeat the code's logic in English.
- Avoid changing whitespace on code lines which are not directly connected to code lines where actual logic is modified.
- Avoid writing spaghetti code; keep `MyGameLogicComponent` and `MySessionComponentBase` methods highly readable and orderly.
- In the face of ambiguity resist the temptation to guess. Ask questions instead.
- NEVER change the `AGENTS.md` or `agent_rules.yaml` files, UNLESS you're explicitly asked to do so.

## Scripting & ModAPI Best Practices
- Sandbox Compliance: SE mods must be fully compatible with the Sandbox API. Avoid Reflection and unsafe code unless absolutely necessary.
- PB Compatibility: If writing logic intended for use by Programmable Blocks, expose methods via public static wrapper classes. Document all public API methods clearly in the script header.
- Performance: Mods execute on all clients and the server simultaneously. Avoid heavy operations in `Update()` if events or `MyAPIGateway.Utilities.InvokeOnGameThread` can be used.
- Exception Handling: Implement graceful degradation for block logic. If a specific component fails, ensure the rest of the mod and the game remain stable. Log errors to `MyLog.Default` with a clear mod identifier.
- ReSharper: Keep `// ReSharper` comments in place, as they function like pragmas specific to JetBrains Rider/ReSharper.

## Folder structure reference
- `Data/`: XML definitions (blocks, blueprints, cubes, etc.).
- `Data/Scripts/`: C# source code files.
- `Models/`: .mwm model files.
- `Textures/`: DDS texture files.
- `Docs/`: Images linked from README or additional documentation.

## Declaration Integrity
- Strictly forbidden to invent or hallucinate API names. Existing declarations (e.g., `IMyStoreBlock`, `MyGameLogicComponent`) must be verified and used exactly as defined in the official SE ModAPI.
- Zero mixing of XML logic and C# source code.

## Enforcement
- Context A (IDE/Workspace): You are permitted to use precise 'Search/Replace' blocks. Placeholders like '// ...' or '// rest of code' are strictly forbidden.
- Context B (Chat/Text-only): You MUST output 100% COMPLETE FILES. Every file must be returned in its entirety, including all original comments, namespace declarations, and logic. Use [code] tokens at the start and end of code snippets to prevent Markdown truncation.