using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class GridManager : MonoBehaviour, IGridOccupancy
{
    [Header("References")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    public event Action GridChanged;

    private readonly Dictionary<Vector3Int, GridOccupant> cellToOccupant = new Dictionary<Vector3Int, GridOccupant>();
    private readonly Dictionary<GridOccupant, List<Vector3Int>> occupantToCells = new Dictionary<GridOccupant, List<Vector3Int>>();

    public Grid Grid => grid;
    public Tilemap GroundTilemap => groundTilemap;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (grid == null)
        {
            grid = FindFirstObjectByType<Grid>();
        }

        if (groundTilemap == null)
        {
            var allTilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (var tm in allTilemaps)
            {
                if (tm != null && tm.name == "Ground")
                {
                    groundTilemap = tm;
                    break;
                }
            }

            if (groundTilemap == null && allTilemaps.Length > 0)
            {
                groundTilemap = allTilemaps[0];
            }
        }
    }

    public Vector3Int WorldToCell(Vector3 world)
    {
        if (grid == null)
        {
            EnsureReferences();
        }
        return grid != null ? grid.WorldToCell(world) : Vector3Int.zero;
    }

    public Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        if (grid == null)
        {
            EnsureReferences();
        }
        return grid != null ? grid.GetCellCenterWorld(cell) : Vector3.zero;
    }

    public bool HasGround(Vector3Int cell)
    {
        if (groundTilemap == null)
        {
            EnsureReferences();
        }
        return groundTilemap != null && groundTilemap.HasTile(cell);
    }

    public bool IsOccupied(Vector3Int cell)
    {
        return cellToOccupant.ContainsKey(cell);
    }

    public bool TryGetOccupant(Vector3Int cell, out GridOccupant occupant)
    {
        return cellToOccupant.TryGetValue(cell, out occupant);
    }

    public bool IsWalkable(Vector3Int cell)
    {
        return HasGround(cell) && !IsOccupied(cell);
    }

    public bool CanOccupyFootprint(Vector3Int originCell, Vector2Int size)
    {
        if (size.x < 1 || size.y < 1)
        {
            return false;
        }

        foreach (var cell in EnumerateFootprintCells(originCell, size))
        {
            if (!HasGround(cell))
            {
                return false;
            }
            if (IsOccupied(cell))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryRegister(GridOccupant occupant, Vector3Int originCell, Vector2Int size)
    {
        if (occupant == null)
        {
            return false;
        }

        if (size.x < 1 || size.y < 1)
        {
            Debug.LogWarning($"GridManager: refusing to register '{occupant.name}' with invalid footprint {size}.");
            return false;
        }

        if (occupantToCells.ContainsKey(occupant))
        {
            Debug.LogWarning($"GridManager: '{occupant.name}' is already registered. Unregister first.");
            return false;
        }

        // Validate first.
        if (!CanOccupyFootprint(originCell, size))
        {
            Debug.LogWarning($"GridManager: cannot register '{occupant.name}' at {originCell} with size {size} (blocked or no ground).");
            return false;
        }

        // Commit.
        var cells = new List<Vector3Int>(size.x * size.y);
        foreach (var cell in EnumerateFootprintCells(originCell, size))
        {
            cellToOccupant[cell] = occupant;
            cells.Add(cell);
        }

        occupantToCells[occupant] = cells;
        occupant.NotifyRegistered(this, originCell, size);

        GridChanged?.Invoke();
        return true;
    }

    public void Unregister(GridOccupant occupant)
    {
        if (occupant == null)
        {
            return;
        }

        if (!occupantToCells.TryGetValue(occupant, out var cells))
        {
            return;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int cell = cells[i];
            if (cellToOccupant.TryGetValue(cell, out var current) && current == occupant)
            {
                cellToOccupant.Remove(cell);
            }
        }

        occupantToCells.Remove(occupant);
        occupant.NotifyUnregistered(this);

        GridChanged?.Invoke();
    }

    public IEnumerable<Vector3Int> GetNeighbors8(Vector3Int cell, bool requireWalkable = true)
    {
        // 8-way (includes diagonals).
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var n = cell + new Vector3Int(dx, dy, 0);
                if (requireWalkable)
                {
                    if (IsWalkable(n))
                    {
                        yield return n;
                    }
                }
                else
                {
                    if (HasGround(n))
                    {
                        yield return n;
                    }
                }
            }
        }
    }

    private static IEnumerable<Vector3Int> EnumerateFootprintCells(Vector3Int originCell, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                yield return originCell + new Vector3Int(x, y, 0);
            }
        }
    }
}

