using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{

    [SerializeField] private ResourceManager resourceManager;
    [Header("Resources")]
    [SerializeField] private float collectRange = 1.5f;
    [SerializeField] private LayerMask collectLayer;
    [SerializeField] private float collectDelay = 0.5f;
    [SerializeField] private float collectCooldownTime = 1f;
    private Coroutine collectCoroutine;
    [SerializeField] private float gatherTargetCheckInterval = 0.1f;
    private float nextGatherTargetCheckTime;
    private bool hasGatherTargetCached;

    [Header("Placement")]
    [SerializeField, Min(0f)] private float placementRange = 5f;

    [Header("Movement")]
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool normalizeInput = true;
    [SerializeField] private bool lockMovementWhileGathering = false;

    [Header("Dust (direction change)")]
    [SerializeField] private Transform dustAnchor;
    [SerializeField] private float dustDirectionEpsilon = 0.0001f;
    private Vector2 lastDustEmitDir = Vector2.zero;
    private Coroutine dustStopCoroutine;
    private Vector3 dustAnchorLocalOffset;

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
    [SerializeField] private bool useRawInputForAnimator = true;
     [SerializeField] private bool snapAnimatorToEightDirections = true;

    private Vector2 input;
    private Vector2 moveDir;
    private Vector2 lastNonZeroMoveDir = Vector2.down;

    private HashSet<int> animParamHashes;
    private int moveXHash;
    private int moveYHash;
    private int speedHash;
    private int lastMoveXHash;
    private int lastMoveYHash;
    private int isGatheringHash;

    public float PlacementRange => placementRange;

    void Reset()
    {
        animator = GetComponent<Animator>();
        rb2D = GetComponent<Rigidbody2D>();
    }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rb2D == null)rb2D = GetComponent<Rigidbody2D>();
        if (dustAnchor == null) dustAnchor = (rb2D != null) ? rb2D.transform : transform;
        if (dustParticles != null)
        {
            if (dustAnchor != null)
            {
                dustAnchorLocalOffset = dustAnchor.InverseTransformPoint(dustParticles.transform.position);
            }
            dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dustParticles.Clear(true);
        }

        moveXHash = Animator.StringToHash(moveXParam);
        moveYHash = Animator.StringToHash(moveYParam);
        speedHash = Animator.StringToHash(speedParam);
        lastMoveXHash = Animator.StringToHash(lastMoveXParam);
        lastMoveYHash = Animator.StringToHash(lastMoveYParam);
        isGatheringHash = Animator.StringToHash(isGatheringParam);

        CacheAnimatorParameters();
    }

    void Update()
    {
        HandleCollectInput();
        ReadInput();
        HandleDustOnDirectionChange();
        UpdateAnimator();   
    }

    private void HandleCollectInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (collectCoroutine == null) collectCoroutine = StartCoroutine(CollectResourcesCoroutine());
        }
        else if (Input.GetKeyUp(KeyCode.Space)) StopCollecting();
    }

    private void StopCollecting()
    {
        if (collectCoroutine != null)
        {
            StopCoroutine(collectCoroutine);
            collectCoroutine = null;
        }
    }

    private void OnDisable()
    {
        StopCollecting();
        StopDustStopCoroutine();
        if (dustParticles != null)
        {
            dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dustParticles.Clear(true);
        }
    }

    IEnumerator CollectResourcesCoroutine()
    {

        //yield return new WaitForSeconds(collectDelay);

        var wait = new WaitForSeconds(Mathf.Max(0.01f, collectCooldownTime));

        while (Input.GetKey(KeyCode.Space))
        {
            yield return wait;
            CollectResources();
        }

        collectCoroutine = null;
    }

    void FixedUpdate()
    {
        MoveCharacter();
    }

    private void ReadInput()
    {
        input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        moveDir = normalizeInput ? input.normalized : input;

         Vector2 facingDir = GetAnimatorDirection();
         if (facingDir.sqrMagnitude > 0.0001f)
        {
            lastNonZeroMoveDir = facingDir;
        }
    }

    private void HandleDustOnDirectionChange()
    {
        if (dustParticles == null)
        {
            return;
        }

        bool movementLocked = lockMovementWhileGathering && Input.GetKey(KeyCode.Space);
        bool isMoving = !movementLocked && moveDir.sqrMagnitude > dustDirectionEpsilon;

        if (!isMoving)
        {
            // Reset so the next idle->move transition triggers a dust burst.
            lastDustEmitDir = Vector2.zero;
            return;
        }

        Vector2 dir = GetAnimatorDirection();
        if (dir.sqrMagnitude <= dustDirectionEpsilon)
        {
            return;
        }

        // Emit dust when starting movement or changing direction while moving.
        if ((dir - lastDustEmitDir).sqrMagnitude > dustDirectionEpsilon)
        {
            SoundManager.Instance.PlaySfx2D("walk");
            EmitDustOpposite(dir);
            lastDustEmitDir = dir;
        }
    }

    private void EmitDustOpposite(Vector2 moveDirection)
    {
        if (dustParticles == null)
        {
            return;
        }

        Vector2 dir = moveDirection.normalized;
        if (dir.sqrMagnitude <= dustDirectionEpsilon)
        {
            return;
        }

        // Ensure the emitter is at the correct position even if the movement Rigidbody2D is on a different transform.
        if (dustAnchor != null)
        {
            dustParticles.transform.position = dustAnchor.TransformPoint(dustAnchorLocalOffset);
        }

        // Rotate opposite to movement direction. Assumes ParticleSystem's local +Y (up) is its forward emission axis.
        dustParticles.transform.up = -dir;

        // Use the ParticleSystem's own Burst settings.
        dustParticles.Clear(true);
        dustParticles.Play(true);

        // Ensure it doesn't loop emit; stop emitting right after the burst while keeping spawned particles alive.
        StopDustStopCoroutine();
        dustStopCoroutine = StartCoroutine(StopDustEmittingNextFrame());
    }

    private IEnumerator StopDustEmittingNextFrame()
    {
        yield return null;

        if (dustParticles != null)
        {
            dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        dustStopCoroutine = null;
    }

    private void StopDustStopCoroutine()
    {
        if (dustStopCoroutine != null)
        {
            StopCoroutine(dustStopCoroutine);
            dustStopCoroutine = null;
        }
    }

    private void MoveCharacter()
    {
        if (lockMovementWhileGathering && Input.GetKey(KeyCode.Space))
        {
            return;
        }

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

        // If your Animator has an "IsGathering" bool, drive it from the collect input.
        if (animParamHashes != null && animParamHashes.Contains(isGatheringHash))
        {
            animator.SetBool(isGatheringHash, CanGatherNow());
        }
    }

    private bool CanGatherNow()
    {
        // Only gather if the player is holding the collect key AND there is something collectable in range.
        if (!Input.GetKey(KeyCode.Space))
        {
            hasGatherTargetCached = false;
            return false;
        }

        if (Time.time >= nextGatherTargetCheckTime)
        {
            hasGatherTargetCached = HasNonDepletedResourceInRange();
            nextGatherTargetCheckTime = Time.time + Mathf.Max(0.01f, gatherTargetCheckInterval);
        }

        return hasGatherTargetCached;
    }

    private bool HasNonDepletedResourceInRange()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, collectRange, collectLayer);
        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        foreach (var c in colliders)
        {
            if (c == null)
            {
                continue;
            }

            var resource = c.GetComponent<Resource>();
            if (resource != null && !resource.IsDepleted())
            {
                return true;
            }
        }

        return false;
    }
 
     private Vector2 GetAnimatorDirection()
     {
         Vector2 dir = useRawInputForAnimator ? input : moveDir;
 
         if (!snapAnimatorToEightDirections || !useRawInputForAnimator)
         {
             return dir;
         }
         return new Vector2(SnapAxis(dir.x), SnapAxis(dir.y));
     }
 
     private static float SnapAxis(float v)
     {
         if (Mathf.Abs(v) < 0.0001f)
         {
             return 0f;
         }
         return Mathf.Sign(v); // -1 or +1
     }

    private void CacheAnimatorParameters()
    {
        animParamHashes = new HashSet<int>();
        if (animator == null)
        {
            return;
        }

        foreach (var p in animator.parameters)
        {
            animParamHashes.Add(p.nameHash);
        }
    }

    private void TrySetFloat(int paramHash, float value, float dampTime)
    {
        if (animParamHashes == null || !animParamHashes.Contains(paramHash))
        {
            return;
        }

        animator.SetFloat(paramHash, value, dampTime, Time.deltaTime);
    }


    private void CollectResources()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, collectRange, collectLayer);
        Resource closest = null;
        float bestSqrDist = float.PositiveInfinity;
        Vector2 myPos = transform.position;

        foreach (Collider2D collider in colliders)
        {
            Resource resource = collider.GetComponent<Resource>();
            if (resource != null && !resource.IsDepleted())
            {
                float sqrDist = ((Vector2)collider.transform.position - myPos).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    closest = resource;
                }
            }
        }

        // Collect ONLY one resource per tick (closest valid resource in range).
        if (closest == null)
        {
            return;
        }

        switch (closest.GetResourceType())
        {
            case Resource.resourceType.wood:
                CollectWood();
                break;
            case Resource.resourceType.stone:
                CollectStone();
                break;
            case Resource.resourceType.iron:
                CollectIron();
                break;
            case Resource.resourceType.gold:
                CollectGold();
                break;
        }

        closest.CollectResource();
    }

    private void CollectWood()
    {
        SoundManager.Instance.PlaySfx2D("wood");
        if (resourceManager != null) resourceManager.SetWoodAmount(5);
    }
    private void CollectStone()
    {
        SoundManager.Instance.PlaySfx2D("stone");
        if (resourceManager != null) resourceManager.SetStoneAmount(1);
    }
    private void CollectIron()
    {
        SoundManager.Instance.PlaySfx2D("stone");
        if (resourceManager != null) resourceManager.SetIronAmount(1);
    }
    private void CollectGold()
    {
        SoundManager.Instance.PlaySfx2D("stone");
        if (resourceManager != null) resourceManager.SetGoldAmount(1);
    }
}
