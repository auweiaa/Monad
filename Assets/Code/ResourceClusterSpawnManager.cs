using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class ResourceClusterSpawnManager : MonoBehaviour
{
    [Serializable]
    public class ResourceDefinition
    {
        [Tooltip("Prefab to spawn for this resource type (should be 1 tile / 1 cell).")]
        public GameObject prefab;

        [Tooltip("Probability (0..1) that each cluster attempt is actually spawned for this resource.")]
        [Range(0f, 1f)]
        public float weight = 1f;

        [Tooltip("How many clusters to attempt to spawn for this resource (inclusive).")]
        public Vector2Int clustersRange = new Vector2Int(3, 6);

        [Header("Patch settings")]
        [Tooltip("Approximate patch size as a cell-budget (inclusive). Higher = larger disk.\n" +
                 "Example: 30..80 will create a disk whose area is roughly 30..80 cells before filtering by ground/occupancy/collider.")]
        public Vector2Int patchCellBudgetRange = new Vector2Int(30, 80);

        [Tooltip("How densely to fill valid cells inside the patch (inclusive, 0..1).")]
        public Vector2 densityRange01 = new Vector2(0.35f, 0.7f);

        [Tooltip("Max attempts to find a valid start cell for each cluster.")]
        [Min(1)]
        public int maxStartPickAttempts = 50;
    }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Collider2D spawnArea;

    [Header("Spawn Output")]
    [Tooltip("Optional parent for spawned resources.")]
    [SerializeField] private Transform resourcesParent;

    [Header("Resource Cluster Rules")]
    [SerializeField] private List<ResourceDefinition> resources = new List<ResourceDefinition>();

    [Header("Generation")]
    [Tooltip("If true, resources will generate automatically on Start().")]
    [SerializeField] private bool generateOnStart = true;

    [Header("Start cell selection")]
    [Tooltip("If true, cluster start cells are chosen by sampling random world points inside the spawnArea collider.\n" +
             "This reduces grid-aliasing patterns that can look like diagonal banding on isometric maps.")]
    [SerializeField] private bool pickStartCellByWorldSampling = true;

    [Tooltip("Max attempts when pickStartCellByWorldSampling is enabled.")]
    [SerializeField, Min(1)] private int worldStartPickAttempts = 250;

    private readonly List<GameObject> spawned = new List<GameObject>();

    private const int MaxPatchRadiusCells = 60;
    private const int MaxPatchCellsScanned = 20000;

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
    }

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (gridManager == null)
        {
            Debug.LogWarning("ResourceClusterSpawnManager: no GridManager found in scene.");
            return;
        }

        if (spawnArea == null)
        {
            Debug.LogWarning("ResourceClusterSpawnManager: spawnArea is not assigned.");
            return;
        }

        if (resources == null || resources.Count == 0)
        {
            Debug.LogWarning("ResourceClusterSpawnManager: no resource definitions configured.");
            return;
        }

        var candidates = BuildCandidateCells();
        if (candidates.Count == 0)
        {
            Debug.LogWarning("ResourceClusterSpawnManager: no candidate cells found (check spawnArea placement and Ground tilemap).");
            return;
        }

        for (int i = 0; i < resources.Count; i++)
        {
            var def = resources[i];
            if (def == null || def.prefab == null)
            {
                continue;
            }

            int clustersToTry = RandomInclusive(def.clustersRange);
            clustersToTry = Mathf.Max(0, clustersToTry);

            for (int c = 0; c < clustersToTry; c++)
            {
                float chance = Mathf.Clamp01(def.weight);
                if (chance <= 0f || Random.value > chance)
                {
                    continue;
                }

                SpawnCluster(def, candidates);
            }
        }
    }

    [ContextMenu("Regenerate (Clear + Generate)")]
    public void Regenerate()
    {
        ClearSpawned();
        Generate();
    }

    [ContextMenu("Clear Spawned")]
    public void ClearSpawned()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            var go = spawned[i];
            if (go == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private List<Vector3Int> BuildCandidateCells()
    {
        var grid = gridManager.Grid;
        if (grid == null)
        {
            Debug.LogWarning("ResourceClusterSpawnManager: GridManager.Grid is null.");
            return new List<Vector3Int>();
        }

        Bounds b = spawnArea.bounds;
        Vector3Int minCell = grid.WorldToCell(b.min);
        Vector3Int maxCell = grid.WorldToCell(b.max);

        int minX = Mathf.Min(minCell.x, maxCell.x);
        int maxX = Mathf.Max(minCell.x, maxCell.x);
        int minY = Mathf.Min(minCell.y, maxCell.y);
        int maxY = Mathf.Max(minCell.y, maxCell.y);

        var candidates = new List<Vector3Int>((maxX - minX + 1) * (maxY - minY + 1));
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!gridManager.HasGround(cell))
                {
                    continue;
                }

                Vector3 center = gridManager.GetCellCenterWorld(cell);
                if (!spawnArea.OverlapPoint((Vector2)center))
                {
                    continue;
                }

                if (!gridManager.CanOccupyFootprint(cell, Vector2Int.one))
                {
                    continue;
                }

                candidates.Add(cell);
            }
        }

        return candidates;
    }

    private void SpawnCluster(ResourceDefinition def, List<Vector3Int> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return;
        }

        if (!TryPickStartCell(def, candidates, out var startCell))
        {
            return;
        }

        SpawnPatchFillCluster(def, startCell);
    }

    private void SpawnPatchFillCluster(ResourceDefinition def, Vector3Int startCell)
    {
        Vector2Int cellBudgetRange = def.patchCellBudgetRange;
        if (cellBudgetRange.x < 1 || cellBudgetRange.y < 1)
        {
            // Safety default (helps when adding new fields to an existing component).
            cellBudgetRange = new Vector2Int(30, 80);
        }

        int budget = Mathf.Max(1, RandomInclusive(cellBudgetRange));
        int cellRadius = CellBudgetToCellRadius(budget);

        float density = PickDensity01(def.densityRange01);

        // Compute a world-space circle radius that corresponds to roughly 'budget' cells.
        // Using world-space distance avoids isometric "diamond" clusters that look diagonal.
        Vector3 centerWorld = gridManager.GetCellCenterWorld(startCell);
        Vector3 vx = gridManager.GetCellCenterWorld(startCell + new Vector3Int(1, 0, 0)) - centerWorld;
        Vector3 vy = gridManager.GetCellCenterWorld(startCell + new Vector3Int(0, 1, 0)) - centerWorld;
        float cellArea = Mathf.Abs(vx.x * vy.y - vx.y * vy.x);
        if (cellArea <= 0.000001f)
        {
            cellArea = 1f;
        }
        float radiusWorld = Mathf.Sqrt((budget * cellArea) / Mathf.PI);
        float radiusWorldSqr = radiusWorld * radiusWorld;

        var validCells = new List<Vector3Int>();
        EnumeratePatchCellsWorldCircle(startCell, cellRadius, vx, vy, radiusWorldSqr, (cell) =>
        {
            if (!gridManager.HasGround(cell))
            {
                return true;
            }

            if (!gridManager.CanOccupyFootprint(cell, Vector2Int.one))
            {
                return true;
            }

            Vector3 center = gridManager.GetCellCenterWorld(cell);
            if (!spawnArea.OverlapPoint((Vector2)center))
            {
                return true;
            }

            validCells.Add(cell);
            return true;
        });

        if (validCells.Count == 0)
        {
            return;
        }

        int toSpawn = Mathf.RoundToInt(validCells.Count * density);
        toSpawn = Mathf.Clamp(toSpawn, 1, validCells.Count);

        Shuffle(validCells);
        for (int i = 0; i < toSpawn; i++)
        {
            TryPlace(def, validCells[i]);
        }
    }


    private bool TryPickStartCell(ResourceDefinition def, List<Vector3Int> candidates, out Vector3Int cell)
    {
        if (pickStartCellByWorldSampling && spawnArea != null)
        {
            int worldAttempts = Mathf.Max(1, worldStartPickAttempts);
            Bounds b = spawnArea.bounds;
            for (int i = 0; i < worldAttempts; i++)
            {
                var p = new Vector2(
                    Random.Range(b.min.x, b.max.x),
                    Random.Range(b.min.y, b.max.y)
                );

                if (!spawnArea.OverlapPoint(p))
                {
                    continue;
                }

                Vector3Int c = gridManager.WorldToCell(p);
                c.z = 0;

                if (!gridManager.HasGround(c))
                {
                    continue;
                }

                // Extra guard: ensure the cell center is inside too (helps near polygon edges).
                Vector3 center = gridManager.GetCellCenterWorld(c);
                if (!spawnArea.OverlapPoint((Vector2)center))
                {
                    continue;
                }

                if (!gridManager.CanOccupyFootprint(c, Vector2Int.one))
                {
                    continue;
                }

                cell = c;
                return true;
            }
        }

        int candidateAttempts = Mathf.Max(1, def.maxStartPickAttempts);
        for (int i = 0; i < candidateAttempts; i++)
        {
            var pick = candidates[Random.Range(0, candidates.Count)];
            if (!gridManager.CanOccupyFootprint(pick, Vector2Int.one))
            {
                continue;
            }
            cell = pick;
            return true;
        }

        cell = default;
        return false;
    }


    private bool TryPlace(ResourceDefinition def, Vector3Int cell)
    {
        if (!gridManager.CanOccupyFootprint(cell, Vector2Int.one))
        {
            return false;
        }

        Vector3 pos = gridManager.GetCellCenterWorld(cell);
        pos.z = 0f;

        Transform parent = resourcesParent != null ? resourcesParent : transform;
        GameObject go = Instantiate(def.prefab, pos, Quaternion.identity, parent);
        spawned.Add(go);

        // Prefer to rely on existing GridOccupant auto-registration (your prefabs already have it),
        // but add a fallback in case a prefab is missing GridOccupant.
        var occ = go.GetComponent<GridOccupant>();
        if (occ == null)
        {
            occ = go.AddComponent<GridOccupant>();
            occ.Configure(GridObjectKind.Resource, Vector2Int.one);
        }

        // If it didn't auto-register for any reason, try explicit registration.
        if (!occ.IsRegistered)
        {
            gridManager.TryRegister(occ, cell, Vector2Int.one);
        }

        return true;
    }

    private static int RandomInclusive(Vector2Int range)
    {
        int min = Mathf.Min(range.x, range.y);
        int max = Mathf.Max(range.x, range.y);
        return Random.Range(min, max + 1);
    }

    private static float PickDensity01(Vector2 densityRange01)
    {
        float min = Mathf.Min(densityRange01.x, densityRange01.y);
        float max = Mathf.Max(densityRange01.x, densityRange01.y);

        // Default safety if left unconfigured.
        if (max <= 0f)
        {
            min = 0.35f;
            max = 0.7f;
        }

        min = Mathf.Clamp01(min);
        max = Mathf.Clamp01(max);
        if (max < min)
        {
            (min, max) = (max, min);
        }

        return Random.Range(min, max);
    }

    private static int CellBudgetToCellRadius(int cellBudget)
    {
        // Use a conservative cell-radius upper bound for scanning.
        // The actual circle membership is decided in world-space.
        float r = Mathf.Sqrt(Mathf.Max(1, cellBudget));
        int radius = Mathf.Clamp(Mathf.CeilToInt(r), 1, MaxPatchRadiusCells);
        return radius;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void EnumeratePatchCellsWorldCircle(
        Vector3Int centerCell,
        int cellRadius,
        Vector3 vx,
        Vector3 vy,
        float radiusWorldSqr,
        Func<Vector3Int, bool> visitor)
    {
        cellRadius = Mathf.Clamp(cellRadius, 1, MaxPatchRadiusCells);

        int scanned = 0;
        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        {
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                // Approx world offset using basis vectors.
                Vector3 offset = vx * dx + vy * dy;
                if (offset.sqrMagnitude > radiusWorldSqr)
                {
                    continue;
                }

                scanned++;
                if (scanned > MaxPatchCellsScanned)
                {
                    return;
                }

                var cell = centerCell + new Vector3Int(dx, dy, 0);
                if (!visitor(cell))
                {
                    return;
                }
            }
        }
    }


    private void OnDrawGizmosSelected()
    {
        if (spawnArea == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Bounds b = spawnArea.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}


