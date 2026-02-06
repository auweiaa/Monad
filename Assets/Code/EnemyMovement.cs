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

    [Header("Obstacle Avoidance")]
    [SerializeField] private float obstacleCheckDistance = 1f;
    [SerializeField] private float stuckCheckTime = 0.5f;
    [SerializeField] private float avoidanceForce = 2f;
    [SerializeField] private LayerMask obstacleLayer = -1;
    [SerializeField] private string coreTag = "Core";
    [SerializeField] private float coreProximityDistance = 5f;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb2D;
    [SerializeField] private Animator animator;

    [Header("Animator parameters (Blend Tree)")]
    [SerializeField] private string moveXParam = "X";
    [SerializeField] private string moveYParam = "Y";
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string lastMoveXParam = "LastMoveX";
    [SerializeField] private string lastMoveYParam = "LastMoveY";

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

    private HashSet<int> animParamHashes;
    private int moveXHash;
    private int moveYHash;
    private int speedHash;
    private int lastMoveXHash;
    private int lastMoveYHash;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rb2D == null) rb2D = GetComponent<Rigidbody2D>();
        
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

        CacheAnimatorParameters();
        lastPosition = transform.position;
        
        // Find the core object and set target position
        coreObject = GameObject.FindGameObjectWithTag(coreTag);
        
        if (coreObject != null)
        {
            targetPosition = coreObject.transform.position;
        }
        else
        {
            UnityEngine.Debug.LogWarning($"EnemyMovement on {gameObject.name}: Core object with tag '{coreTag}' not found!");
        }
    }

    private void CacheAnimatorParameters()
    {
        animParamHashes = new HashSet<int>();
        if (animator == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            animParamHashes.Add(p.nameHash);
        }
    }

    private void Update()
    {
        CheckIfStuck();
        CalculateMoveDirection();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        MoveCharacter();
    }

    private void CalculateMoveDirection()
    {
        Vector2 currentPos = (Vector2)transform.position;
        
        // Use core position as target if available
        Vector2 actualTarget = targetPosition;
        if (coreObject != null)
        {
            actualTarget = (Vector2)coreObject.transform.position;
            
            float distanceToCore = Vector2.Distance(currentPos, actualTarget);
            if (distanceToCore <= coreProximityDistance)
            {
                // Stop when in proximity of core
                moveDir = Vector2.zero;
                UnityEngine.Debug.Log($"Distance to core: {distanceToCore}");
                return;
            }
        }
        
        float distanceToTarget = Vector2.Distance(currentPos, actualTarget);
        
        if (distanceToTarget > stoppingDistance)
        {
            Vector2 direction = (actualTarget - currentPos);
            moveDir = normalizeDirection ? direction.normalized : direction;
            
            // Check for obstacles and apply avoidance
            if (isAvoiding)
            {
                moveDir = avoidanceDirection;
            }
            else if (DetectObstacle(moveDir))
            {
                moveDir = GetAvoidanceDirection(moveDir);
            }
            
            Vector2 facingDir = GetAnimatorDirection();
            if (facingDir.sqrMagnitude > 0.0001f)
            {
                lastNonZeroMoveDir = facingDir;
            }
        }
        else
        {
            moveDir = Vector2.zero;
            isAvoiding = false;
        }
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
}
