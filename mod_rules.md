// MAMBA SE MOD RULES 2026
API: Use exact VRage/SE API (MatrixD, IMyShipController); no hallucinations.
CHARS: English only. No emojis in .cs files (prevents VRage crash).
COMPAT: Strict C# 6.0 and .NET 4.8 syntax. No modern features (init, records).
CONFIG: Zero hardcoding. Use StorageUtils for XML or single global config file.
HEADER: Line 1 comment = relative path + '// MAMBA [NAME]'. Namespace matches folder.
INTEGRITY: 100% complete files; zero placeholders (//..., TODO); no fake methods.
LOGS: LogManager must support Info, Warn, Error, SetDebugMode, conditional Debug, legacy ctor.
README: Wrap in python block. Use identical [code] tags for both start and end.
SAFETY: Ensure strict thread safety; prevent race conditions in parallel execution.
SANDBOX: Use Keen updates (Update10/100). No forbidden namespaces (System.IO, Reflection).
STYLE: HR/EN responses. Concise writing; return Yes/No questions as 'Da'/'Ne' only.