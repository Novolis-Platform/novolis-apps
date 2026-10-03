using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>JSON repository envelope for one immutable Hours journal event.</summary>
public sealed record HoursJournalDocument(Guid Id, HoursEvent Event) : IHasId;
