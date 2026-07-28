# Live Avalonia components (extractable)

These types under `Components/Live/` are shaped to move into `Novolis.Avalonia.Live`:

| Type | Role |
|------|------|
| `LiveDemoDocument` / `LiveDemoCatalog` | Editable demo source catalog |
| `LiveScriptCompiler` | Roslyn C# → `LiveProgramDefinition` (+ Note.Play REPL fallback) |
| `LiveCodeEditorControl` | AvaloniaEdit + Ctrl+Space Live DSL completion |
| `LiveDslCompletionProvider` | Completion catalog |
| `ILiveVisualizer` + graph / piano / interpretation | Visualizers |
| `LiveVisualizerWindow` | Child window host |

Move steps later: copy into `novolis-avalonia/src/Novolis.Avalonia.Live`, publish GPR, switch LiveStudio to PackageReference-only usage.
