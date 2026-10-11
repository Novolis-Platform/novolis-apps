using System.Numerics;
using FrankMoat.Actors;

namespace FrankMoat.Levels;

internal sealed record EnemySpawnDefinition(EnemyKind Kind, Vector3 Position);
