using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Pathfinding")]
    [SerializeField] private string coreTag = "Core";
    [SerializeField] private float  waypointReachRadius = 0.25f;
    [SerializeField] private int    maxAStarIterations  = 2000;
    
    [Header("Repath Scheduling")]
    [Tooltip("How many enemies may recalculate their path per frame after a grid change.")]
    [SerializeField, Min(1)] private int repathsPerFrame = 2;

    [Header("Attack")]
    [SerializeField] private float attackRange    = 1.5f;
    [SerializeField] private float attackDamage   = 5f;
    [SerializeField] private float attackInterval = 1f;

    [Header("Animator Parameters")]
    [SerializeField] private string moveXParam       = "X";
    [SerializeField] private string moveYParam       = "Y";
    [SerializeField] private string speedParam       = "Speed";
    [SerializeField] private string lastMoveXParam   = "LastMoveX";
    [SerializeField] private string lastMoveYParam   = "LastMoveY";
    [SerializeField] private string isGatheringParam = "IsGathering";
    [SerializeField] private float  animDampTime     = 0.05f;

    // Private references
    private Rigidbody2D      rb2D;
    private Animator         animator;
    private Enemy            enemy;
    private PlacementManager placementManager;
    private GridManager      gridManager;
    private Grid             grid;
    private Transform        coreTransform;
    private Core             coreComponent;

    // Pathfinding state
    private List<Vector3> waypoints    = new List<Vector3>();
    private int           waypointIndex = 0;
    private bool          hasPath       = false;
    private bool          pathDirty     = false;
    private bool          queuedForRepath = false;

    private static readonly Queue<EnemyMovement> repathQueue = new Queue<EnemyMovement>();
    private static int repathProcessedFrame = -1;

    // Attack state
    private float attackTimer = 0f;
    private bool  isAttacking = false;

    // Animator
    private int          moveXHash, moveYHash, speedHash, lastMoveXHash, lastMoveYHash, isGatheringHash;
    private HashSet<int> paramHashes  = new HashSet<int>();
    private Vector2      lastNonZeroDir = Vector2.down;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private void Awake()
    {
        rb2D     = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        // Resolve Enemy on this object or parent so spawner speed applies when Enemy is on root (e.g. EnemyPawn > Knight hierarchy).
        enemy    = GetComponentInParent<Enemy>();

        if (rb2D != null)
        {
            rb2D.bodyType      = RigidbodyType2D.Kinematic;
            rb2D.gravityScale  = 0f;
            rb2D.constraints   = RigidbodyConstraints2D.FreezeRotation;
            rb2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        moveXHash       = Animator.StringToHash(moveXParam);
        moveYHash       = Animator.StringToHash(moveYParam);
        speedHash       = Animator.StringToHash(speedParam);
        lastMoveXHash   = Animator.StringToHash(lastMoveXParam);
        lastMoveYHash   = Animator.StringToHash(lastMoveYParam);
        isGatheringHash = Animator.StringToHash(isGatheringParam);

        if (animator != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                paramHashes.Add(p.nameHash);

        placementManager = FindFirstObjectByType<PlacementManager>();
        gridManager      = FindFirstObjectByType<GridManager>();
        grid             = FindFirstObjectByType<Grid>();

        GameObject coreObj = GameObject.FindGameObjectWithTag(coreTag);
        if (coreObj != null)
        {
            coreTransform = coreObj.transform;
            coreComponent = coreObj.GetComponent<Core>();
        }
    }

    private void Start()
    {
        // Recalculate whenever a tower is placed or destroyed.
        if (placementManager != null)
            placementManager.OnGridChanged += RequestRepath;

        // Recalculate whenever a resource/environment object appears or is depleted.
        if (gridManager != null)
            gridManager.GridChanged += RequestRepath;

        // Initial path on spawn.
        RecalculatePath();
    }

    private void OnDestroy()
    {
        if (placementManager != null)
            placementManager.OnGridChanged -= RequestRepath;

        if (gridManager != null)
            gridManager.GridChanged -= RequestRepath;
    }

    private void Update()
    {
        ProcessRepathQueueOncePerFrame();

        if (coreTransform == null) return;

        float distToCore = Vector2.Distance(transform.position, coreTransform.position);

        // Attack range: stop and damage the core.
        if (distToCore <= attackRange)
        {
            isAttacking  = true;
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackInterval)
            {
                attackTimer = 0f;
                DamageCore();
            }
            UpdateAnimator(Vector2.zero);
            return;
        }

        isAttacking = false;
        attackTimer = 0f;

        // No path found: accept defeat and stand still.
        if (!hasPath || waypoints.Count == 0)
        {
            UpdateAnimator(Vector2.zero);
            return;
        }

        if (enemy == null) enemy = GetComponentInParent<Enemy>();
        float speed = enemy != null ? enemy.MoveSpeed : 10f;
        float step  = Mathf.Max(0f, speed * Time.deltaTime);

        Vector2 startPos = rb2D != null ? rb2D.position : (Vector2)transform.position;
        Vector2 pos = startPos;
        Vector2 lastDelta = Vector2.zero;

        // Move with step clamping so we never overshoot a waypoint even on large deltaTime.
        int safety = 0;
        while (step > 0f && hasPath && waypointIndex < waypoints.Count && safety++ < 64)
        {
            Vector2 target = waypoints[waypointIndex];
            Vector2 to = target - pos;
            float dist = to.magnitude;

            // Skip already-reached waypoints (including when snapping).
            if (dist <= Mathf.Max(0.0001f, waypointReachRadius))
            {
                waypointIndex++;
                if (waypointIndex >= waypoints.Count)
                {
                    hasPath = false;
                }
                continue;
            }

            if (dist <= step)
            {
                // Consume the whole segment and continue to the next waypoint in the same frame.
                pos = target;
                lastDelta = to;
                step -= dist;

                waypointIndex++;
                if (waypointIndex >= waypoints.Count)
                {
                    hasPath = false;
                }
            }
            else
            {
                // Partial move toward the current waypoint.
                Vector2 delta = to * (step / dist);
                pos += delta;
                lastDelta = delta;
                step = 0f;
            }
        }

        if (rb2D != null)
            rb2D.MovePosition(pos);
        else
            transform.position = pos;

        Vector2 moveDir = lastDelta.sqrMagnitude > 0.000001f ? lastDelta.normalized : Vector2.zero;
        UpdateAnimator(moveDir);
    }

    // -----------------------------------------------------------------------
    // Path calculation
    // Called on: spawn, tower placed, tower destroyed
    // -----------------------------------------------------------------------
    private void RecalculatePath()
    {
        waypoints.Clear();
        waypointIndex = 0;
        hasPath       = false;

        if (grid == null || coreTransform == null) return;

        Vector3Int start = grid.WorldToCell(transform.position);
        Vector3Int end   = grid.WorldToCell(coreTransform.position);

        List<Vector3Int> cellPath = FindPath(start, end);
        if (cellPath == null || cellPath.Count == 0) return; // no path  give up

        foreach (Vector3Int cell in cellPath)
            waypoints.Add(grid.GetCellCenterWorld(cell));

        // Skip waypoints that we are already within reach of.
        while (waypointIndex < waypoints.Count &&
               Vector2.Distance(transform.position, waypoints[waypointIndex]) <= waypointReachRadius)
        {
            waypointIndex++;
        }

        if (waypointIndex >= waypoints.Count) return;
        hasPath = true;
    }

    private void RequestRepath()
    {
        pathDirty = true;
        hasPath = false; // pause until a fresh path is ready

        if (!queuedForRepath)
        {
            queuedForRepath = true;
            repathQueue.Enqueue(this);
        }
    }

    private void ProcessRepathQueueOncePerFrame()
    {
        if (Time.frameCount == repathProcessedFrame) return;
        repathProcessedFrame = Time.frameCount;

        int budget = Mathf.Max(1, repathsPerFrame);
        for (int i = 0; i < budget; i++)
        {
            if (repathQueue.Count == 0) break;
            EnemyMovement e = repathQueue.Dequeue();
            if (e == null) continue;

            e.queuedForRepath = false;
            if (!e.pathDirty) continue;

            e.pathDirty = false;
            e.RecalculatePath();
        }
    }

    // -----------------------------------------------------------------------
    // A* (4-directional cardinal)
    // -----------------------------------------------------------------------
    private static readonly Vector3Int[] Dirs =
    {
        new Vector3Int( 1,  0, 0),
        new Vector3Int(-1,  0, 0),
        new Vector3Int( 0,  1, 0),
        new Vector3Int( 0, -1, 0),
    };

    private List<Vector3Int> FindPath(Vector3Int start, Vector3Int end)
    {
        var heap     = new MinHeap();
        var gScore   = new Dictionary<Vector3Int, float>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var closed   = new HashSet<Vector3Int>();

        gScore[start] = 0f;
        heap.Push(start, Heuristic(start, end));

        int iter = 0;
        while (heap.Count > 0 && iter++ < maxAStarIterations)
        {
            Vector3Int cur = heap.Pop();
            if (cur == end) return Reconstruct(cameFrom, cur);
            if (!closed.Add(cur)) continue;

            foreach (Vector3Int d in Dirs)
            {
                Vector3Int nb = cur + d;
                if (closed.Contains(nb))        continue;
                if (IsBlocked(nb) && nb != end) continue;

                float g = gScore[cur] + 1f;
                if (!gScore.TryGetValue(nb, out float prev) || g < prev)
                {
                    gScore[nb]   = g;
                    cameFrom[nb] = cur;
                    heap.Push(nb, g + Heuristic(nb, end));
                }
            }
        }
        return null;
    }

    private bool IsBlocked(Vector3Int cell)
    {
        // No ground tile, or a tower is placed here.
        if (placementManager != null && !placementManager.IsCellWalkable(cell)) return true;
        // A resource/environment object occupies this cell.
        if (gridManager != null && gridManager.IsOccupied(cell)) return true;
        return false;
    }

    private static float Heuristic(Vector3Int a, Vector3Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static List<Vector3Int> Reconstruct(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int cur)
    {
        var path = new List<Vector3Int>();
        while (cameFrom.ContainsKey(cur)) { path.Add(cur); cur = cameFrom[cur]; }
        path.Add(cur);
        path.Reverse();
        return path;
    }

    // -----------------------------------------------------------------------
    // Binary min-heap
    // -----------------------------------------------------------------------
    private class MinHeap
    {
        private readonly List<(float f, Vector3Int cell)> data = new List<(float, Vector3Int)>();
        public int Count => data.Count;

        public void Push(Vector3Int cell, float f)
        {
            data.Add((f, cell));
            int i = data.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (data[p].f <= data[i].f) break;
                (data[p], data[i]) = (data[i], data[p]);
                i = p;
            }
        }

        public Vector3Int Pop()
        {
            Vector3Int top = data[0].cell;
            int last = data.Count - 1;
            data[0] = data[last];
            data.RemoveAt(last);
            for (int i = 0, n = data.Count;;)
            {
                int l = 2*i+1, r = 2*i+2, s = i;
                if (l < n && data[l].f < data[s].f) s = l;
                if (r < n && data[r].f < data[s].f) s = r;
                if (s == i) break;
                (data[i], data[s]) = (data[s], data[i]);
                i = s;
            }
            return top;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private void DamageCore()
    {
        if (coreComponent == null || coreComponent.IsDestroyed) return;
        coreComponent.TakeDamage(enemy != null ? enemy.AttackDamage : attackDamage);
    }

    private void UpdateAnimator(Vector2 moveDir)
    {
        if (animator == null) return;
        Vector2 animDir = moveDir.sqrMagnitude > 0.0001f ? SnapTo8(moveDir) : Vector2.zero;
        float   speed   = moveDir.magnitude;
        if (animDir.sqrMagnitude > 0.0001f) lastNonZeroDir = animDir;
        TrySetFloat(moveXHash,     animDir.x,        animDampTime);
        TrySetFloat(moveYHash,     animDir.y,        animDampTime);
        TrySetFloat(speedHash,     speed,            animDampTime);
        TrySetFloat(lastMoveXHash, lastNonZeroDir.x, animDampTime);
        TrySetFloat(lastMoveYHash, lastNonZeroDir.y, animDampTime);
        TrySetBool (isGatheringHash, isAttacking);
    }

    private static Vector2 SnapTo8(Vector2 dir)
    {
        float rad = Mathf.Round(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg / 45f) * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void TrySetFloat(int hash, float value, float damp)
    {
        if (paramHashes.Contains(hash)) animator.SetFloat(hash, value, damp, Time.deltaTime);
    }

    private void TrySetBool(int hash, bool value)
    {
        if (paramHashes.Contains(hash)) animator.SetBool(hash, value);
    }
}

