using UnityEngine;

public class Hazard : MonoBehaviour
{
    [SerializeField]
    private float fallSpeed = 3f;
    [SerializeField]
    private float roamSpeed = 1.5f;
    [SerializeField]
    private float minRoamTime = 6.0f; // Increased roam time (User request: roam longer)
    [SerializeField]
    private float maxRoamTime = 10.0f; // Increased roam time

    [Header("Effects")]
    [SerializeField]
    private AudioClip moveSound;
    [SerializeField]
    private GameObject bubbleParticlesPrefab;

    private Material bubbleMaterial;
    private Texture2D bubbleTexture;

    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer; // Cached reference

    private enum State { Dropping, Roaming, Retracting }
    private State currentState = State.Dropping;

    private float targetY;
    private int roamDirection = 0; // -1 left, 1 right
    
    private float lifeTimer = 0f;
    private float currentRoamDuration = 3f;
    private bool wasPaused = false;

    // Track particle system for toggling emission
    private ParticleSystem activeParticleSystem;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        // Setup Audio
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Audio settings
        if (audioSource != null)
        {
            audioSource.volume = 0.5f;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        // Play Drop Sound
        PlayMoveSound();
        
        // Setup Particles (Continuous Trail)
        if (bubbleParticlesPrefab != null)
        {
            // If user assigned a prefab, assume it's set up correctly, but ensure we parent it to the bait
            GameObject p = Instantiate(bubbleParticlesPrefab, transform.position, Quaternion.identity, transform);
            p.name = "HazardBubbles";
            SetupParticlePosition(p);
            
            activeParticleSystem = p.GetComponent<ParticleSystem>();
            if (activeParticleSystem != null)
            {
                 var main = activeParticleSystem.main;
                 main.loop = false; // Burst
                 main.playOnAwake = false;
                 activeParticleSystem.Stop();
            }
        }
        else
        {
            // Create manually if no prefab
            CreateTrailParticles();
        }

        // Fixed World Logic (Surface based)
        // Spawn is at Y=22 (from GridController). 
        // We want it to drop to a reasonable depth in the world.
        // World Bounds are approx -14 to 14.
        
        // MODIFIED: User provided longer sprites, so we can go deeper.
        // We randomize the target depth significantly now (-13 to 12).
        // The safety check below will prevent it from detaching from the surface if the sprite is too short.
        targetY = Random.Range(-13f, 12f); 

        // Safety Check: Ensure the top of the line doesn't go below the surface (Prevent "cut off" look)
        if (spriteRenderer != null)
        {
            float halfHeight = spriteRenderer.size.y / 2f;
            // Increased surfaceY to ensure the top of the line is well above the screen view.
            // Camera Top is approx 10-12. Surface is 14.5. Let's use 22.0 to be extremely safe.
            float surfaceY = 22.0f; 
            float minSafeY = surfaceY - halfHeight;
            
            // If targetY is lower than minSafeY (too deep), clamp it.
            // This ensures we use the full length of the new longer sprites without breaking visuals.
            if (targetY < minSafeY)
            {
                 targetY = minSafeY;
            }

            // --- VISUAL FIX FOR SPAWN ---
            // If the sprite is extremely long (e.g. 40 units), spawning at Y=22 (Center) puts Bottom at 2.
            // This means it would "pop" into view instantly visible in the middle of the screen.
            // We need to ensure the spawn position (Center) is high enough so the Bottom is > TopOfScreen.
            
            float topOfScreen = 15f; 
            float bottomY = transform.position.y - halfHeight;
            
            if (bottomY < topOfScreen)
            {
                // Shift Up immediately so it starts off-screen
                float requiredY = topOfScreen + halfHeight + 2f;
                transform.position = new Vector3(transform.position.x, requiredY, transform.position.z);
            }
        }

        ConfigureCollider();
    }

    private void SetupParticlePosition(GameObject particleObj)
    {
        // Position at the "Bait" (Bottom of sprite)
        if (spriteRenderer != null)
        {
            float spriteHeight = spriteRenderer.size.y;
            // Bait is roughly at the bottom center.
            // Offset: -(Height / 2)
            particleObj.transform.localPosition = new Vector3(0, -(spriteHeight / 2f) + 0.2f, 0);
        }
    }

    private void ConfigureCollider()
    {
        // Auto-adjust collider to only cover the "Bait" (bottom of the sprite)
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        // Remove PolygonCollider2D if present (it likely traces the line)
        PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
        if (poly != null) Destroy(poly);

        // Remove CapsuleCollider2D if present
        CapsuleCollider2D cap = GetComponent<CapsuleCollider2D>();
        if (cap != null) Destroy(cap);

        // Get or Add BoxCollider2D
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) box = gameObject.AddComponent<BoxCollider2D>();

        // Resize to bottom 8% of the sprite, and narrower width (30%) - Tighter fit per user request
        float spriteHeight = sr.size.y;
        float spriteWidth = sr.size.x;

        box.size = new Vector2(spriteWidth * 0.3f, spriteHeight * 0.08f);
        box.offset = new Vector2(0, -(spriteHeight / 2f) + (box.size.y / 2f));
        
        box.isTrigger = true; // Ensure it's a trigger for OnTriggerEnter in Player
    }



    private void Update()
    {
        // Pause Check
        bool currentPaused = (GameManager.instance != null && GameManager.Paused);

        if (currentPaused != wasPaused)
        {
            wasPaused = currentPaused;
            if (currentPaused)
            {
                if (audioSource != null) audioSource.Pause();
            }
            else
            {
                if (audioSource != null) audioSource.UnPause();
            }
        }

        if (currentPaused) return;

        // Distance-based Volume Fading
        if (audioSource != null && GameManager.instance != null && GameManager.instance.playerGameObject != null)
        {
             float dist = Vector3.Distance(transform.position, GameManager.instance.playerGameObject.transform.position);
             float maxDist = 20f; 
             // Volume: 0.5f at 0 dist, 0f at 20 dist
             float volume = Mathf.Clamp01(1f - (dist / maxDist)) * 0.5f; 
             audioSource.volume = volume;
        }

        if (currentState == State.Dropping)
        {
            // Move down
            transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);

            // Check if reached target depth
            if (transform.position.y <= targetY)
            {
                StartRoaming();
            }
        }
        else if (currentState == State.Roaming)
        {
            // Move horizontally
            transform.Translate(Vector3.right * roamDirection * roamSpeed * Time.deltaTime, Space.World);

            // Flip Sprite based on direction
            // Assumption: Sprite faces Right by default.
            // If moving Right (1), Scale X is positive. If Left (-1), Scale X is negative.
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (roamDirection > 0 ? 1 : -1);
            transform.localScale = s;

            // Keep in bounds (Bounce instead of Destroy so it can pull up later)
            KeepInBounds();
            
            // Check Lifetime
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= currentRoamDuration)
            {
                StartRetracting();
            }
        }
        else if (currentState == State.Retracting)
        {
            // Move up (Retract)
            transform.Translate(Vector3.up * fallSpeed * Time.deltaTime, Space.World); // Same speed as fall
            
            // Destroy if fully off screen top (Fixed World Position)
            // Use dynamic calculation to ensure full sprite clearance
            if (transform.position.y >= GetRetractTargetY())
            {
                Destroy(gameObject);
            }
        }
    }

    private void CreateTrailParticles()
    {
        GameObject bubbles = new GameObject("HazardBubbles");
        bubbles.transform.SetParent(transform, false);
        SetupParticlePosition(bubbles);

        activeParticleSystem = bubbles.AddComponent<ParticleSystem>();
        
        // Configure Particle System (Match Player Speed Boost)
        var main = activeParticleSystem.main;
        main.loop = true; 
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize = 0.15f;
        main.startLifetime = 0.8f;
        main.startSpeed = 0f;
        main.gravityModifier = -0.2f; // Float up

        var emission = activeParticleSystem.emission;
        emission.rateOverTime = 8f; 
        
        var shape = activeParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;


        // Assign Material
        var renderer = bubbles.GetComponent<ParticleSystemRenderer>();
        
        if (bubbleMaterial != null)
        {
            renderer.material = bubbleMaterial;
        }
        else if (bubbleTexture != null)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Mobile/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.mainTexture = bubbleTexture;
                renderer.material = mat;
            }
        }
        
        renderer.sortingOrder = 5;
    }

    private void StartRoaming()
    {
        currentState = State.Roaming;
        lifeTimer = 0f;
        
        // Pick random duration
        currentRoamDuration = Random.Range(minRoamTime, maxRoamTime);

        // Pick random direction (Left or Right)
        roamDirection = (Random.value > 0.5f) ? 1 : -1;

        // Stop Sound (Ensure drop sound doesn't bleed into roaming)
        if (audioSource != null) audioSource.Stop();

        // Start Particles (Roaming)
        if (activeParticleSystem != null)
        {
            activeParticleSystem.Play();
        }
    }

    private void StartRetracting()
    {
        currentState = State.Retracting;
        
        // Play Retract Sound (Reuse Move Sound)
        PlayMoveSound();

        // Stop Particles (Retracting)
        if (activeParticleSystem != null)
        {
            activeParticleSystem.Stop();
        }
    }
    
    // Safety check for Retract Logic
    private float GetRetractTargetY()
    {
        // Calculate safe height to ensure full sprite is off-screen
        float defaultRetractY = 25f; // Safe default
        
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
             // Bounds Max Y + Buffer
             // However, transform.position is center.
             // We want (CenterY - ExtentsY) > TopOfScreen
             // Or simpler: CenterY > TopOfScreen + ExtentsY
             
             float halfHeight = spriteRenderer.bounds.extents.y;
             // Top of screen is approx 12 (Camera Size 10 + 2 buffer)
             // Let's use 22f (Spawn Y) as base, but ensure we go high enough.
             
             // If we spawned at 22, we should return to at least 22.
             // But if the sprite is HUGE (e.g. 40 units tall), 22 might not be enough to clear the bottom?
             // Spawn Y 22 means Center is at 22. If height is 40, bottom is at 2. Visible!
             // So we must retract until Bottom > TopOfScreen.
             
             float topOfScreen = 15f; // Safe estimate
             return topOfScreen + halfHeight + 2f; 
        }
        
        return defaultRetractY;
    }
    
    public void Initialize(AudioClip sound, GameObject particles, Material mat, Texture2D tex)
    {
        if (moveSound == null) moveSound = sound;
        if (bubbleParticlesPrefab == null) bubbleParticlesPrefab = particles;
        if (bubbleMaterial == null) bubbleMaterial = mat;
        if (bubbleTexture == null) bubbleTexture = tex;
    }

    private void PlayMoveSound()
    {
        if (audioSource != null && moveSound != null)
        {
            audioSource.clip = moveSound;
            audioSource.Play();
        }
    }

    private void KeepInBounds()
    {
        // Fixed World Bounds (consistent with FishSchool)
        // Restricted further to ensure hook stays on screen
        float leftBound = -8f;
        float rightBound = 8f;

        // Bounce logic
        if (transform.position.x < leftBound && roamDirection < 0)
        {
            roamDirection = 1; // Turn Right
        }
        else if (transform.position.x > rightBound && roamDirection > 0)
        {
            roamDirection = -1; // Turn Left
        }
    }

    private void OnDestroy()
    {
        // Cleanup if needed
    }
}
