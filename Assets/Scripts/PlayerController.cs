using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveForce = 25f;
    public float maxSpeed = 8f;
    public float groundDrag = 2f;
    public float airDrag = 0.2f;

    [Header("Hover")]
    public float hoverHeight = 2f;
    public float hoverForce = 80f;
    public float hoverDamping = 8f;
    public LayerMask groundMask;

    private Rigidbody rb;
    private InputAction moveAction;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
    }

    void OnEnable()
    {
        moveAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
    }

    void FixedUpdate()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 moveInput = new Vector3(input.x, 0f, input.y).normalized;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            rb.AddForce(moveInput * moveForce, ForceMode.Acceleration);
        }

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVel.magnitude > maxSpeed)
        {
            Vector3 limited = flatVel.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(limited.x, rb.linearVelocity.y, limited.z);
        }

        bool grounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, hoverHeight + 1f, groundMask);

        if (grounded)
        {
            float heightError = hoverHeight - hit.distance;
            float upwardSpeed = Vector3.Dot(rb.linearVelocity, Vector3.up);
            float lift = (heightError * hoverForce) - (upwardSpeed * hoverDamping);

            rb.AddForce(Vector3.up * lift, ForceMode.Acceleration);
            rb.linearDamping = groundDrag;
        }
        else
        {
            rb.linearDamping = airDrag;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(transform.position.x, transform.position.y - 5f, transform.position.z), new Vector3(transform.position.x, transform.position.y + 5f, transform.position.z));
    }
}