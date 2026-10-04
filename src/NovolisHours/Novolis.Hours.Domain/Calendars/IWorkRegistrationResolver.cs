using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Resolves a work assertion against an immutable calendar shape.</summary>
public interface IWorkRegistrationResolver
{
    /// <summary>Produces a derived workday without changing the assertion.</summary>
    ResolvedWorkDay Resolve(WorkRegistration registration, DayShape shape);
}
