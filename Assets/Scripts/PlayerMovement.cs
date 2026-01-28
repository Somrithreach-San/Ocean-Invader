using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerMovement
/// - Legacy script. 
/// - Disabling functionality to prevent conflicts with PlayerController and Input System errors.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    // Script disabled to prefer PlayerController.cs and New Input System.
    // If you need this script, please migrate it to the new Input System.
    /*
    [Header("Movement")]
    public Rect playBounds = new Rect(-8f, -4f, 16f, 8f);
    public float acceleration = 20f;
    public float deceleration = 30f;
    public float maxSpeed = 5f;
    public float rotationSpeed = 720f;
    public float minMoveThreshold = 0.05f;

    [Header("Touch / Input")]
    public bool enableTouch = true;
    public float touchDeadzone = 0.15f;

    Rigidbody2D rb;
    Vector2 currentVelocity = Vector2.zero;
    Transform gfxTransform;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        Transform t = transform.Find("PlayerGraphics");
        if (t != null)
            gfxTransform = t;
        else
            gfxTransform = transform;
    }

    private void FixedUpdate()
    {
        // ... (Logic commented out)
    }
    */
}
