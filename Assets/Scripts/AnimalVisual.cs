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
    private const int ANIM_PECK_OFFSET = 8;

    // Wander config
    private const float WANDER_RADIUS = 5f;
    private const float MIN_PAUSE = 1f;
    private const float MAX_PAUSE = 2.5f;

    // Egg visual
    [SerializeField] private Sprite eggSprite;
    private GameObject eggInstance;

    // Gem visual
    [SerializeField] private Sprite gemSprite;
    private GameObject gemInstance;

    private bool _pauseWander;
    public bool PauseWander
    {
        get => _pauseWander;
        set
        {
            // When releasing control back to AnimalVisual, force a fresh anim sync.
            // FarmDog may have left the Animator in a state lastAnimState doesn't know about.
            if (_pauseWander && !value) lastAnimState = -1;
            _pauseWander = value;
        }
    }

    public void Initialize(AnimalData animalData)
    {
        data = animalData;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        animatorHasAnimState = HasAnimStateParam(animator);
        if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 10;
        }

        wanderTarget = transform.position;
        pauseTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);

        // Depth-sort the animal by its Y (wanders, so it updates every frame).
        YSort.Ensure(gameObject);
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

    /// <summary>
    /// Lets another controller (e.g. Cow eating) drive the facing + walk/idle animation from a
    /// movement direction, so the sprite faces where it's actually going. Use together with
    /// <see cref="PauseWander"/> = true so the built-in wander doesn't fight it.
    /// </summary>
    public void DriveMovementAnim(Vector3 worldDirection, bool moving)
    {
        if (moving) UpdateFacing(worldDirection);
        ApplyAnimState(moving);
    }

    private void Update()
    {
        if (data == null)
        {
            ApplyAnimState(false);
            return;
        }
        if (PauseWander) return;

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

        // Use U/D only within ±15° of vertical; everything else gets L/R.
        // tan(75°) ≈ 3.73 — if |y| exceeds this multiple of |x|, we're nearly straight up/down.
        const float verticalBias = 3.73f;
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x) * verticalBias)
            facingDir = direction.y >= 0 ? 1 : 3;
        else
            facingDir = direction.x >= 0 ? 0 : 2;
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

    // ── Peck ─────────────────────────────────────

    private Coroutine peckCoroutine;

    /// <summary>
    /// Plays the Peck animation for the given duration, then returns to idle.
    /// </summary>
    public void TriggerPeck(float duration = 1.5f)
    {
        if (!animatorHasAnimState) return;
        if (peckCoroutine != null) StopCoroutine(peckCoroutine);
        peckCoroutine = StartCoroutine(PeckRoutine(duration));
    }

    private System.Collections.IEnumerator PeckRoutine(float duration)
    {
        PauseWander = true;
        animator.SetInteger(ANIM_STATE_PARAM, ANIM_PECK_OFFSET + facingDir);
        yield return new WaitForSeconds(duration);
        animator.SetInteger(ANIM_STATE_PARAM, facingDir); // back to idle
        PauseWander = false;
        peckCoroutine = null;
    }

    // ── Egg Visual ──────────────────────────────

    public void DropEgg()
    {
        if (eggInstance != null) return; // Already has an egg

        eggInstance = new GameObject("EggDrop");
        // NOT parented to the chicken — drops onto the ground where she laid it and stays put as she wanders.
        eggInstance.transform.position = transform.position + Vector3.down * 0.2f;

        SpriteRenderer eggRenderer = eggInstance.AddComponent<SpriteRenderer>();
        eggRenderer.sortingOrder = 11; // above the animal body (sortingOrder 10); YSort will manage by depth

        if (eggSprite != null)
        {
            eggRenderer.sprite = eggSprite;
        }
        else
        {
            // Fallback procedural egg
            Texture2D tex = new Texture2D(12, 16, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color eggWhite = new Color(1f, 0.98f, 0.9f);
            Color eggShadow = new Color(0.9f, 0.88f, 0.8f);
            Color[] pixels = new Color[12 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    float ecx = (x - 5.5f) / 5.5f;
                    float ecy = (y - 8f) / 8f;
                    float topFactor = 1f - ecy * 0.3f;
                    float dist = (ecx * ecx) / (topFactor * topFactor) + ecy * ecy;
                    if (dist < 0.85f)
                        pixels[y * 12 + x] = y < 6 ? eggShadow : eggWhite;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            eggRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 12, 16), new Vector2(0.5f, 0f), 32f);
        }

        // Subtle drop animation
        eggInstance.transform.localScale = Vector3.zero;
        LeanTween.scale(eggInstance, Vector3.one * 0.8f, 0.3f).setEaseOutBack();

        // Egg is unparented (stays on the ground), so it depth-sorts on its own.
        YSort.Ensure(eggInstance, isStatic: true);
    }

    public void RemoveEgg()
    {
        if (eggInstance == null) return;

        GameObject egg = eggInstance;
        eggInstance = null;

        LeanTween.scale(egg, Vector3.zero, 0.2f).setEaseInBack().setOnComplete(() =>
        {
            Destroy(egg);
        });
    }

    // ── Gem Visual ──────────────────────────────

    public void DropGem()
    {
        if (gemInstance != null) return;

        gemInstance = new GameObject("GemDrop");
        // NOT parented to the rooster — drops onto the ground where he was and stays put as he wanders.
        gemInstance.transform.position = transform.position + Vector3.down * 0.3f;

        SpriteRenderer gemRenderer = gemInstance.AddComponent<SpriteRenderer>();
        gemRenderer.sortingOrder = 11; // ABOVE the rooster body (sortingOrder 10) — was 9, hidden behind

        if (gemSprite != null)
        {
            gemRenderer.sprite = gemSprite;
        }
        else
        {
            // Procedural purple diamond placeholder
            Texture2D tex = new Texture2D(12, 16, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color gemColor = new Color(0.659f, 0.333f, 0.969f);
            Color gemHighlight = new Color(0.8f, 0.6f, 1f);
            Color[] pixels = new Color[12 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    float cx = Mathf.Abs((x - 5.5f) / 5.5f);
                    float cy = Mathf.Abs((y - 7.5f) / 7.5f);
                    if (cx + cy < 0.9f)
                        pixels[y * 12 + x] = (x < 6 && y > 8) ? gemHighlight : gemColor;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            gemRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 12, 16), new Vector2(0.5f, 0f), 32f);
        }

        gemInstance.transform.localScale = Vector3.zero;
        LeanTween.scale(gemInstance, Vector3.one * 1.3f, 0.3f).setEaseOutBack(); // procedural gem sprite is tiny; 1.3x reads as a dropped gem

        // Gem is unparented (stays on the ground), so it depth-sorts on its own.
        YSort.Ensure(gemInstance, isStatic: true);
    }

    public void RemoveGem()
    {
        if (gemInstance == null) return;

        GameObject gem = gemInstance;
        gemInstance = null;

        LeanTween.scale(gem, Vector3.zero, 0.2f).setEaseInBack().setOnComplete(() =>
        {
            Destroy(gem);
        });
    }

    private void OnDestroy()
    {
        // Drops are unparented (so they stay on the ground as the animal wanders), so they no
        // longer die with the visual automatically — clean them up here on unequip/swap/destroy.
        if (eggInstance != null) Destroy(eggInstance);
        if (gemInstance != null) Destroy(gemInstance);
    }

    /// <summary>
    /// Scene-wide sweep for orphan egg/gem GameObjects. Called by AnimalManager on claim
    /// to guarantee no stale visuals linger if a previous DropEgg/DropGem orphaned its
    /// spawn (e.g. animal swap between drop and claim, or a hot scene reload).
    /// </summary>
    public static void CleanupAllOrphanDrops()
    {
        DestroyAllByName("EggDrop");
        DestroyAllByName("GemDrop");
    }

    private static void DestroyAllByName(string name)
    {
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) Destroy(all[i]);
    }
}
