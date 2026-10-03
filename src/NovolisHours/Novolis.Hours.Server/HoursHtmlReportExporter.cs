using Novolis.Hours.Domain;
using Novolis.Markup.Html;

namespace Novolis.Hours.Server;

/// <summary>Exports a self-contained, read-only HTML audit report without converting duration records into payroll data.</summary>
public sealed class HoursHtmlReportExporter
{
    /// <summary>Renders one employee's immutable worktime view as portable HTML.</summary>
    public string Export(HoursEmployeeView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var document = HtmlMarkup.Document()
            .Lang("en")
            .CharsetUtf8()
            .Viewport()
            .Title("Novolis Hours report")
            .WithHead(head => head.StyleSheet(ReportStyles))
            .WithBody(body =>
            {
                body.H1("Novolis Hours worktime report");
                body.P(paragraph => paragraph.Strong("Employee: ").Text(view.EmployeeId));
                body.P(paragraph => paragraph.Strong("Flex saldo: ").Text(Format(view.FlexSaldo)));
                body.P("This report tracks worktime only. It does not calculate payment or leave.");

                body.H2("Recorded presence");
                AddTable(
                    body,
                    ["Day", "Expected snapshot", "Presence", "Expected", "Actual", "Flex", "Financial compensation", "Rule firings", "Comment"],
                    view.Entries.Select(entry => new[]
                    {
                        entry.Day.ToString("yyyy-MM-dd"),
                        Snapshot(entry),
                        $"{entry.StartedAt:HH\\:mm}–{entry.EndedAt:HH\\:mm}",
                        Format(entry.ExpectedDuration),
                        Format(entry.ActualDuration),
                        Format(entry.FlexDelta),
                        FinancialCompensation(entry),
                        string.Join("; ", entry.LegalNotices.Select(notice => $"{notice.RuleId} ({notice.PresetId} {notice.PresetVersion})")),
                        entry.Comment,
                    }));

                body.H2("Adjustments");
                AddTable(
                    body,
                    ["Day", "Duration", "Reason", "State", "Proposed by", "Employee response", "HR/Higher resolution", "Comment"],
                    view.Adjustments.Select(adjustment => new[]
                    {
                        adjustment.EffectiveDay.ToString("yyyy-MM-dd"),
                        Format(adjustment.DurationDelta),
                        adjustment.Reason.ToString(),
                        adjustment.State.ToString(),
                        adjustment.ProposedBy.DisplayName,
                        adjustment.EmployeeResponseComment ?? string.Empty,
                        adjustment.ResolutionComment ?? string.Empty,
                        adjustment.Comment,
                    }));

                body.H2("Approval periods");
                AddTable(
                    body,
                    ["Period", "Employee due", "Manager due", "HR/Higher due", "State", "Last action"],
                    view.ApprovalPeriods.Select(period => new[]
                    {
                        $"{period.StartsOn:yyyy-MM-dd}–{period.EndsOn:yyyy-MM-dd}",
                        period.EmployeeSubmitDueOn.ToString("yyyy-MM-dd"),
                        period.ManagerReviewDueOn.ToString("yyyy-MM-dd"),
                        period.HrResolutionDueOn.ToString("yyyy-MM-dd"),
                        period.State.ToString(),
                        period.LastComment ?? string.Empty,
                    }));

                body.H2("Notices and anomalies");
                if (view.Anomalies.IsEmpty)
                {
                    body.P("No current notices or anomalies.");
                }
                else
                {
                    foreach (var anomaly in view.Anomalies)
                    {
                        body.Div(notice => notice
                            .Class("notice")
                            .Strong(anomaly.Code)
                            .Text($": {anomaly.Message}"));
                    }
                }
            });
        return document.ToString();
    }

    private const string ReportStyles = """
        body{font-family:system-ui,sans-serif;margin:2rem;color:#17212b;background:#f7fafc}
        table{border-collapse:collapse;width:100%;background:white}th,td{padding:.55rem;border:1px solid #d7e0e8;text-align:left;vertical-align:top}
        h1,h2{color:#0e3b58}.notice{background:#fff7df;padding:.6rem;margin:.4rem 0;border-left:4px solid #d28d27}
        """;

    private static void AddTable(HtmlElement parent, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        parent.Table(table =>
        {
            table.Thead(header => header.Tr(row =>
            {
                foreach (var label in headers)
                {
                    row.Th(label);
                }
            }));
            table.Tbody(body =>
            {
                foreach (var cells in rows)
                {
                    body.Tr(row =>
                    {
                        foreach (var cell in cells)
                        {
                            row.Td(cell);
                        }
                    });
                }
            });
        });
    }

    private static string Snapshot(HoursEntry entry) =>
        $"{entry.WorktimeSnapshot.ProfileId}; {entry.WorktimeSnapshot.TemplateId}; {entry.WorktimeSnapshot.CalendarId} ({entry.WorktimeSnapshot.CalendarSource}); {entry.WorktimeSnapshot.LegalPresetId} {entry.WorktimeSnapshot.LegalPresetVersion}";

    private static string FinancialCompensation(HoursEntry entry)
    {
        var marks = entry.FinancialCompensationSlices
            .Select(slice => $"{slice.Start:HH\\:mm}–{slice.End:HH\\:mm}: {slice.Reason}");
        return $"{Format(entry.FinancialCompensationDuration)} {string.Join("; ", marks)}".TrimEnd();
    }

    private static string Format(TimeSpan duration) =>
        FormattableString.Invariant($"{duration.TotalHours:+0.##;-0.##;0} h");
}
