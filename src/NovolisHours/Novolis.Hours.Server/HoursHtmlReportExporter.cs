using System.Net;
using System.Text;
using Novolis.Hours.Domain;

namespace Novolis.Hours.Server;

/// <summary>Exports a self-contained, read-only HTML audit report without converting duration records into payroll data.</summary>
public sealed class HoursHtmlReportExporter
{
    /// <summary>Renders one employee's immutable worktime view as portable HTML.</summary>
    public string Export(HoursEmployeeView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var html = new StringBuilder();
        html.Append("""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><title>Novolis Hours report</title>
            <style>
            body{font-family:system-ui,sans-serif;margin:2rem;color:#17212b;background:#f7fafc}
            table{border-collapse:collapse;width:100%;background:white}th,td{padding:.55rem;border:1px solid #d7e0e8;text-align:left}
            h1,h2{color:#0e3b58}.notice{background:#fff7df;padding:.6rem;margin:.4rem 0;border-left:4px solid #d28d27}
            </style></head><body>
            """);
        html.Append("<h1>Novolis Hours worktime report</h1>");
        html.Append("<p><strong>Employee:</strong> ").Append(Encode(view.EmployeeId)).Append("</p>");
        html.Append("<p><strong>Flex saldo:</strong> ").Append(Format(view.FlexSaldo)).Append("</p>");
        html.Append("<p>This report tracks worktime only. It does not calculate payment or leave.</p>");
        html.Append("<h2>Recorded presence</h2><table><thead><tr><th>Day</th><th>Presence</th><th>Expected</th><th>Actual</th><th>Flex</th><th>Financial compensation mark</th><th>Comment</th></tr></thead><tbody>");
        foreach (var entry in view.Entries)
        {
            html.Append("<tr><td>").Append(entry.Day).Append("</td><td>")
                .Append(entry.StartedAt.ToString("HH:mm")).Append("–").Append(entry.EndedAt.ToString("HH:mm"))
                .Append("</td><td>").Append(Format(entry.ExpectedDuration))
                .Append("</td><td>").Append(Format(entry.ActualDuration))
                .Append("</td><td>").Append(Format(entry.FlexDelta))
                .Append("</td><td>").Append(Format(entry.FinancialCompensationDuration))
                .Append("</td><td>").Append(Encode(entry.Comment)).Append("</td></tr>");
        }

        html.Append("</tbody></table><h2>Adjustments</h2><table><thead><tr><th>Day</th><th>Duration</th><th>Reason</th><th>State</th><th>Comment</th></tr></thead><tbody>");
        foreach (var adjustment in view.Adjustments)
        {
            html.Append("<tr><td>").Append(adjustment.EffectiveDay).Append("</td><td>")
                .Append(Format(adjustment.DurationDelta)).Append("</td><td>")
                .Append(adjustment.Reason).Append("</td><td>")
                .Append(adjustment.State).Append("</td><td>")
                .Append(Encode(adjustment.Comment)).Append("</td></tr>");
        }

        html.Append("</tbody></table><h2>Notices and anomalies</h2>");
        if (view.Anomalies.IsEmpty)
        {
            html.Append("<p>No current notices or anomalies.</p>");
        }
        else
        {
            foreach (var anomaly in view.Anomalies)
            {
                html.Append("<div class=\"notice\"><strong>")
                    .Append(Encode(anomaly.Code))
                    .Append("</strong>: ")
                    .Append(Encode(anomaly.Message))
                    .Append("</div>");
            }
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string Format(TimeSpan duration) =>
        FormattableString.Invariant($"{duration.TotalHours:+0.##;-0.##;0} h");
}
