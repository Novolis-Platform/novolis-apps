using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Pure query projection over the immutable employee view.</summary>
public static class HoursQueryEngine
{
    /// <summary>Executes a named query without changing any worktime or approval facts.</summary>
    public static HoursQueryResult Execute(HoursEmployeeView view, HoursQuery query)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(query);
        if (!string.Equals(view.EmployeeId, query.EmployeeId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The query employee must match the projected employee.", nameof(query));
        }

        IEnumerable<HoursQueryRow> rows = query.Kind switch
        {
            HoursQueryKind.Summary =>
            new[]
            {
                new HoursQueryRow(
                    null,
                    "summary",
                    $"{view.Entries.Length} presence record(s); {view.Adjustments.Length} adjustment(s).",
                    view.FlexSaldo),
            },
            HoursQueryKind.Entries => view.Entries
                .Where(entry => IsInRange(entry.Day, query))
                .Select(entry => new HoursQueryRow(
                    entry.Day,
                    "presence",
                    entry.Comment,
                    entry.FlexDelta,
                    null,
                    entry.Id)),
            HoursQueryKind.Adjustments => view.Adjustments
                .Where(adjustment => IsInRange(adjustment.EffectiveDay, query))
                .Select(adjustment => new HoursQueryRow(
                    adjustment.EffectiveDay,
                    adjustment.Reason.ToString(),
                    adjustment.Comment,
                    adjustment.DurationDelta,
                    adjustment.State.ToString(),
                    adjustment.Id)),
            HoursQueryKind.Anomalies => view.Anomalies
                .Select(anomaly => new HoursQueryRow(
                    null,
                    anomaly.Code,
                    anomaly.Message,
                    null,
                    null,
                    anomaly.RelatedRecordId)),
            HoursQueryKind.Ledger => view.LedgerPostings
                .Select(posting => new HoursQueryRow(
                    null,
                    posting.Reason.ToString(),
                    $"{posting.Account}: {posting.Narrative}",
                    posting.SignedDuration,
                    null,
                    posting.SourceId)),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Kind, "Unknown Hours query kind."),
        };

        return new HoursQueryResult(query.EmployeeId, query.Kind, rows.ToImmutableArray());
    }

    private static bool IsInRange(DateOnly day, HoursQuery query) =>
        (query.From is null || day >= query.From) &&
        (query.Through is null || day <= query.Through);
}
