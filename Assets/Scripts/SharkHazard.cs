using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class SharkHazard : MonoBehaviour
{
    [Header("Shark Settings")]
    [SerializeField]
    private float moveSpeed = 8f;
    [SerializeField]
    private float lifeTimeAfterPass = 5.0f;

    [Header("Effects")]
    [SerializeField]
    private AudioClip warningSound;
    [SerializeField]
    private AudioClip attackSound;
    [SerializeField]
    private ParticleSystem eatEffect;
    [SerializeField]
    private ParticleSystem trailEffect;
    [SerializeField]
    private Material bubbleMaterial;
    [SerializeField]
    private Texture2D bubbleTexture;

    // Dependencies
    private GameObject warningIcon; // Kept for reference, but might point to shared icon
    private AudioSource audioSource;
    
    private int direction = 1; // 1 = Right, -1 = Left
    private bool isCharging = false;
    private bool hasPassedScreen = false;

    // References
    private Camera cam;
    private Animator animator; // Add Animator reference

    // State
    private float chargeY;
    private Rigidbody2D rb;

    // Cache
    private static Shader cachedParticleShader;

    // --- STATIC WARNING UI SYSTEM ---
    private static Canvas _sharedCanvas;
    private static Image _sharedIconImage;
    private static RectTransform _sharedIconRect;
    private static GameObject _sharedCanvasObj;

    public void Initialize(int dir, GameObject iconPrefab, Sprite iconSprite, AudioClip warnClip, AudioClip atkClip, Material mat = null, Texture2D tex = null)
    {
        direction = dir;
        warningSound = warnClip;
        attackSound = atkClip;
        bubbleMaterial = mat;
        bubbleTexture = tex;
        
        // Setup Animator
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
            // If you have specific triggers, set them here. 
            // Usually "Idle" or "Swim" is default.
        }

        // Setup Audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        
        // Setup Eat Particles
        // Optimization: Only create if null. The heavy allocation is here.
        if (eatEffect == null)
        {
             CreateEatParticles();
        }

        if (trailEffect == null)
        {
             SetupTrailParticles();
        }
        
        // Enforce Physics Constraints
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        rb.gravityScale = 0f;

        // Optimized Collider (More Realistic)
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            // Get original sprite size if possible
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                 // Target: 90% Width, 40% Height (Body only, ignore fins/empty space)
                 Vector2 spriteSize = sr.sprite.bounds.size;
                 col.size = new Vector2(spriteSize.x * 0.9f, spriteSize.y * 0.4f);
                 
                 // Offset: Center Y, maybe slight X offset?
                 col.offset = Vector2.zero;
            }
            else
            {
                 // Fallback if no sprite
                 col.size = new Vector2(col.size.x * 0.9f, col.size.y * 0.4f);
            }
        }

        cam = Camera.main;

        // Flip Sprite if moving Left
        if (direction < 0)
        {
            Vector3 s = transform.localScale;
            s.x = -Mathf.Abs(s.x);
            transform.localScale = s;
        }
        else
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x);
            transform.localScale = s;
        }

        // Create Warning Icon
        if ((iconPrefab != null || iconSprite != null) && cam != null)
        {
            StartCoroutine(ShowWarningRoutine(iconPrefab, iconSprite));
        }
        else
        {
            StartCharging();
        }
    }

    private void EnsureSharedCanvas()
    {
        if (_sharedCanvasObj == null)
        {
            _sharedCanvasObj = new GameObject("SharkWarningCanvas_Shared");
            DontDestroyOnLoad(_sharedCanvasObj); // Persist across scenes
            
            _sharedCanvas = _sharedCanvasObj.AddComponent<Canvas>();
            _sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _sharedCanvas.sortingOrder = 999;
            
            CanvasScaler scaler = _sharedCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); // Standardize

            GameObject iconObj = new GameObject("SharedIconImage");
            iconObj.transform.SetParent(_sharedCanvasObj.transform);
            
            _sharedIconImage = iconObj.AddComponent<Image>();
            _sharedIconImage.raycastTarget = false;
            
            _sharedIconRect = iconObj.GetComponent<RectTransform>();
            _sharedIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            _sharedIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            _sharedIconRect.pivot = new Vector2(0.5f, 0.5f);
            
            // Default hidden
            _sharedCanvasObj.SetActive(false);
        }
    }

    private IEnumerator ShowWarningRoutine(GameObject iconPrefab, Sprite iconSprite)
    {
        // === APPROACH 3: UI CANVAS OVERLAY (Guaranteed Visibility) ===
        // Optimized: Reuse static canvas
        EnsureSharedCanvas();
        
        // Reset State
        _sharedCanvasObj.SetActive(true);
        _sharedIconImage.enabled = true;
        _sharedIconImage.color = Color.white;
        
        // Set Sprite
        if (iconPrefab != null)
        {
             Sprite s = null;
             if (iconPrefab.TryGetComponent<SpriteRenderer>(out var sr)) s = sr.sprite;
             else if (iconPrefab.TryGetComponent<Image>(out var prefabImg)) s = prefabImg.sprite;
             
             if (s != null) _sharedIconImage.sprite = s;
        }
        else if (iconSprite != null)
        {
            _sharedIconImage.sprite = iconSprite;
        }

        // Size
        _sharedIconRect.sizeDelta = new Vector2(100, 100);
        if (_sharedIconImage.sprite != null)
        {
            float aspect = _sharedIconImage.sprite.rect.width / _sharedIconImage.sprite.rect.height;
            _sharedIconRect.sizeDelta = new Vector2(100 * aspect, 100);
        }

        // Play Warning Sound (Looping while warning is active)
        if (audioSource != null && warningSound != null)
        {
            audioSource.clip = warningSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // START MOVING IMMEDIATELY
        // The shark spawns far away (-55/55), so we start it moving now.
        // The warning will persist until it actually reaches the screen.
        isCharging = true;
        chargeY = transform.position.y;
        
        // Start Trail
        if (trailEffect != null)
        {
            trailEffect.Play();
        }

        // Flash Warning UNTIL shark is visible
        float timer = 0f;
        float accumulatedTime = 0f;
        
        bool isOffScreen = true;

        while (isOffScreen)
        {
            if (cam == null) break;

            // Check if shark has entered the screen view
            float camHeight = 2f * cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;
            float halfWidth = camWidth / 2f;
            
            // Add a small buffer (e.g. 2 units) so the warning disappears JUST as it enters
            // We calculate distance covered in 1 second (speed * 1s) and add it to the threshold to dismiss early.
            float earlyDismissBuffer = moveSpeed * 1.0f;
            float distX = Mathf.Abs(transform.position.x - cam.transform.position.x);
            
            if (distX < (halfWidth + 2f + earlyDismissBuffer))
            {
                isOffScreen = false;
            }

            timer += Time.deltaTime;
            
            // ... (Rest of UI logic)
            
            // Update Y position
            if (cam != null && _sharedCanvas != null)
            {
                RectTransform canvasRect = _sharedCanvas.GetComponent<RectTransform>();
                float canvasWidth = canvasRect.rect.width;
                float canvasHeight = canvasRect.rect.height;
                float iconWidth = _sharedIconRect.rect.width;

                // Position Logic in Canvas Space
                // User Request: "literally almost touch the screen border"
                // 40f was too close (clipped), 45f was too far. 
                // Reduced percentage to prevent large gaps on tablets.
                float screenPadding = Mathf.Max(42f, canvasWidth * 0.02f);
                
                // Calculate X based on direction
                // direction > 0 (Moving Right) -> Comes from Left -> Show on Left Edge
                // direction < 0 (Moving Left) -> Comes from Right -> Show on Right Edge
                
                float targetX = 0f;
                if (direction > 0)
                {
                    // Left Edge
                    targetX = -(canvasWidth / 2f) + (iconWidth / 2f) + screenPadding;
                }
                else
                {
                    // Right Edge
                    targetX = (canvasWidth / 2f) - (iconWidth / 2f) - screenPadding;
                }
                
                // Calculate Y
                // WorldToScreenPoint gives pixels (0 to Screen.width).
                // We need to convert this to Canvas Space.
                Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
                
                // Normalized Y (0 to 1)
                float normalizedY = screenPos.y / Screen.height;
                
                // Map to Canvas Height (-Height/2 to Height/2)
                float targetY = (normalizedY - 0.5f) * canvasHeight;
                
                // Clamp Icon to Screen Height (with padding) so player knows shark is above/below
                float yLimit = (canvasHeight / 2f) - (_sharedIconRect.rect.height / 2f) - 20f;
                targetY = Mathf.Clamp(targetY, -yLimit, yLimit);

                _sharedIconRect.anchoredPosition = new Vector2(targetX, targetY);
            }

            // Blink & Pulse effect
            if (_sharedIconImage != null)
            {
                // Pulse based on proximity (closer = faster)
                // Normalize distance (55 max, 10 min)
                float distFactor = Mathf.Clamp01(1f - (distX / 60f)); 
                float frequency = Mathf.Lerp(4f, 20f, distFactor);
                
                accumulatedTime += Time.deltaTime * frequency;

                float alpha = Mathf.PingPong(accumulatedTime, 1f);
                // Min alpha 0.2 to never fully disappear
                alpha = Mathf.Lerp(0.2f, 1f, alpha);
                
                Color col = _sharedIconImage.color;
                col.a = alpha;
                _sharedIconImage.color = col;
                
                // Pulse Scale
                float scale = Mathf.Lerp(1f, 1.3f, alpha);
                _sharedIconRect.localScale = Vector3.one * scale;
            }
            yield return null;
        }
        
        // Cleanup Warning (Hide instead of Destroy)
        _sharedCanvasObj.SetActive(false);

        // Stop Warning Sound
        if (audioSource != null && audioSource.isPlaying && audioSource.clip == warningSound)
        {
            audioSource.Stop();
        }

        // Play Attack Sound (Now that it's here!)
        if (audioSource != null && attackSound != null)
        {
            audioSource.PlayOneShot(attackSound);
        }
    }

    private void StartCharging()
    {
        isCharging = true;
        chargeY = transform.position.y;
        if (trailEffect != null) trailEffect.Play();
    }

    private void Update()
    {
        if (!isCharging) return;
        if (GameManager.instance != null && GameManager.Paused) return;

        // Move across screen (Strictly Horizontal)
        float newX = transform.position.x + (direction * moveSpeed * Time.deltaTime);
        transform.position = new Vector3(newX, chargeY, 0f);

        // Check bounds to destroy after passing
        if (!hasPassedScreen && cam != null)
        {
            float camHeight = 2f * cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;
            float halfWidth = camWidth / 2f;
            
            float buffer = 5f;
            float rightEdge = cam.transform.position.x + halfWidth + buffer;
            float leftEdge = cam.transform.position.x - halfWidth - buffer;

            if ((direction > 0 && transform.position.x > rightEdge) || 
                (direction < 0 && transform.position.x < leftEdge))
            {
                hasPassedScreen = true;
                Destroy(gameObject, lifeTimeAfterPass); // Cleanup shortly after
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent<PlayerController>(out var pc))
            {
                pc.Death();
            }
        }
        else if (other.CompareTag("Enemy"))
        {
            if (other.TryGetComponent<Fish>(out var fish))
            {
                fish.Die(); 
                PlayEatEffect();
            }
        }
    }

    private void SetupTrailParticles()
    {
        if (trailEffect != null) return;

        GameObject bubbles = new GameObject("SharkTrailBubbles");
        bubbles.transform.SetParent(transform, false);
        // Offset for tail. Moved further back to align with tail.
        bubbles.transform.localPosition = new Vector3(-4.8f, -0.3f, 0f);

        trailEffect = bubbles.AddComponent<ParticleSystem>();
        var renderer = bubbles.GetComponent<ParticleSystemRenderer>();

        // Settings
        var main = trailEffect.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = 0.4f; // Reduced lifetime for a tighter trail
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.gravityModifier = -0.05f;
        
        var emission = trailEffect.emission;
        emission.rateOverTime = 8f; // Reduced emission for less bubbles

        var shape = trailEffect.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        // Reuse material logic
        if (bubbleMaterial != null)
        {
            renderer.material = bubbleMaterial;
        }
        else if (bubbleTexture != null)
        {
             if (cachedParticleShader == null)
             {
                 cachedParticleShader = Shader.Find("Particles/Standard Unlit");
                 if (cachedParticleShader == null) cachedParticleShader = Shader.Find("Sprites/Default");
             }
             
             if (cachedParticleShader != null)
             {
                 Material mat = new Material(cachedParticleShader);
                 mat.mainTexture = bubbleTexture;
                 renderer.material = mat;
             }
        }
        
        renderer.sortingOrder = 4; // Behind shark
    }

    private void CreateEatParticles()
    {
        // Optimizing allocation: Check again
        if (eatEffect != null) return;

        GameObject bubbles = new GameObject("SharkEatBubbles");
        bubbles.transform.SetParent(transform, false);
        bubbles.transform.localPosition = Vector3.zero;

        eatEffect = bubbles.AddComponent<ParticleSystem>();
        var renderer = bubbles.GetComponent<ParticleSystemRenderer>();

        // Main Settings
        var main = eatEffect.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.gravityModifier = -0.05f;
        main.maxParticles = 50;

        // Emission
        var emission = eatEffect.emission;
        emission.rateOverTime = 0;

        // Shape
        var shape = eatEffect.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;
        shape.angle = 25f;

        // Noise
        var noise = eatEffect.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.8f;
        noise.scrollSpeed = 1f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        // Velocity
        var vel = eatEffect.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
        vel.y = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.space = ParticleSystemSimulationSpace.World;

        // Color
        var col = eatEffect.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.7f, 0.0f), new GradientAlphaKey(0.5f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        col.color = grad;

        // Material
        if (bubbleMaterial != null)
        {
            renderer.material = bubbleMaterial;
        }
        else if (bubbleTexture != null)
        {
             if (cachedParticleShader == null)
             {
                 cachedParticleShader = Shader.Find("Particles/Standard Unlit");
                 if (cachedParticleShader == null) cachedParticleShader = Shader.Find("Sprites/Default");
             }
             
             if (cachedParticleShader != null)
             {
                 Material mat = new Material(cachedParticleShader);
                 mat.mainTexture = bubbleTexture;
                 renderer.material = mat;
             }
        }
        
        renderer.sortingOrder = 6;
    }

    private void PlayEatEffect()
    {
        if (eatEffect != null)
        {
            int count = Random.Range(2, 4);
            eatEffect.Emit(count);
        }
    }

    private void OnDestroy()
    {
        // Ensure shared canvas is hidden when shark is destroyed
        // This handles cases where shark is destroyed during warning phase (e.g. game over/restart)
        if (_sharedCanvasObj != null && _sharedCanvasObj.activeSelf)
        {
            _sharedCanvasObj.SetActive(false);
        }
    }
}
