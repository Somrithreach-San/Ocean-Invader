using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Fish : MonoBehaviour
{
    private bool hasError = false;
    private Transform gfx;
    private Sprite image;

    [SerializeField]
    private int level = 0;
    public int Level => level;

    [SerializeField]
    private int xp = 0;
    public int Xp => xp;

    [SerializeField]
    private bool isFacingRight = true;
    public bool IsFacingRight => isFacingRight;

    [SerializeField]
    private float speed = 1f;
    public float Speed => speed;

    // Schooling
    public FishSchool school;
    public Vector2 formationOffset;

    private Vector3 initialScale;
    private bool initialized = false;

    // OPTIMIZATION: Static list to track all active fish without FindObjectsOfType
    public static List<Fish> AllFish = new List<Fish>();
    private GameManager cachedGameManager;
    private Transform cachedPlayerTransform;

    public void Die()
    {
        // Simple death logic
        Destroy(gameObject);
    }

    private void OnEnable()
    {
        AllFish.Add(this);
    }

    private void OnDisable()
    {
        AllFish.Remove(this);
    }

    private void Start()
    {
        // Cache references to avoid repeated singleton access
        cachedGameManager = GameManager.instance;
        if (cachedGameManager != null)
        {
            // Note: Player might respawn, so we might need to re-fetch if null
            // But for Start, this is good.
            if (cachedGameManager.playerGameObject != null)
                cachedPlayerTransform = cachedGameManager.playerGameObject.transform;
        }

        UpdateCollision();
    }

    private void UpdateCollision()
    {
        if (gfx == null) return;
        SpriteRenderer sr = gfx.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        // "Game Style" / "Feeding Frenzy" Collision
        // 1. Fit shape to sprite (Capsule is best for fish)
        // 2. Reduce size slightly (0.85f) for forgiveness

        // Check if we already have a CapsuleCollider2D
        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        
        // If we have other collider types (Box, Circle, Polygon), remove them to enforce Capsule
        Collider2D[] allCols = GetComponents<Collider2D>();
        foreach(var c in allCols)
        {
            if (c != capsule) Destroy(c);
        }

        // Add capsule if missing
        if (capsule == null)
        {
             capsule = gameObject.AddComponent<CapsuleCollider2D>();
        }

        capsule.isTrigger = true;

        // Calculate Bounds
        Bounds b = sr.sprite.bounds;
        Vector2 spriteSize = b.size;
        Vector2 spriteCenter = b.center;

        // Adjust for gfx scale relative to root
        // Note: We use Abs because collider size must be positive.
        // We assume gfx is a child of this transform (or the same).
        float scaleX = Mathf.Abs(gfx.localScale.x);
        float scaleY = Mathf.Abs(gfx.localScale.y);

        Vector2 finalSize = new Vector2(spriteSize.x * scaleX, spriteSize.y * scaleY);
        
        // Calculate Center Offset in Root Local Space
        // We use TransformPoint to get World Center, then InverseTransformPoint to get Root Local Center
        // This accounts for any offset of the graphics child.
        Vector3 worldCenter = gfx.TransformPoint(spriteCenter);
        Vector3 localCenter = transform.InverseTransformPoint(worldCenter);

        // Apply Forgiveness (0.85f) - "Feeding Frenzy" feel
        float forgiveness = 0.85f;
        
        capsule.size = finalSize * forgiveness;
        capsule.offset = localCenter;
        
        // Auto-Orientation
        if (finalSize.x >= finalSize.y)
            capsule.direction = CapsuleDirection2D.Horizontal;
        else
            capsule.direction = CapsuleDirection2D.Vertical;
    }

    private void Update()
    {
        // Despawn logic: 
        // Simply use distance from player. 
        // This allows fish to enter/exit the screen freely without hitting an invisible "world boundary" wall.
        
        // Refresh cached player if missing (e.g. after respawn)
        if (cachedPlayerTransform == null)
        {
            if (cachedGameManager == null) cachedGameManager = GameManager.instance;
            if (cachedGameManager != null && cachedGameManager.playerGameObject != null)
                cachedPlayerTransform = cachedGameManager.playerGameObject.transform;
        }

        if (cachedPlayerTransform != null)
        {
            // OPTIMIZATION: Use sqrMagnitude to avoid expensive square root calculation
            float distSqr = (transform.position - cachedPlayerTransform.position).sqrMagnitude;
            
            // Reduced distance from 80f to 35f (35*35 = 1225)
            if (distSqr > 1225f) 
            {
                Destroy(gameObject);
                return;
            }
        }
    }

    private void Awake()
    {
        gfx = GetComponentInChildren<SpriteRenderer>()?.transform;
        if (gfx != null)
        {
            image = gfx.GetComponent<SpriteRenderer>().sprite;
        }
        else
        {
            Debug.Log("Cant find child sprite renderer in fish");
            hasError = true;
        }

        if (!initialized)
        {
            initialScale = transform.localScale;
            initialized = true;
        }

        EvaluateFish();
    }



    public void SetLevel(int newLevel)
    {
        // USER REQUIREMENT: 1 Fish = 1 Level.
        // If this fish prefab already has a level set (e.g. 3) in the Inspector, 
        // we should NOT allow it to be downgraded/overridden to Level 1 or 2.
        // We only allow setting level if the current level is 0 (unassigned).
        if (level > 0)
        {
            // Already has a level. Ignore override to preserve "Fixed Size/Fixed Level" identity.
            return;
        }

        level = newLevel;
        if (!initialized)
        {
            initialScale = transform.localScale;
            initialized = true;
        }
        EvaluateFish();
    }

    private void EvaluateFish()
    {
        if (hasError) return;
        if (level <= 0) return;

        // USER REQUEST: Manual sizing only.
        // Removed programmatic scaling logic (GameManager.GetTargetScale).
        // The size set in the Inspector/Prefab is the final size.
    }

    public void Flip()
    {
        isFacingRight = !isFacingRight;
        transform.localScale = new Vector3(transform.localScale.x * -1f, transform.localScale.y, transform.localScale.z);
    }

    public void TurnLeft()
    {
        //Already looking left
        if (!isFacingRight) return;

        Flip();
    }

    public void TurnRight()
    {
        //Already looking right
        if (isFacingRight) return;
        Flip();
    }

    public void FlipTowardsDestination(Vector2 _destination, bool localSpace = true)
    {
        if(localSpace)
        {
            if (_destination.x < transform.localPosition.x)
                TurnLeft();
            else if(_destination.x > transform.localPosition.x)
                TurnRight();

            return;
        }
        else
        {
            if (_destination.x < transform.position.x)
                TurnLeft();
            else if (_destination.x > transform.position.x)
                TurnRight();
        }

        
    }


}

