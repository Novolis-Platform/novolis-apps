using DraftStudio.Core;
using DraftStudio.Models;

namespace DraftStudio.Commands;

internal interface IDraftCommand
{
    string Label { get; }

    void Execute(DraftSession session);

    void Undo(DraftSession session);
}

internal sealed class DraftCommandBus
{
    private readonly DraftSession _session;
    private readonly Stack<IDraftCommand> _undo = new();
    private readonly Stack<IDraftCommand> _redo = new();

    public DraftCommandBus(DraftSession session) => _session = session;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public event Action? Changed;

    public void Execute(IDraftCommand command)
    {
        command.Execute(_session);
        _undo.Push(command);
        _redo.Clear();
        _session.MarkDirty();
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;
        var cmd = _undo.Pop();
        cmd.Undo(_session);
        _redo.Push(cmd);
        _session.MarkDirty();
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;
        var cmd = _redo.Pop();
        cmd.Execute(_session);
        _undo.Push(cmd);
        _session.MarkDirty();
        Changed?.Invoke();
    }
}

internal sealed class AddEntityCommand : IDraftCommand
{
    private readonly CadEntity _entity;

    public AddEntityCommand(CadEntity entity) => _entity = entity;

    public string Label => $"Add {_entity.Kind}";

    public void Execute(DraftSession session)
    {
        _entity.WithLayer(session.Document);
        if (session.Document.Entities.All(e => e.Id != _entity.Id))
            session.Document.Entities.Add(_entity);
        session.SelectedId = _entity.Id;
    }

    public void Undo(DraftSession session)
    {
        session.Document.Entities.RemoveAll(e => e.Id == _entity.Id);
        if (session.SelectedId == _entity.Id)
            session.SelectedId = null;
    }
}

internal sealed class DeleteEntitiesCommand : IDraftCommand
{
    private readonly List<CadEntity> _removed = [];
    private readonly HashSet<Guid> _ids;

    public DeleteEntitiesCommand(IEnumerable<Guid> ids) => _ids = ids.ToHashSet();

    public string Label => "Delete";

    public void Execute(DraftSession session)
    {
        _removed.Clear();
        foreach (var entity in session.Document.Entities.Where(e => _ids.Contains(e.Id)).ToList())
        {
            _removed.Add(entity);
            session.Document.Entities.Remove(entity);
        }

        if (session.SelectedId is { } sid && _ids.Contains(sid))
            session.SelectedId = null;
    }

    public void Undo(DraftSession session)
    {
        foreach (var entity in _removed)
        {
            if (session.Document.Entities.All(e => e.Id != entity.Id))
                session.Document.Entities.Add(entity);
        }
    }
}

internal sealed class MoveEntitiesCommand : IDraftCommand
{
    private readonly HashSet<Guid> _ids;
    private readonly float _dx;
    private readonly float _dy;
    private readonly float _dz;

    public MoveEntitiesCommand(IEnumerable<Guid> ids, float dx, float dy, float dz)
    {
        _ids = ids.ToHashSet();
        _dx = dx;
        _dy = dy;
        _dz = dz;
    }

    public string Label => "Move";

    public void Execute(DraftSession session) => Apply(session, _dx, _dy, _dz);

    public void Undo(DraftSession session) => Apply(session, -_dx, -_dy, -_dz);

    private void Apply(DraftSession session, float dx, float dy, float dz)
    {
        foreach (var entity in session.Document.Entities.Where(e => _ids.Contains(e.Id)))
            CadVec.TranslateEntity(entity, dx, dy, dz);
    }
}

/// <summary>Undoable geometry mutation for grip edits (line endpoints, box extents, …).</summary>
internal sealed class MutateEntityGeometryCommand : IDraftCommand
{
    private readonly Guid _id;
    private readonly EntityGeometrySnapshot _before;
    private readonly EntityGeometrySnapshot _after;

    public MutateEntityGeometryCommand(Guid id, EntityGeometrySnapshot before, EntityGeometrySnapshot after)
    {
        _id = id;
        _before = before;
        _after = after;
    }

    public string Label => "Edit geometry";

    public void Execute(DraftSession session) => Apply(session, _after);

    public void Undo(DraftSession session) => Apply(session, _before);

    private void Apply(DraftSession session, EntityGeometrySnapshot snap)
    {
        var entity = session.Document.Entities.FirstOrDefault(e => e.Id == _id);
        if (entity is null)
            return;
        snap.ApplyTo(entity);
        session.SelectedId = _id;
    }
}

internal sealed class EntityGeometrySnapshot
{
    public float[]? A { get; init; }
    public float[]? B { get; init; }
    public float[]? Center { get; init; }
    public float[]? HalfExtents { get; init; }
    public float Radius { get; init; }
    public float Height { get; init; }
    public List<float[]>? ControlPoints { get; init; }
    public float[]? Knots { get; init; }
    public float[]? Weights { get; init; }
    public List<float[]>? FitPoints { get; init; }
    public bool Closed { get; init; }

    public static EntityGeometrySnapshot Capture(CadEntity e) => new()
    {
        A = Clone(e.A),
        B = Clone(e.B),
        Center = Clone(e.Center),
        HalfExtents = Clone(e.HalfExtents),
        Radius = e.Radius,
        Height = e.Height,
        ControlPoints = e.ControlPoints?.Select(Clone!).Where(p => p is not null).Cast<float[]>().ToList(),
        Knots = Clone(e.Knots),
        Weights = Clone(e.Weights),
        FitPoints = e.FitPoints?.Select(Clone!).Where(p => p is not null).Cast<float[]>().ToList(),
        Closed = e.Closed,
    };

    public void ApplyTo(CadEntity e)
    {
        e.A = Clone(A);
        e.B = Clone(B);
        e.Center = Clone(Center);
        e.HalfExtents = Clone(HalfExtents);
        e.Radius = Radius;
        e.Height = Height;
        e.ControlPoints = ControlPoints?.Select(Clone!).Where(p => p is not null).Cast<float[]>().ToList();
        e.Knots = Clone(Knots);
        e.Weights = Clone(Weights);
        e.FitPoints = FitPoints?.Select(Clone!).Where(p => p is not null).Cast<float[]>().ToList();
        e.Closed = Closed;
    }

    private static float[]? Clone(float[]? v) => v is null ? null : (float[])v.Clone();
}
