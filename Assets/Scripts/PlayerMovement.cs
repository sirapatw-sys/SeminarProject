using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;

    public Vector2 Movement => movement;
    public bool IsMoving => movement.sqrMagnitude > 0.001f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Friction against walls and furniture made the player stick when
        // pushing diagonally into them; with none they slide along instead.
        PhysicsMaterial2D slippery = new PhysicsMaterial2D("PlayerNoFriction")
        {
            friction = 0f,
            bounciness = 0f,
        };
        rb.sharedMaterial = slippery;
        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            col.sharedMaterial = slippery;
        }
    }

    private void Update()
    {
        if (InputGate.IsBlocked)
        {
            movement = Vector2.zero;
            return;
        }

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        movement = movement.normalized;
    }

    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}
