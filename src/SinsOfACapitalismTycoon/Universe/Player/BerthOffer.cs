using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>One playable berth bet for the captain bridge.</summary>
internal sealed record BerthOffer(
  BerthOfferKind Kind,
  string Title,
  string Band,
  string Hook,
  string Detail,
  CaptainJobBoard.SpotCandidate? Spot,
  int SpotIndex,
  int? WaitDaysHint = null);
