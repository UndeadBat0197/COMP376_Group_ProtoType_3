using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class WallClimbing : MonoBehaviour
{
    [Header("References")]
    private Rigidbody rb;
    private BoxCollider boxCollider;
    private ThirdPersonMovement movementScript;

    [Header("Climbing Settings")]
    public float climbSpeed = 3f;

    [Tooltip("The minimum steepness angle to trigger climbing. 90 is a vertical wall, 45 is a steep ramp.")]
    [Range(45f, 90f)] public float minClimbAngle = 70f;

    private bool isClimbing = false;
    private bool touchingValidSurface = false;
    private Vector3 surfaceNormal;

    // Sticky Buffer to prevent single-frame physics separation from dropping the state
    private float looseContactTimer = 0f;
    private const float CONTACT_STICKY_WINDOW = 0.15f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        movementScript = GetComponent<ThirdPersonMovement>();
    }

    void Update()
    {
        // 1. Hardware inputs
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift);

        // 2. Extra safety sweep: If physics logs a temporary disconnect, check geometrically if the wall is still right there
        if (!touchingValidSurface && isClimbing)
        {
            CheckForWallProximity();
        }

        // 3. Manage the sticky window timer
        if (touchingValidSurface)
        {
            looseContactTimer = CONTACT_STICKY_WINDOW; // Refresh buffer
        }
        else if (looseContactTimer > 0f)
        {
            looseContactTimer -= Time.deltaTime;
        }

        // 4. State Transitions (Uses the timer buffer instead of instant raw boolean to prevent jitter loops)
        if ((touchingValidSurface || looseContactTimer > 0f) && isShiftHeld)
        {
            if (!isClimbing)
            {
                isClimbing = true;
                if (movementScript != null) movementScript.enabled = false;
                rb.useGravity = false;
            }
        }
        else
        {
            if (isClimbing)
            {
                isClimbing = false;
                if (movementScript != null) movementScript.enabled = true;
                rb.useGravity = true;
            }
        }

        // 5. Climb Execution Loop
        if (isClimbing)
        {
            // Rotate the model 90 degrees UP so it visually scales the wall face vertically
            Vector3 lookDirection = Vector3.up;
            Vector3 upwardOrientation = surfaceNormal;

            if (lookDirection != Vector3.zero && upwardOrientation != Vector3.zero)
            {
                rb.MoveRotation(Quaternion.LookRotation(lookDirection, upwardOrientation));
            }

            // Remap WASD inputs relative to wall planes
            Vector3 wallRight = Vector3.Cross(Vector3.up, surfaceNormal).normalized;
            Vector3 climbVelocity = (wallRight * -horizontalInput + Vector3.up * verticalInput).normalized * climbSpeed;

            if (Mathf.Abs(horizontalInput) < 0.1f && Mathf.Abs(verticalInput) < 0.1f)
            {
                climbVelocity = Vector3.zero;
            }

            // FIX: Replaced the aggressive physics push with a subtle velocity pull that matches the surface exactly
            // and lets the fixed update cycles manage boundaries cleanly without deep penetration
            climbVelocity -= surfaceNormal * 0.2f;

            rb.linearVelocity = climbVelocity;
        }
    }

    void FixedUpdate()
    {
        // Reset every physics tick so OnCollisionStay can refresh it accurately
        touchingValidSurface = false;
    }

    // Backup proximity sweep using an Overlap Box right in front of our shell
    void CheckForWallProximity()
    {
        Vector3 boxCenter = transform.position + boxCollider.center - (surfaceNormal * 0.05f);
        Vector3 halfExtents = boxCollider.size * 0.55f; // Look slightly outside our skin

        Collider[] hitColliders = Physics.OverlapBox(boxCenter, halfExtents, transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        foreach (var col in hitColliders)
        {
            if (col.gameObject == gameObject) continue;

            // ClosestPoint lets us mathematically figure out the true vector direction of the wall surface right next to us
            Vector3 closestPoint = col.ClosestPoint(transform.position + boxCollider.center);
            Vector3 calculatedNormal = ((transform.position + boxCollider.center) - closestPoint).normalized;

            float angle = Vector3.Angle(calculatedNormal, Vector3.up);
            if (angle >= minClimbAngle)
            {
                touchingValidSurface = true;
                surfaceNormal = calculatedNormal;
                break;
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            float contactAngle = Vector3.Angle(contact.normal, Vector3.up);

            if (contactAngle >= minClimbAngle)
            {
                touchingValidSurface = true;
                surfaceNormal = contact.normal;
                break;
            }
        }
    }
}
