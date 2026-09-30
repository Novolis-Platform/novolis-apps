using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;

namespace SinsOfACapitalismTycoon.Sim;

internal sealed record DramaStats(
    int PeriodsWithoutProduction,
    int LongestProductionGap,
    decimal PeakMineOreStockpile,
    int FactoryOreStockoutPeriods,
    int DelinquentObligationSightings,
    int DefaultedObligationSightings,
    int CreditDraws,
    int ShocksInjected,
    int CapacityExpansions,
    decimal MoneyCreatedByPolicy);
