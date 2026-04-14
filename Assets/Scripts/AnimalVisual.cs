using UnityEngine;

public class AnimalVisual : MonoBehaviour
{
    private AnimalData data;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private bool animatorHasAnimState;

    // Wander state
    private Vector3 wanderTarget;
    private float pauseTimer = 0f;
    private bool isPaused = true;

    // Animator direction (0=R, 1=U, 2=L, 3=D). Kept so idle uses the last facing.
    private int facingDir = 3;
    private int lastAnimState = -1;

    private const string ANIM_STATE_PARAM = "AnimState";
    private const int ANIM_WALK_OFFSET = 4;

    // Wander config
    private const float WANDER_RADIUS = 3f;
    private const float MIN_PAUSE = 1.5f;
    private const float MAX_PAUSE = 4f;

    // Egg visual
    private GameObject eggInstance;

    public bool PauseWander { get; set; }

    public void Initialize(AnimalData animalData)
    {
        data = animalData;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        animatorHasAnimState = HasAnimStateParam(animator);

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 10;
        }

        wanderTarget = transform.position;
        pauseTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
    }

    private static bool HasAnimStateParam(Animator a)
    {
        if (a == null || a.runtimeAnimatorController == null) return false;
        foreach (var p in a.parameters)
        {
            if (p.name == ANIM_STATE_PARAM && p.type == AnimatorControllerParameterType.Int) return true;
        }
        return false;
    }

    private void ApplyAnimState(bool moving)
    {
        if (!animatorHasAnimState) return;
        int state = (moving ? ANIM_WALK_OFFSET : 0) + facingDir;
        if (state == lastAnimState) return;
        animator.SetInteger(ANIM_STATE_PARAM, state);
        lastAnimState = state;
    }

    private void Update()
    {
        if (data == null || PauseWander)
        {
            ApplyAnimState(false);
            return;
        }

        if (isPaused)
        {
            ApplyAnimState(false);

            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
                PickNewTarget();
            }
        }
        else
        {
            Vector3 direction = wanderTarget - transform.position;
            float distance = direction.magnitude;

            if (distance < 0.1f)
            {
                // Arrived at target
                isPaused = true;
                pauseTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
                ApplyAnimState(false);
            }
            else
            {
                // Move toward target
                float speed = data.roamSpeed > 0 ? data.roamSpeed : 0.6f;
                transform.position = Vector3.MoveTowards(transform.position, wanderTarget, speed * Time.deltaTime);

                UpdateFacing(direction);

                if (animatorHasAnimState)
                {
                    ApplyAnimState(true);
                }
                else if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
                {
                    // Fallback for animals without a directional animator: horizontal flip only.
                    spriteRenderer.flipX = direction.x < 0;
                }
            }
        }
    }

    private void UpdateFacing(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        // Project onto the dominant axis. 0=R, 1=U, 2=L, 3=D.
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            facingDir = direction.x >= 0 ? 0 : 2;
        else
            facingDir = direction.y >= 0 ? 1 : 3;
    }

    private void PickNewTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * WANDER_RADIUS;
        Vector3 candidate = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0);

        // Clamp to visible screen bounds
        wanderTarget = ClampToScreenBounds(candidate);
    }

    private Vector3 ClampToScreenBounds(Vector3 position)
    {
        Camera cam = Camera.main;
        if (cam == null) return position;

        // Use viewport with 8% padding inset from edges
        float pad = 0.08f;
        Vector3 minWorld = cam.ViewportToWorldPoint(new Vector3(pad, pad, cam.nearClipPlane));
        Vector3 maxWorld = cam.ViewportToWorldPoint(new Vector3(1f - pad, 1f - pad, cam.nearClipPlane));

        position.x = Mathf.Clamp(position.x, minWorld.x, maxWorld.x);
        position.y = Mathf.Clamp(position.y, minWorld.y, maxWorld.y);
        position.z = 0;

        return position;
    }

    // ── Egg Visual ──────────────────────────────

    public void DropEgg()
    {
        if (eggInstance != null) return; // Already has an egg

        eggInstance = new GameObject("EggDrop");
        eggInstance.transform.position = transform.position + Vector3.down * 0.2f;

        SpriteRenderer eggRenderer = eggInstance.AddComponent<SpriteRenderer>();
        eggRenderer.sortingOrder = 9;

        // Create a simple egg texture (placeholder — replace with sprite asset later)
        Texture2D tex = new Texture2D(12, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color eggWhite = new Color(1f, 0.98f, 0.9f);
        Color eggShadow = new Color(0.9f, 0.88f, 0.8f);

        Color[] pixels = new Color[12 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        // Simple egg shape
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                float cx = (x - 5.5f) / 5.5f;
                float cy = (y - 8f) / 8f;
                // Egg-ish ellipse (narrower at top)
                float topFactor = 1f - cy * 0.3f;
                float dist = (cx * cx) / (topFactor * topFactor) + cy * cy;
                if (dist < 0.85f)
                {
                    pixels[y * 12 + x] = y < 6 ? eggShadow : eggWhite;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        eggRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 12, 16), new Vector2(0.5f, 0f), 32f);

        // Subtle drop animation
        eggInstance.transform.localScale = Vector3.zero;
        LeanTween.scale(eggInstance, Vector3.one * 0.8f, 0.3f).setEaseOutBack();
    }

    public void RemoveEgg()
    {
        if (eggInstance == null) return;

        GameObject egg = eggInstance;
        eggInstance = null;

        // Pop-out animation then destroy
        LeanTween.scale(egg, Vector3.zero, 0.2f).setEaseInBack().setOnComplete(() =>
        {
            Destroy(egg);
        });
    }
}
