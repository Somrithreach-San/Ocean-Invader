using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rhinotap.Toolkit;

public class GridController : MonoBehaviour
{
    #region Inspector

    [Header("Enemy Library Object")]
    [SerializeField]
    private EnemyLibrary enemyLibrary;

    [Header("Object to track in the grid")]
    [SerializeField]
    private GameObject player;


    [Header("Enable/Disable grid runtime")]
    [SerializeField]
    private bool isActive = true;
    public bool Active => isActive;

    [Header("Arena Spawning")]
    [SerializeField]
    private int maxFishCount = 20; // Increased limit to allow more fish density
    [SerializeField]
    private float spawnInterval = 1.5f; // Spawning happens faster (was 5f -> 2f) to replenish eatable fish
    private float spawnTimer = 0f;

    [Header("Hazard Settings")]
    [SerializeField]
    private GameObject hazardPrefab;
    [SerializeField]
    private Sprite hazardSprite; // Backup if prefab is missing
    [SerializeField]
    private Sprite hazardSpriteVariant; // Second Variant
    [SerializeField]
    private float hazardChance = 0.20f; // 20% chance
    [SerializeField]
    private float hazardScale = 0.8f; // Scale modifier for sprite-spawned hazard

    [Header("Hazard Effects")]
    [SerializeField]
    private AudioClip hazardSound;
    [SerializeField]
    private GameObject hazardBubblePrefab;
    
    // Track active hazard to limit to 1
    private GameObject activeHazard;

    [Header("Shark Hazard Settings")]
    [SerializeField]
    private GameObject sharkPrefab;
    [SerializeField]
    private Sprite sharkSprite;
    [SerializeField]
    private GameObject warningIconPrefab;
    [SerializeField]
    private Sprite warningIconSprite;
    [SerializeField]
    private float sharkChance = 0.05f; // 5% chance per spawn tick (Rare but deadly)
    [SerializeField]
    private AudioClip sharkWarningSound;
    [SerializeField]
    private AudioClip sharkAttackSound;
    [SerializeField]
    private AudioClip sharkSwimSound; // New Swim Sound
    
    // Track active shark
    private GameObject activeShark;

    // Templates for Optimization
    private GameObject sharkTemplate;
    private GameObject hazardTemplate;

    #endregion

    //Public enemy library access point
    public EnemyLibrary EnemyLibrary => enemyLibrary;
    // Public access to tracked player GameObject
    public GameObject Player => player;
    
    #region MonoBehaviour

    private void Awake()
    {
        EventManager.StartListening<GameObject>("PlayerSpawn", (spawnedObject) =>
        {
            if (spawnedObject != null)
                player = spawnedObject;
        });
    }

    private void Start()
    {
        // Migration: Update spawnInterval to new faster default if it's still at the old default
        if (Mathf.Abs(spawnInterval - 2.0f) < 0.01f)
        {
             spawnInterval = 1.5f;
        }

        // Ensure hazard chance is reasonable
    }

    private void Update()
    {
        if (!isActive) return;
        if (player == null) return;

        // Arena Mode: Continuous Spawning
        HandleArenaSpawning();
    }

    private void HandleArenaSpawning()
    {
        if (enemyLibrary == null) return;

        // Periodic Cleanup (Every 60 frames / ~1s) to remove far-off fish
        // This ensures high-level fish that leave the screen are eventually destroyed
        // even if the population limit hasn't been reached.
        if (Time.frameCount % 60 == 0)
        {
             CullObsoleteFish(GameManager.PlayerLevel);
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;

            // 1. Hazard Spawn Check (Priority over Fish)
            // Allowed to spawn even if Fish count is maxed out
            // User Request: "More frequent both of it" -> Boosted chances
            // User Request: "increase the chanes of the hazrd hook more"
            float effectiveHazardChance = Mathf.Max(hazardChance, 0.5f); // Increased from 0.4f
            
            // Further boost for low levels since they don't have sharks
            if (GameManager.PlayerLevel <= 2) 
            {
                effectiveHazardChance = 0.7f; 
            }

            if (activeHazard == null && Random.value < effectiveHazardChance)
            {
                SpawnHazard();
                return; 
            }

            // 1.5 Shark Spawn Check (Priority over Fish, Independent of Fisherman)
            // Can happen alongside other things, but limit to 1 active shark
            
            // User Request: "when player is still low level around level 1 -2 dont spawn the shark hazzard yet"
            if (GameManager.PlayerLevel > 2)
            {
                // User Request: "Shark should appear more when user reaches level 4 5 6"
                float effectiveSharkChance = Mathf.Max(sharkChance, 0.15f); // Boost base chance
                
                if (GameManager.PlayerLevel >= 4)
                {
                    effectiveSharkChance = 0.45f; // Much more frequent at high levels
                }

                if (activeShark == null && Random.value < effectiveSharkChance)
                {
                    SpawnShark();
                    // If we spawn a shark, maybe skip fish spawning this frame to reduce chaos?
                    return;
                }
            }

            // 2. Fish Count Limit Check
            // OPTIMIZATION: Use static list from Fish class
            
            // Get player level early for culling check
            int playerLevel = GameManager.PlayerLevel;

            if (Fish.AllFish.Count >= maxFishCount)
            {
                // CULLING LOGIC:
                // If the ocean is full, check if we have "Obsolete" fish (Level < PlayerLevel)
                // that are taking up space. If so, remove them to make room for new, relevant fish.
                if (CullObsoleteFish(playerLevel))
                {
                    // We culled a fish. It will be removed at end of frame.
                    // Return now, but keep spawnTimer high so we retry spawning immediately next frame.
                    return;
                }
                
                // If nothing to cull, we are truly full.
                return;
            }
            
            // PREDATOR CAP LOGIC:
            // Count how many active fish are predators (Level > PlayerLevel)
            int predatorCount = 0;
            
            // Optimization: Iterate over static list
            for (int i = 0; i < Fish.AllFish.Count; i++)
            {
                if (Fish.AllFish[i].Level > playerLevel) predatorCount++;
            }

            // Hard Limit: Max 3 Predators allowed at any time.
            bool forceEatable = (predatorCount >= 3);

            SpawnArenaFish(playerLevel, forceEatable);
        }
    }

    private void SpawnArenaFish(int playerLevel, bool forceEatable)
    {
        // Spawn 1 fish per interval (Reduced count to prevent crowding)
        int count = 1;
        
        // Calculate Camera View Boundaries
        Camera cam = Camera.main;
        if (cam == null) return;

        float camHeight = 2f * cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;
        float halfWidth = camWidth / 2f;
        
        // Spawn just outside the camera view (buffer of 2 units)
        float buffer = 2f;
        float rightEdge = cam.transform.position.x + halfWidth + buffer;
        float leftEdge = cam.transform.position.x - halfWidth - buffer;

        for (int i = 0; i < count; i++)
        {
            // Randomly choose Left or Right side relative to Camera
            float spawnX = (Random.value > 0.5f) ? rightEdge : leftEdge;
            
            // Random Y within world vertical bounds (-14 to 14)
            // But also clamp to be near camera Y to ensure visibility? 
            // Let's keep it within world bounds but maybe biased towards camera Y?
            // For now, world bounds -14 to 14 is safe.
            float spawnY = Random.Range(-14f, 14f); 

            // Add slight randomness to X
            spawnX += Random.Range(-1f, 1f);

            Vector2 spawnPos = new Vector2(spawnX, spawnY);
            
            // Difficulty Logic:
            // User Request: 80% Current Level (Eatable), 20% Next Level (Predator)
            // Logic slides with Player Level.
            
            int spawnLevel = 1;
            
            // 80% Chance for Eatable (Current Level)
            float eatableChance = 0.80f; 
            
            // If we hit the predator cap, we FORCE eatable fish (100% chance)
            if (forceEatable)
            {
                eatableChance = 1.0f;
            }
            
            if (Random.value < eatableChance)
            {
                // === EATABLE POOL (80% Total) ===
                
                // User Request: "Dont stop spawning lower level fishes entirely"
                // Balanced Distribution:
                // - 50% chance for Current Level (Primary Food)
                // - 50% chance for Any Lower Level (Ambient/Easy Food)
                // This results in roughly: 40% Current, 40% Lower, 20% Predator.
                
                if (playerLevel > 1 && Random.value < 0.5f)
                {
                     // Spawn any lower level (1 to PlayerLevel-1)
                     spawnLevel = Random.Range(1, playerLevel);
                }
                else
                {
                     // Spawn fish of the current player level
                     spawnLevel = playerLevel;
                }
            }
            else
            {
                // === PREDATOR POOL (20% Total) ===
                // Spawn fish of the next level
                spawnLevel = playerLevel + 1;
            }

            if (spawnLevel > 6) spawnLevel = 6;
            
            // Debug Log for verification
            // Debug.Log($"Spawning Level {spawnLevel} Fish (Player Level: {playerLevel}) | Eatable Chance: {eatableChance}");

            // Determine Prefab
            // User Request: Check for prefab name correctly and check their assigned level
            string targetName = "level " + spawnLevel + " fish";
            Fish prefabToSpawn = enemyLibrary.GetPrefabByName(spawnLevel, targetName);
            
            // Fallback if specific name not found (just in case)
            if (prefabToSpawn == null)
            {
                prefabToSpawn = enemyLibrary.GetRandomPrefab(spawnLevel);
            }
            
            if (prefabToSpawn != null)
            {
                // Verification: Ensure the prefab's level matches our intended spawn level
                if (prefabToSpawn.Level != spawnLevel)
                {
                    Debug.LogWarning($"Spawn Mismatch! Intended: {spawnLevel}, Prefab: {prefabToSpawn.name} has Level {prefabToSpawn.Level}");
                    // We continue spawning, as the user might have custom setups, but we warned them.
                }

                // SCHOOLING LOGIC: Level 1, L01-00 (level 1 fish)
                // User Request: "spawn less schooling fish spawn more alone fish"
                // Reduced chance from 90% to 10% (so 90% chance to be alone)
                if (spawnLevel == 1 && prefabToSpawn.name.Contains("level 1 fish") && Random.value < 0.1f)
                {
                    // Create School
                    GameObject schoolObj = new GameObject("FishSchool");
                    FishSchool school = schoolObj.AddComponent<FishSchool>();
                    bool movingRight = (spawnX < 0); 
                    school.Initialize(movingRight);
                    
                    // User Request: "group of babies fish form together"
                    // Reduced count to prevent crowding/jitter (3 to 5 fish)
                    int schoolSize = Random.Range(3, 6);
                    
                    for (int s = 0; s < schoolSize; s++)
                    {
                        // "Natural Formation": 
                        // Use a slightly larger, irregular spread (0.5f to 1.5f radius)
                        // This prevents them from being too perfectly circular or too tight
                        Vector2 schoolOffset = Random.insideUnitCircle * Random.Range(0.5f, 2.0f);
                        
                        // Stretch horizontally to look like they are swimming in a line/group
                        schoolOffset.x *= 1.5f; 

                        Vector2 finalPos = spawnPos + schoolOffset;
                        finalPos.y = Mathf.Clamp(finalPos.y, -14f, 14f);
                        
                        // FIX: Don't override the prefab's inherent level. 
                        // The user has carefully set up prefabs with specific levels/sprites.
                        // Passing '0' or '-1' as overrideLevel to respect the prefab's data.
                        Fish fish = enemyLibrary.SpawnSpecific(prefabToSpawn, finalPos, 0f, 0f, -1);
                        if (fish != null)
                        {
                            fish.school = school;
                            fish.formationOffset = schoolOffset;
                            OrientFish(fish, finalPos, new Vector2(cam.transform.position.x, finalPos.y));
                        }
                    }
                }
                else
                {
                    // Spawn Single (10% chance for L01-00, or 100% for others)
                    // FIX: Don't override level. Respect Prefab settings.
                    Fish fish = enemyLibrary.SpawnSpecific(prefabToSpawn, spawnPos, 0f, 0f, -1);
                    if (fish != null)
                    {
                        OrientFish(fish, spawnPos, new Vector2(cam.transform.position.x, spawnY));
                    }
                }
            }
            
            // Note: We don't save these to GridBlocks because they are temporary "passers-by"
        }
    }

    //==============================| Helpers |========================//

    private bool CullObsoleteFish(int playerLevel)
    {
        if (Camera.main == null) return false;

        // Calculate Camera Bounds (with margin)
        float camHeight = 2f * Camera.main.orthographicSize;
        float camWidth = camHeight * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;
        
        // Define a bounding box for the visible area + margin
        // Fish outside this box are candidates for culling
        Bounds viewBounds = new Bounds(new Vector3(camPos.x, camPos.y, 0), new Vector3(camWidth + 5f, camHeight + 5f, 100f));

        // Define a larger bounding box for "Distant" culling (Cleanup)
        // Any fish that wanders this far (high level or not) should be removed to free up memory/slots
        Bounds distantBounds = new Bounds(new Vector3(camPos.x, camPos.y, 0), new Vector3(camWidth + 30f, camHeight + 30f, 100f));

        // Find a candidate
        foreach (var fish in Fish.AllFish)
        {
            if (fish == null) continue;

            // Condition 0: Is Way Off-Screen? (Universal Cleanup)
            // This handles High-Level fish that leave the screen and keep going.
            if (!distantBounds.Contains(fish.transform.position))
            {
                Destroy(fish.gameObject);
                return true; 
            }

            // Condition 1: Is Obsolete? (Lower level than player)
            // We want to keep current level fish and higher level predators.
            if (fish.Level < playerLevel)
            {
                // Condition 2: Is Off-Screen?
                if (!viewBounds.Contains(fish.transform.position))
                {
                    // Found one! Cull it.
                    Destroy(fish.gameObject);
                    return true; // Culled one, job done.
                }
            }
        }

        return false;
    }

    private void SpawnHazard()
    {
        // Double check
        if (activeHazard != null) return;

        GameObject hazardObj = null;

        if (hazardPrefab != null)
        {
            hazardObj = Instantiate(hazardPrefab);
        }
        else if (hazardSprite != null)
        {
            // Fallback: Create hazard from sprite
            // OPTIMIZATION: Use Template
            if (hazardTemplate == null)
            {
                hazardTemplate = new GameObject("Hazard_Template");
                hazardTemplate.transform.SetParent(transform);
                hazardTemplate.SetActive(false);
                
                SpriteRenderer sr = hazardTemplate.AddComponent<SpriteRenderer>();
                sr.sprite = hazardSprite; // Default
                
                BoxCollider2D col = hazardTemplate.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                if (sr.sprite != null) col.size = sr.sprite.bounds.size;
                
                hazardTemplate.AddComponent<Hazard>();
                hazardTemplate.tag = "Enemy";
                hazardTemplate.transform.localScale = Vector3.one * hazardScale;
            }

            hazardObj = Instantiate(hazardTemplate);
            hazardObj.name = "Hazard_Hook";
            hazardObj.SetActive(true);

            // Randomly choose between default and variant if available
            SpriteRenderer objSr = hazardObj.GetComponent<SpriteRenderer>();
            if (objSr != null)
            {
                if (hazardSpriteVariant != null && Random.value > 0.5f)
                {
                    objSr.sprite = hazardSpriteVariant;
                }
                else
                {
                    objSr.sprite = hazardSprite;
                }
            }
        }
        else
        {
            // EMERGENCY FALLBACK: Red Quad
            Debug.LogWarning("[GridController] Hazard Prefab AND Sprite are missing! Spawning Red Quad fallback.");
            hazardObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hazardObj.name = "Hazard_Fallback";
            Destroy(hazardObj.GetComponent<Collider>()); // Remove 3D collider
            
            BoxCollider2D col = hazardObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1f, 1f);

            hazardObj.AddComponent<Hazard>();
            hazardObj.tag = "Enemy";
            
            Renderer r = hazardObj.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.red;
            
            hazardObj.transform.localScale = new Vector3(1.5f, 1.5f, 1f); // Visible size
        }

        if (hazardObj != null)
        {
            activeHazard = hazardObj; // Track it

            // Inject Effects (if missing)
            Hazard h = hazardObj.GetComponent<Hazard>();
            if (h != null)
            {
                Material pMat = null;
                Texture2D pTex = null;
                if (player != null)
                {
                    PlayerController pc = player.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pMat = pc.BubbleMaterial;
                        pTex = pc.BubbleTexture;
                    }
                }
                h.Initialize(hazardSound, hazardBubblePrefab, pMat, pTex);
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            float camHeight = 2f * cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;
            float halfWidth = camWidth / 2f;
            
            // Spawn at top, random X within Fixed World Bounds
            // User Request: "spawn based on the game sense itself", not camera.
            // But also: "dont spawn outside of the background or the player bounderies"
            // We clamp spawnX to be within the visible width (plus margin) to ensure it's "in play".
            
            float bgLimit = halfWidth - 1f; // Keep it slightly inside camera/bg edges
            float spawnX = Random.Range(-bgLimit, bgLimit);
            
            // Offset by camera position to keep it relative to view if camera moves
            spawnX += cam.transform.position.x;

            // Fixed Surface Level Spawning (User Request: "not based on player camera")
            // Game vertical bounds are approx -14 to 14. 
            // We spawn high enough (22f) to ensure the top of the line/rod is off-screen initially,
            // preventing the "line cut off" visual issue when the player is near the surface.
            float spawnY = 22f; 
            
            hazardObj.transform.position = new Vector3(spawnX, spawnY, 0);
        }
    }

    private void SpawnShark()
    {
        if (activeShark != null) return;

        // FIXED: Spawn based on World Coordinates (Arena) instead of Camera View.
        // This prevents the shark from feeling "attached" to the player's movement.
        
        // 1. Determine Direction (Left->Right or Right->Left)
        int direction = (Random.value > 0.5f) ? 1 : -1;

        // 2. Determine Spawn Position
        // X: Spawn closer to reduce warning time (approx 1 sec less travel time)
        // Original was +/- 55f. With speed ~9.5f, 1 sec is ~9.5 units.
        // New Spawn X: +/- 45.5f (55 - 9.5)
        float spawnX = (direction > 0) ? -45.5f : 45.5f; 
        
        // Y: Random height within the fixed game world (-14 to 14).
        // This makes the shark feel like it's patrolling the ocean, not chasing the camera.
        float spawnY = Random.Range(-14f, 14f);

        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0);

        GameObject sharkObj = null;

        if (sharkPrefab != null)
        {
            sharkObj = Instantiate(sharkPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Fallback: Create from sprite
            // OPTIMIZATION: Use Template
            if (sharkTemplate == null)
            {
                sharkTemplate = new GameObject("Shark_Template");
                sharkTemplate.transform.SetParent(transform);
                sharkTemplate.SetActive(false);
                
                SpriteRenderer sr = sharkTemplate.AddComponent<SpriteRenderer>();
                if (sharkSprite != null) sr.sprite = sharkSprite;
                
                // Collider
                BoxCollider2D col = sharkTemplate.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                if (sr.sprite != null) col.size = sr.sprite.bounds.size;
                else col.size = new Vector2(2f, 1f);
                
                sharkTemplate.AddComponent<SharkHazard>();
                
                // No tag set in original code? Adding Enemy tag just in case, though SharkHazard handles collision logic.
                // Original code didn't set tag for Shark.
            }

            sharkObj = Instantiate(sharkTemplate, spawnPos, Quaternion.identity);
            sharkObj.name = "Shark_Hazard";
            sharkObj.SetActive(true);
            
            // Apply Sprite (already default, but just in case we add variants later)
             SpriteRenderer sharkSr = sharkObj.GetComponent<SpriteRenderer>();
             if (sharkSr != null && sharkSprite != null) sharkSr.sprite = sharkSprite;

             // Note: Original code rotated capsule if no sprite. 
             // If we have sprite, we don't rotate.
             // If we don't have sprite (Emergency Fallback), we use primitive logic which is outside this block in original code?
             // Wait, original code had: if (sharkSprite != null) ... else { // Capsule }
             // My template logic handles sprite case.
             // If sharkSprite is null, template will have null sprite.
             
             if (sharkSprite == null)
             {
                 // Revert to emergency fallback for this instance if no sprite
                 Destroy(sharkObj); // Kill template instance
                 sharkObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                 Destroy(sharkObj.GetComponent<Collider>());
                 sharkObj.transform.rotation = Quaternion.Euler(0, 0, 90);
                 Renderer r = sharkObj.GetComponent<Renderer>();
                 if (r != null) r.material.color = Color.gray;
                 
                 BoxCollider2D col = sharkObj.AddComponent<BoxCollider2D>();
                 col.isTrigger = true;
                 col.size = new Vector2(2f, 1f);
                 
                 sharkObj.AddComponent<SharkHazard>();
                 sharkObj.transform.position = spawnPos;
             }
        }

        if (sharkObj != null)
        {
            activeShark = sharkObj;
            
            SharkHazard shark = sharkObj.GetComponent<SharkHazard>();
            if (shark == null) shark = sharkObj.AddComponent<SharkHazard>();

            // Inject Effects (Bubble Material from Player)
            Material pMat = null;
            Texture2D pTex = null;
            if (player != null)
            {
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pMat = pc.BubbleMaterial;
                    pTex = pc.BubbleTexture;
                }
            }

            shark.Initialize(direction, warningIconPrefab, warningIconSprite, sharkWarningSound, sharkAttackSound, sharkSwimSound, pMat, pTex);
        }
    }

    private void OrientFish(Fish fish, Vector2 spawnPos, Vector2 targetPos)
    {
        if (fish == null) return;

        // Check if fish uses Rotation-based movement (FishAI or FishMovement)
        bool usesRotation = fish.GetComponent<FishAI>() != null || fish.GetComponent<FishMovement>() != null;

        if (usesRotation)
        {
            Vector2 dir = (targetPos - spawnPos).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            fish.transform.rotation = Quaternion.Euler(0, 0, angle);

            // Fix Upside Down if facing left
            if (Mathf.Abs(angle) > 90f)
            {
                Vector3 s = fish.transform.localScale;
                s.y = -Mathf.Abs(s.y);
                fish.transform.localScale = s;
            }
            else
            {
                // Ensure Y is positive if facing right
                Vector3 s = fish.transform.localScale;
                s.y = Mathf.Abs(s.y);
                fish.transform.localScale = s;
            }
        }
        else
        {
            // Use standard Flip (Scale X)
            // Ensure rotation is zero
            fish.transform.rotation = Quaternion.identity;
            
            // Flip towards target
            fish.FlipTowardsDestination(targetPos, false);
        }
    }

    #endregion
}