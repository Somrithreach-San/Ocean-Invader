using UnityEngine;

public class FishSchool : MonoBehaviour
{
    public Vector2 CurrentDestination { get; private set; }
    private bool movingRight;
    private float decisionTimer;
    
    // Bounds
    private float minX = -45f;
    private float maxX = 45f;
    private float minY = -13f;
    private float maxY = 13f;

    public void Initialize(bool startRight)
    {
        movingRight = startRight;
        PickNewDestination();
        // Auto-destroy school controller after 60 seconds (fish should be gone by then)
        Destroy(gameObject, 60f);
    }

    private void Update()
    {
        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0)
        {
            PickNewDestination();
        }
    }

    private void PickNewDestination()
    {
        // Small chance to turn around (Group Turn)
        if (Random.value < 0.1f) 
        {
            movingRight = !movingRight;
        }

        // Target X is towards the edge we are moving to
        // We set it far out so they keep swimming
        float targetX = movingRight ? maxX + 10f : minX - 10f;

        // Random Y within bounds
        float targetY = Random.Range(minY, maxY);

        CurrentDestination = new Vector2(targetX, targetY);

        // Re-evaluate in 2-4 seconds
        // This allows them to change Y or turn back occasionally
        decisionTimer = Random.Range(2f, 4f);
    }
}
