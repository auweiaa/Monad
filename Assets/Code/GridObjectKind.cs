using UnityEngine;

public enum GridObjectKind
{
    Tower = 0,
    Resource = 1,
    Core = 2,
    Other = 3,
}

/// <summary>
/// Minimal interface for systems that reserve/query grid occupancy.
/// Kept small so gameplay systems can depend on it without needing a concrete GridManager type.
/// </summary>
public interface IGridOccupancy
{
    bool CanOccupyFootprint(Vector3Int originCell, Vector2Int size);
    bool TryRegister(GridOccupant occupant, Vector3Int originCell, Vector2Int size);
    void Unregister(GridOccupant occupant);
}