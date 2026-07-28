using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Novolis.Audio.Live;
using Novolis.Audio.Live.Dsl;
using Novolis.Audio.Live.Repl;
using Novolis.Audio.MusicTheory;
using Novolis.Audio.Patterns;

namespace LiveStudio.Components.Live;

/// <summary>
/// Compiles Live editor buffers: full C# scripts returning <see cref="LiveProgramDefinition"/>,
/// with a fallback to the tiny <c>Note.Play</c> REPL.
/// </summary>
public sealed class LiveScriptCompiler
{
    static readonly Lazy<ScriptOptions> Options = new(CreateOptions);
    readonly LiveReplSyntaxCompiler _repl = new();

    public async Task<LiveProgramDefinition> CompileAsync(string source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        cancellationToken.ThrowIfCancellationRequested();

        var trimmed = source.Trim();
        if (LooksLikeReplOnly(trimmed))
            return _repl.Compile(NormalizeRepl(trimmed));

        try
        {
            var result = await CSharpScript
                .EvaluateAsync<LiveProgramDefinition>(trimmed, Options.Value, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result ?? throw new InvalidOperationException("Script returned null LiveProgramDefinition.");
        }
        catch (CompilationErrorException ex)
        {
            var message = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException(message, ex);
        }
    }

    static bool LooksLikeReplOnly(string source)
    {
        if (source.Contains("return ", StringComparison.Ordinal)
            || source.Contains("Program(", StringComparison.Ordinal)
            || source.Contains("Track(", StringComparison.Ordinal)
            || source.Contains("Sequence(", StringComparison.Ordinal))
            return false;

        return source.Contains("Note.Play", StringComparison.Ordinal);
    }

    static string NormalizeRepl(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var parts = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
                line = line[..comment];
            line = line.Trim();
            if (line.Length > 0)
                parts.Add(line);
        }

        return string.Join(' ', parts);
    }

    static ScriptOptions CreateOptions()
    {
        var refs = new Assembly[]
        {
            typeof(LiveProgramDefinition).Assembly,
            typeof(LiveDsl).Assembly,
            typeof(PitchClass).Assembly,
            typeof(PatternNode).Assembly,
            typeof(object).Assembly,
        };

        return ScriptOptions.Default
            .WithReferences(refs)
            .WithImports(
                "System",
                "Novolis.Audio.Live",
                "Novolis.Audio.Live.Dsl",
                "Novolis.Audio.MusicTheory",
                "Novolis.Audio.Patterns")
            .WithEmitDebugInformation(false);
    }
}
