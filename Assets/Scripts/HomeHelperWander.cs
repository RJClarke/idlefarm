using UnityEngine;

/// <summary>
/// Simple wander behavior for cosmetic home screen helpers.
/// Picks a random point nearby, walks to it, pauses, then picks another.
/// </summary>
public class HomeHelperWander : MonoBehaviour
{
    private Vector3 wanderTarget;
    private Vector3 origin;
    private float moveSpeed = 0.6f;
    private float pauseTimer = 0f;
    private bool isPaused = true;
    private SpriteRenderer spriteRenderer;

    private const float WANDER_RADIUS = 25f;
    private const float MIN_PAUSE = 1f;
    private const float MAX_PAUSE = 3.5f;
    private const float ARRIVAL_THRESHOLD = 0.08f;

    private void Start()
    {
        origin = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
        PickNewTarget();
        pauseTimer = Random.Range(0.5f, 2f); // Stagger initial pause
    }

    private void Update()
    {
        if (isPaused)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
                PickNewTarget();
            }
            return;
        }

        // Move toward target
        float dist = Vector3.Distance(transform.position, wanderTarget);
        if (dist <= ARRIVAL_THRESHOLD)
        {
            // Arrived — pause before picking next point
            isPaused = true;
            pauseTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            return;
        }

        Vector3 dir = (wanderTarget - transform.position).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;

        // Flip sprite to face movement direction
        if (spriteRenderer != null && Mathf.Abs(dir.x) > 0.01f)
            spriteRenderer.flipX = dir.x < 0f;
    }

    private void PickNewTarget()
    {
        wanderTarget = origin + new Vector3(
            Random.Range(-WANDER_RADIUS, WANDER_RADIUS),
            Random.Range(-WANDER_RADIUS * 0.5f, WANDER_RADIUS * 0.5f),
            0f
        );
    }
}
