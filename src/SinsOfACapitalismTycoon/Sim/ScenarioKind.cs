namespace SinsOfACapitalismTycoon.Sim;

internal enum ScenarioKind
{
    Baseline,
    LogisticsBind,
    WorkingCapital,
    CreditCycle,
    FiscalStress,
    Shock
}

internal static class ScenarioKindParser
{
    public static ScenarioKind Parse(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "baseline" => ScenarioKind.Baseline,
            "logistics_bind" or "logistics" => ScenarioKind.LogisticsBind,
            "working_capital" or "working-capital" or "wc" => ScenarioKind.WorkingCapital,
            "credit_cycle" or "credit" => ScenarioKind.CreditCycle,
            "fiscal_stress" or "fiscal" => ScenarioKind.FiscalStress,
            "shock" => ScenarioKind.Shock,
            _ => throw new ArgumentException(
                $"Unknown --scenario '{value}'. Use baseline|logistics_bind|working_capital|credit_cycle|fiscal_stress|shock.")
        };

    public static string ToArg(ScenarioKind kind) =>
        kind switch
        {
            ScenarioKind.Baseline => "baseline",
            ScenarioKind.LogisticsBind => "logistics_bind",
            ScenarioKind.WorkingCapital => "working_capital",
            ScenarioKind.CreditCycle => "credit_cycle",
            ScenarioKind.FiscalStress => "fiscal_stress",
            ScenarioKind.Shock => "shock",
            _ => kind.ToString()
        };
}
