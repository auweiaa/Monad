using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Target")]
    private Vector2 targetPosition;
    
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stoppingDistance = 0.5f;
    [SerializeField] private bool normalizeDirection = true;

    [Header("Pathfinding")]
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private Grid grid;
    [SerializeField] private float pathRecalculationInterval = 0.5f;
    [SerializeField] private int maxPathfindingIterations = 1000;
    
    [Header("Obstacle Avoidance")]
    [SerializeField] private float obstacleCheckDistance = 1f;
    [SerializeField] private float stuckCheckTime = 0.5f;
    [SerializeField] private float avoidanceForce = 2f;
    [SerializeField] private LayerMask obstacleLayer = -1;
    [SerializeField] private string coreTag = "Core";
    [SerializeField] private float coreProximityDistance = 5f;
    
    [Header("Attack Mode")]
    [SerializeField] private float attackModeDistance = 3f;
    [SerializeField] private bool isInAttackMode = false;
    
    [Header("Debug")]
    [SerializeField] private bool showPathGizmos = true;
    [SerializeField] private Color pathColor = Color.yellow;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb2D;
    [SerializeField] private Animator animator;

    [Header("Animator parameters (Blend Tree)")]
    [SerializeField] private string moveXParam = "X";
    [SerializeField] private string moveYParam = "Y";
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string lastMoveXParam = "LastMoveX";
    [SerializeField] private string lastMoveYParam = "LastMoveY";
    [Header("Animator parameters (Actions)")]
    [SerializeField] private string isGatheringParam = "IsGathering";

    [SerializeField] private float animatorDampTime = 0.05f;
    [SerializeField] private bool snapAnimatorToEightDirections = true;

    private Vector2 moveDir;
    private Vector2 lastNonZeroMoveDir = Vector2.down;

    // Obstacle avoidance tracking
    private Vector2 lastPosition;
    private float stuckTimer = 0f;
    private bool isAvoiding = false;
    private Vector2 avoidanceDirection;

    // Core reference
    private GameObject coreObject;
    
    // Pathfinding
    private List<Vector3Int> currentPath = new List<Vector3Int>();
    private int currentPathIndex = 0;
    private float pathRecalculationTimer = 0f;
    private Vector3Int lastTargetCell;

    private HashSet<int> animParamHashes;
    private int moveXHash;
    private int moveYHash;
    private int speedHash;
    private int lastMoveXHash;
    private int lastMoveYHash;
    private int isGatheringHash;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rb2D == null) rb2D = GetComponent<Rigidbody2D>();
        
        //if (animator == null)
        //{
        //    UnityEngine.Debug.LogError($"EnemyMovement on {gameObject.name}: No Animator component found! Animations will not work.");
        //}
        //else if (animator.runtimeAnimatorController == null)
        //{
        //    UnityEngine.Debug.LogError($"EnemyMovement on {gameObject.name}: Animator has no controller assigned! Animations will not work.");
        //}
        
        if (rb2D == null)
        {
            rb2D = gameObject.AddComponent<Rigidbody2D>();
            rb2D.gravityScale = 0f;
            rb2D.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        moveXHash = Animator.StringToHash(moveXParam);
        moveYHash = Animator.StringToHash(moveYParam);
        speedHash = Animator.StringToHash(speedParam);
        lastMoveXHash = Animator.StringToHash(lastMoveXParam);
        lastMoveYHash = Animator.StringToHash(lastMoveYParam);
        isGatheringHash = Animator.StringToHash(isGatheringParam);

        CacheAnimatorParameters();
        lastPosition = transform.position;
        
        // Find the core object and set target position
        coreObject = GameObject.FindGameObjectWithTag(coreTag);
        
        if (coreObject != null)
        {
            targetPosition = coreObject.transform.position;
        }
        //else
        //{
        //    UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Core object with tag '{coreTag}' not found!");
        //}
        
        // Find Grid and PlacementManager if not assigned
        if (grid == null) grid = FindFirstObjectByType<Grid>();
        if (placementManager == null) placementManager = FindFirstObjectByType<PlacementManager>();
        
        //if (grid == null)
        //{
        //    UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Grid not found!");
        //}
        //if (placementManager == null)
        //{
        //    UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: PlacementManager not found!");
        //}
    }

    private void CacheAnimatorParameters()
    {
        animParamHashes = new HashSet<int>();
        if (animator == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            animParamHashes.Add(p.nameHash);
        }
        
        // Debug log missing parameters
        //if (!animParamHashes.Contains(moveXHash))
        //    UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Animator parameter '{moveXParam}' not found!");
        //if (!animParamHashes.Contains(moveYHash))
        //    UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Animator parameter '{moveYParam}' not found!");
        //if (!animParamHashes.Contains(speedHash))
        //    UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Animator parameter '{speedParam}' not found!");
        //if (!animParamHashes.Contains(lastMoveXHash))
        //    UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Animator parameter '{lastMoveXParam}' not found!");
        //if (!animParamHashes.Contains(lastMoveYHash))
        //    UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Animator parameter '{lastMoveYParam}' not found!");
        if (!animParamHashes.Contains(isGatheringHash))
            UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Animator parameter '{isGatheringParam}' not found! Gathering animations will not play.");
            
        //UnityEngine.Debug.Log($"EnemyMovement on {gameObject.name}: Cached {animParamHashes.Count} animator parameters");
    }

    private void Update()
    {
        CheckIfStuck();
        UpdatePathfinding();
        CalculateMoveDirection();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        MoveCharacter();
    }

    private void UpdatePathfinding()
    {
        if (grid == null || coreObject == null) return;
        
        pathRecalculationTimer += Time.deltaTime;
        
        Vector3Int targetCell = grid.WorldToCell(coreObject.transform.position);
        
        // Recalculate path periodically or if target moved
        if (pathRecalculationTimer >= pathRecalculationInterval || targetCell != lastTargetCell)
        {
            pathRecalculationTimer = 0f;
            lastTargetCell = targetCell;
            CalculatePath();
        }
    }
    
    private void CalculateMoveDirection()
    {
        Vector2 currentPos = (Vector2)transform.position;
        
        // Use core position as target if available
        if (coreObject == null) return;
        
        Vector2 corePos = (Vector2)coreObject.transform.position;
        float distanceToCore = Vector2.Distance(currentPos, corePos);
        
        // Check if we should enter attack mode
        if (distanceToCore <= attackModeDistance)
        {
            if (!isInAttackMode)
            {
                isInAttackMode = true;
                //UnityEngine.Debug.Log($"[ATTACK MODE] {gameObject.name} entered ATTACK MODE! Distance to core: {distanceToCore:F2}");
            }
            //else
            //{
            //    // Log while in attack mode (every frame for debugging)
            //    UnityEngine.Debug.Log($"[ATTACK MODE] {gameObject.name} attacking core! Distance: {distanceToCore:F2}");
            //}
            // In attack mode, move directly toward core
            moveDir = (corePos - currentPos).normalized;
            
            Vector2 facingDir = GetAnimatorDirection();
            if (facingDir.sqrMagnitude > 0.0001f)
            {
                lastNonZeroMoveDir = facingDir;
            }
            return;
        }
        else
        {
            if (isInAttackMode)
            {
                isInAttackMode = false;
                //UnityEngine.Debug.Log($"{gameObject.name} exited attack mode");
            }
        }
        
        // Use pathfinding to navigate
        if (currentPath != null && currentPath.Count > 0 && currentPathIndex < currentPath.Count)
        {
            Vector3Int nextCell = currentPath[currentPathIndex];
            Vector3 nextWaypoint = grid.GetCellCenterWorld(nextCell);
            
            float distanceToWaypoint = Vector2.Distance(currentPos, nextWaypoint);
            
            if (distanceToWaypoint < 0.3f) // Close enough to waypoint
            {
                currentPathIndex++;
                if (currentPathIndex >= currentPath.Count)
                {
                    // Reached end of path
                    moveDir = Vector2.zero;
                    return;
                }
                nextCell = currentPath[currentPathIndex];
                nextWaypoint = grid.GetCellCenterWorld(nextCell);
            }
            
            Vector2 direction = ((Vector2)nextWaypoint - currentPos);
            moveDir = normalizeDirection ? direction.normalized : direction;
            
            Vector2 facingDir = GetAnimatorDirection();
            if (facingDir.sqrMagnitude > 0.0001f)
            {
                lastNonZeroMoveDir = facingDir;
            }
        }
        else
        {
            // No valid path, stop or use fallback
            moveDir = Vector2.zero;
        }
    }

    private void CalculatePath()
    {
        if (grid == null || coreObject == null)
        {
            currentPath.Clear();
            return;
        }
        
        Vector3Int startCell = grid.WorldToCell(transform.position);
        Vector3Int endCell = grid.WorldToCell(coreObject.transform.position);
        
        currentPath = FindPath(startCell, endCell);
        currentPathIndex = 0;
        
        //if (currentPath == null || currentPath.Count == 0)
        //{
        //    UnityEngine.Debug.LogWarning($"{gameObject.name}: No path found to core!");
        //}
    }
    
    private List<Vector3Int> FindPath(Vector3Int start, Vector3Int end)
    {
        // A* pathfinding implementation
        Dictionary<Vector3Int, float> gScore = new Dictionary<Vector3Int, float>();
        Dictionary<Vector3Int, float> fScore = new Dictionary<Vector3Int, float>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
        
        // Priority queue (simplified - using sorted list)
        List<Vector3Int> openSet = new List<Vector3Int> { start };
        
        gScore[start] = 0;
        fScore[start] = Heuristic(start, end);
        
        int iterations = 0;
        
        while (openSet.Count > 0 && iterations < maxPathfindingIterations)
        {
            iterations++;
            
            // Get node with lowest fScore
            Vector3Int current = openSet[0];
            float lowestF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;
            
            for (int i = 1; i < openSet.Count; i++)
            {
                float f = fScore.ContainsKey(openSet[i]) ? fScore[openSet[i]] : float.MaxValue;
                if (f < lowestF)
                {
                    lowestF = f;
                    current = openSet[i];
                }
            }
            
            if (current == end)
            {
                return ReconstructPath(cameFrom, current);
            }
            
            openSet.Remove(current);
            closedSet.Add(current);
            
            foreach (Vector3Int neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor)) continue;
                if (IsCellBlocked(neighbor) && neighbor != end) continue;
                
                float tentativeGScore = gScore[current] + 1; // Cost is 1 per cell
                
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, end);
                    
                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }
        
        // No path found
        return new List<Vector3Int>();
    }
    
    private float Heuristic(Vector3Int a, Vector3Int b)
    {
        // Manhattan distance
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
    
    private List<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>
        {
            cell + new Vector3Int(1, 0, 0),   // Right
            cell + new Vector3Int(-1, 0, 0),  // Left
            cell + new Vector3Int(0, 1, 0),   // Up
            cell + new Vector3Int(0, -1, 0),  // Down
        };
        
        return neighbors;
    }
    
    private bool IsCellBlocked(Vector3Int cell)
    {
        if (placementManager == null) return false;
        
        return placementManager.IsCellOccupied(cell);
    }
    
    private List<Vector3Int> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
    {
        List<Vector3Int> path = new List<Vector3Int> { current };
        
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        
        return path;
    }

    private void CheckIfStuck()
    {
        Vector2 currentPos = transform.position;
        float distanceMoved = Vector2.Distance(currentPos, lastPosition);
        
        // If we're trying to move but haven't moved much
        if (moveDir.sqrMagnitude > 0.01f && distanceMoved < 0.01f)
        {
            stuckTimer += Time.deltaTime;
            
            if (stuckTimer >= stuckCheckTime && !isAvoiding)
            {
                // We're stuck, start avoiding
                isAvoiding = true;
                avoidanceDirection = GetAvoidanceDirection(moveDir);
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
            if (distanceMoved > 0.1f)
            {
                isAvoiding = false;
            }
        }
        
        lastPosition = currentPos;
    }

    private bool DetectObstacle(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return false;
        
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, 
            direction.normalized, 
            obstacleCheckDistance,
            obstacleLayer
        );
        
        // Don't treat Core as an obstacle
        if (hit.collider != null && hit.collider.CompareTag(coreTag))
        {
            return false;
        }
        
        return hit.collider != null;
    }

    private Vector2 GetAvoidanceDirection(Vector2 blockedDirection)
    {
        if (blockedDirection.sqrMagnitude < 0.0001f)
            return Vector2.right;
        
        // Try perpendicular directions
        Vector2 perpRight = new Vector2(-blockedDirection.y, blockedDirection.x);
        Vector2 perpLeft = new Vector2(blockedDirection.y, -blockedDirection.x);
        
        // Check which perpendicular direction is clearer
        bool rightClear = !DetectObstacle(perpRight);
        bool leftClear = !DetectObstacle(perpLeft);
        
        if (rightClear && !leftClear)
            return perpRight.normalized;
        else if (leftClear && !rightClear)
            return perpLeft.normalized;
        else if (rightClear && leftClear)
            // Both clear, pick one (slightly biased by random)
            return (Random.value > 0.5f ? perpRight : perpLeft).normalized;
        else
            // Both blocked, try going back slightly at an angle
            return (-blockedDirection + perpRight * 0.5f).normalized;
    }

    private void MoveCharacter()
    {
        if (moveDir.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 delta = moveDir * moveSpeed * Time.fixedDeltaTime;

        if (rb2D != null)
        {
            rb2D.MovePosition(rb2D.position + delta);
        }
        else
        {
            transform.position += (Vector3)delta;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        Vector2 animDir = GetAnimatorDirection();
        float speed = moveDir.magnitude;

        TrySetFloat(moveXHash, animDir.x, animatorDampTime);
        TrySetFloat(moveYHash, animDir.y, animatorDampTime);

        TrySetFloat(speedHash, speed, animatorDampTime);

        TrySetFloat(lastMoveXHash, lastNonZeroMoveDir.x, animatorDampTime);
        TrySetFloat(lastMoveYHash, lastNonZeroMoveDir.y, animatorDampTime);
        
        // If your Animator has an "IsGathering" bool, drive it from the attack mode state.
        if (animParamHashes != null && animParamHashes.Contains(isGatheringHash))
        {
            animator.SetBool(isGatheringHash, isInAttackMode);
        }
        
        //// Debug logging (remove after fixing)
        //if (Time.frameCount % 60 == 0) // Log once per second at 60fps
        //{
        //    UnityEngine.Debug.Log($"Enemy {gameObject.name} - Speed: {speed:F2}, Dir: ({animDir.x:F2}, {animDir.y:F2}), MoveDir: ({moveDir.x:F2}, {moveDir.y:F2})");
        //}
    }

    private Vector2 GetAnimatorDirection()
    {
        Vector2 dir = moveDir;

        if (snapAnimatorToEightDirections && dir.sqrMagnitude > 0.0001f)
        {
            dir = SnapToEightDirections(dir);
        }

        return dir;
    }

    private Vector2 SnapToEightDirections(Vector2 dir)
    {
        if (dir.sqrMagnitude <= 0.0001f) return dir;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        float rad = snapped * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void TrySetFloat(int hash, float value, float dampTime)
    {
        if (animParamHashes != null && animParamHashes.Contains(hash))
        {
            animator.SetFloat(hash, value, dampTime, Time.deltaTime);
        }
    }
    
    // Debug visualization
    private void OnDrawGizmos()
    {
        if (!showPathGizmos || grid == null || currentPath == null || currentPath.Count == 0)
            return;
        
        Gizmos.color = pathColor;
        
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Vector3 current = grid.GetCellCenterWorld(currentPath[i]);
            Vector3 next = grid.GetCellCenterWorld(currentPath[i + 1]);
            Gizmos.DrawLine(current, next);
            Gizmos.DrawSphere(current, 0.1f);
        }
        
        if (currentPath.Count > 0)
        {
            Vector3 last = grid.GetCellCenterWorld(currentPath[currentPath.Count - 1]);
            Gizmos.DrawSphere(last, 0.1f);
        }
        
        // Draw current waypoint
        if (currentPathIndex < currentPath.Count)
        {
            Gizmos.color = Color.green;
            Vector3 waypoint = grid.GetCellCenterWorld(currentPath[currentPathIndex]);
            Gizmos.DrawWireSphere(waypoint, 0.3f);
        }
        
        // Draw attack mode radius
        if (coreObject != null)
        {
            Gizmos.color = isInAttackMode ? Color.red : new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(coreObject.transform.position, attackModeDistance);
        }
    }
}
