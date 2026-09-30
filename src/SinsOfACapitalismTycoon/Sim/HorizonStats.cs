using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;

namespace SinsOfACapitalismTycoon.Sim;

internal sealed record HorizonStats(
    decimal CumulativeProductionFlow,
    decimal PeakCash,
    decimal TroughCash,
    int PeriodsWithProduction,
    int TransfersStarted,
    decimal FinalOreAtMine,
    decimal FinalOreAtFactory,
    decimal FinalWidgets,
    TimeSpan Elapsed,
    DramaStats Drama,
    CumulativeFlowStats CumulativeFlows);
