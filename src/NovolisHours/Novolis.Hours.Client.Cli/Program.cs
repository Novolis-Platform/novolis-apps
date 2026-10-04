using System.CommandLine;
using Novolis.Hours.Client.Cli;
using Novolis.Hours.Client.Presentation;
using Spectre.Console;

var console = AnsiConsole.Console;
var session = new HoursCliSession();

var urlOption = new Option<string?>("--url")
{
    Description = "Hours service URL (otherwise Settings / environment)",
};
var loginOption = new Option<string?>("--login")
{
    Description = "Login",
};
var passwordOption = new Option<string?>("--password")
{
    Description = "Password",
};
var dateOption = new Option<string?>("--date")
{
    Description = "Nominal date yyyy-MM-dd (default today)",
};

var week = new Command("week", "Show this week's DayShape tiles");
week.SetAction(async (parse, cancellation) =>
{
    if (!await EnsureSignedInAsync(parse, cancellation))
    {
        return 1;
    }

    HoursCliTables.WriteWeek(console, await session.LoadWeekAsync(null, cancellation));
    return 0;
});

var day = new Command("day", "Show one day as a layer table");
var dayDate = new Argument<string?>("date") { Description = "Nominal date yyyy-MM-dd", Arity = ArgumentArity.ZeroOrOne };
day.Arguments.Add(dayDate);
day.SetAction(async (parse, cancellation) =>
{
    if (!await EnsureSignedInAsync(parse, cancellation))
    {
        return 1;
    }

    var date = ParseDate(parse.GetValue(dayDate));
    HoursCliTables.WriteDay(console, await session.LoadDayAsync(date, cancellation));
    return 0;
});

var recordCommand = new Command("record", "Record worked-as-scheduled after confirmation");
var scheduled = new Option<bool>("--scheduled") { Description = "Worked as planned", DefaultValueFactory = _ => true };
recordCommand.Options.Add(scheduled);
recordCommand.Options.Add(dateOption);
recordCommand.SetAction(async (parse, cancellation) =>
{
    if (!await EnsureSignedInAsync(parse, cancellation))
    {
        return 1;
    }

    var date = ParseDate(parse.GetValue(dateOption));
    var studio = await session.LoadDayAsync(date, cancellation);
    HoursCliTables.WriteDay(console, studio);
    if (!console.Confirm("Record worked as scheduled?"))
    {
        return 0;
    }

    await session.RecordScheduledAsync(date, cancellation);
    console.MarkupLine("[green]Scheduled work recorded.[/]");
    return 0;
});

var paint = new Command("paint", "Paint a Dimension onto overlapping actual work");
var fromOption = new Option<string>("--from") { Description = "Start clock HH:mm" };
var toOption = new Option<string>("--to") { Description = "End clock HH:mm" };
var valueOption = new Option<string>("--value") { Description = "Brush value id", DefaultValueFactory = _ => "acme" };
var dimensionOption = new Option<string>("--dimension") { Description = "Brush Dimension id", DefaultValueFactory = _ => "customer" };
paint.Options.Add(fromOption);
paint.Options.Add(toOption);
paint.Options.Add(valueOption);
paint.Options.Add(dimensionOption);
paint.Options.Add(dateOption);
paint.SetAction(async (parse, cancellation) =>
{
    if (!await EnsureSignedInAsync(parse, cancellation))
    {
        return 1;
    }

    var date = ParseDate(parse.GetValue(dateOption));
    if (!TimeOnly.TryParse(parse.GetValue(fromOption), out var from) ||
        !TimeOnly.TryParse(parse.GetValue(toOption), out var to))
    {
        console.MarkupLine("[red]--from and --to must be HH:mm clocks.[/]");
        return 1;
    }

    try
    {
        await session.PaintAsync(
            date,
            from,
            to,
            parse.GetValue(dimensionOption) ?? "customer",
            parse.GetValue(valueOption) ?? "acme",
            cancellation);
        console.MarkupLine("[green]Paint saved.[/]");
        return 0;
    }
    catch (Exception exception)
    {
        console.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
        return 1;
    }
});

var review = new Command("review", "Month strip of expected versus recorded");
review.SetAction(async (parse, cancellation) =>
{
    if (!await EnsureSignedInAsync(parse, cancellation))
    {
        return 1;
    }

    var days = await session.LoadMonthAsync(DateOnly.FromDateTime(DateTime.Today), cancellation);
    var table = new Table();
    table.AddColumn("Date");
    table.AddColumn("Expected");
    table.AddColumn("Recorded");
    foreach (var workDay in days)
    {
        table.AddRow(
            HoursClock.FormatDate(workDay.NominalDate),
            HoursClock.Format(workDay.ExpectedWork),
            HoursClock.Format(workDay.ActualWorked));
    }

    console.Write(table);
    return 0;
});

var pressure = new Command("pressure", "Organisation business-pressure totals");
pressure.SetAction(async (parse, cancellation) =>
{
    if (!await EnsureSignedInAsync(parse, cancellation))
    {
        return 1;
    }

    var model = new PressureModel(await session.LoadPressureAsync(cancellation));
    console.MarkupLine(
        $"Total {HoursClock.Format(model.Total)} · attributed {HoursClock.Format(model.Attributed)} · unattributed {HoursClock.Format(model.Unattributed)}");
    return 0;
});

var settings = new Command("settings", "Show or set the Hours service URL");
settings.Options.Add(urlOption);
settings.SetAction((parse, _) =>
{
    var url = parse.GetValue(urlOption);
    if (!string.IsNullOrWhiteSpace(url))
    {
        session.Model.ServiceUrl = url;
        HoursSessionPersistence.Save(session.Model);
    }

    console.MarkupLine($"Service URL: {Markup.Escape(session.Model.ServiceUrl)}");
    console.MarkupLine($"Person: {Markup.Escape(session.Model.DisplayName ?? "not signed in")}");
    return Task.FromResult(0);
});

var root = new RootCommand("""
    Hours employee client — palette and scripted commands.

    Examples:
      hours
      hours week
      hours day 2026-10-01
      hours record --scheduled
      hours paint --from 08:00 --to 11:30 --value acme
    """)
{
    urlOption,
    loginOption,
    passwordOption,
};
root.Subcommands.Add(week);
root.Subcommands.Add(day);
root.Subcommands.Add(recordCommand);
root.Subcommands.Add(paint);
root.Subcommands.Add(review);
root.Subcommands.Add(pressure);
root.Subcommands.Add(settings);

root.SetAction(async (parse, cancellation) =>
{
    if (!await EnsureSignedInAsync(parse, cancellation))
    {
        return 1;
    }

    while (!cancellation.IsCancellationRequested)
    {
        var verb = HoursPalette.Prompt(console);
        if (string.Equals(verb, "quit", StringComparison.Ordinal))
        {
            return 0;
        }

        if (!HoursPalette.Catalog.Contains(verb, StringComparer.Ordinal))
        {
            var hint = HoursLevenshtein.Suggest(verb, HoursPalette.Catalog);
            console.MarkupLine(hint is null
                ? $"[red]Unknown command '{Markup.Escape(verb)}'.[/]"
                : $"[yellow]Unknown command. Did you mean {hint}?[/]");
            continue;
        }

        switch (verb)
        {
            case "week":
                HoursCliTables.WriteWeek(console, await session.LoadWeekAsync(null, cancellation));
                break;
            case "day":
                var dateText = console.Prompt(new TextPrompt<string>("Date (yyyy-MM-dd)").DefaultValue(DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd")));
                HoursCliTables.WriteDay(console, await session.LoadDayAsync(ParseDate(dateText), cancellation));
                break;
            case "record":
                var recordDate = ParseDate(null);
                HoursCliTables.WriteDay(console, await session.LoadDayAsync(recordDate, cancellation));
                if (console.Confirm("Record worked as scheduled?"))
                {
                    await session.RecordScheduledAsync(recordDate, cancellation);
                    console.MarkupLine("[green]Scheduled work recorded.[/]");
                }

                break;
            case "paint":
                var paintDate = ParseDate(null);
                var from = TimeOnly.Parse(console.Prompt(new TextPrompt<string>("From").DefaultValue("08:00")));
                var to = TimeOnly.Parse(console.Prompt(new TextPrompt<string>("To").DefaultValue("11:30")));
                var value = console.Prompt(new TextPrompt<string>("Value").DefaultValue("acme"));
                try
                {
                    await session.PaintAsync(paintDate, from, to, "customer", value, cancellation);
                    console.MarkupLine("[green]Paint saved.[/]");
                }
                catch (Exception exception)
                {
                    console.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
                }

                break;
            case "review":
                var month = await session.LoadMonthAsync(DateOnly.FromDateTime(DateTime.Today), cancellation);
                var monthTable = new Table();
                monthTable.AddColumn("Date");
                monthTable.AddColumn("Expected");
                monthTable.AddColumn("Recorded");
                foreach (var workDay in month)
                {
                    monthTable.AddRow(
                        HoursClock.FormatDate(workDay.NominalDate),
                        HoursClock.Format(workDay.ExpectedWork),
                        HoursClock.Format(workDay.ActualWorked));
                }

                console.Write(monthTable);
                break;
            case "pressure":
                var pressureModel = new PressureModel(await session.LoadPressureAsync(cancellation));
                console.MarkupLine(
                    $"Total {HoursClock.Format(pressureModel.Total)} · attributed {HoursClock.Format(pressureModel.Attributed)} · unattributed {HoursClock.Format(pressureModel.Unattributed)}");
                break;
            case "settings":
                console.MarkupLine($"Service URL: {Markup.Escape(session.Model.ServiceUrl)}");
                break;
        }
    }

    return 0;
});

try
{
    return await root.Parse(args).InvokeAsync();
}
finally
{
    session.Dispose();
}

async Task<bool> EnsureSignedInAsync(ParseResult parse, CancellationToken cancellation)
{
    var url = parse.GetValue(urlOption);
    if (!string.IsNullOrWhiteSpace(url))
    {
        session.Model.ServiceUrl = url;
        HoursSessionPersistence.Save(session.Model);
    }

    if (session.User is not null)
    {
        return true;
    }

    var login = parse.GetValue(loginOption)
        ?? Environment.GetEnvironmentVariable("NOVOLIS_HOURS_LOGIN")
        ?? session.Model.EmployeeId
        ?? console.Prompt(new TextPrompt<string>("Login"));
    var password = parse.GetValue(passwordOption)
        ?? Environment.GetEnvironmentVariable("NOVOLIS_HOURS_PASSWORD")
        ?? console.Prompt(new TextPrompt<string>("Password").Secret());
    try
    {
        await session.SignInAsync(login, password, cancellation);
        return true;
    }
    catch (Exception exception)
    {
        console.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
        return false;
    }
}

static DateOnly ParseDate(string? text) =>
    DateOnly.TryParse(text, out var date) ? date : DateOnly.FromDateTime(DateTime.Today);
