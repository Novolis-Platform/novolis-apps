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
