using Novolis.Hours.Client.Presentation;
using Spectre.Console;

namespace Novolis.Hours.Client.Cli;

/// <summary>Spectre tables shared by palette and scripted subcommands.</summary>
internal static class HoursCliTables
{
    /// <summary>Renders a Monday-first week.</summary>
    public static void WriteWeek(IAnsiConsole console, WeekStudioModel week)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(week);
        var table = new Table();
        table.AddColumn("Day");
        table.AddColumn("Expected");
        table.AddColumn("Chip");
        table.AddColumn("Recorded");
        foreach (var tile in week.Tiles)
        {
            table.AddRow(
                tile.IsToday ? $"[bold]{tile.Weekday}[/]" : tile.Weekday,
                tile.Expected,
                tile.Chip,
                HoursClock.Format(tile.Day.ActualWorked));
        }

        console.Write(table);
        console.MarkupLine(
            $"Expected [bold]{HoursClock.Format(week.ExpectedWork)}[/] · recorded [bold]{HoursClock.Format(week.RecordedWork)}[/]");
    }

    /// <summary>Renders one day strip as a layer table. Silence stays empty.</summary>
    public static void WriteDay(IAnsiConsole console, DayStudioModel studio)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(studio);
        console.MarkupLine(
            $"[bold]{HoursClock.FormatDate(studio.Day.NominalDate)}[/] · {studio.Day.OrganisationId}");
        console.MarkupLine(
            $"Expected {HoursClock.Format(studio.Day.ExpectedWork)} · actual {HoursClock.Format(studio.ActualWork)} · flex {HoursClock.Format(studio.Flex, signed: true)}");
        var layers = new Table();
        layers.AddColumn("Order");
        layers.AddColumn("Layer");
        layers.AddColumn("Said");
        foreach (var row in studio.LayerRail())
        {
            layers.AddRow(row.Order.ToString(), row.Kind, row.Said);
        }

        console.Write(layers);
        var bands = new Table();
        bands.AddColumn("Band");
        bands.AddColumn("Range");
        if (studio.Day.WorkEnvelopeRange is { } envelope)
        {
            bands.AddRow("Envelope", HoursClock.Format(envelope.Start, envelope.End));
        }

        foreach (var core in studio.Day.CoreHourRanges)
        {
            bands.AddRow("Core", HoursClock.Format(core.Start, core.End));
        }

        foreach (var routine in studio.Day.RoutineRanges)
        {
            bands.AddRow("Routine", HoursClock.Format(routine.Start, routine.End));
        }

        foreach (var actual in studio.Actual)
        {
            bands.AddRow("Actual", HoursClock.Format(actual.Start, actual.End));
        }

        foreach (var stroke in studio.Paints)
        {
            bands.AddRow($"Paint {stroke.ValueId}", HoursClock.Format(stroke.Start, stroke.End));
        }

        console.Write(bands);
    }
}
