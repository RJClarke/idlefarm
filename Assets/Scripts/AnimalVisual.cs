using UnityEngine;
using UnityEngine.InputSystem;

public class AnimalVisual : MonoBehaviour
{
    // Tap-to-react: a tiny hop + a randomized sound when the player taps the animal on the farm.
    private bool isHopping;
    private const float TapPadding = 0.3f;

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
    private const float WANDER_INSET = 0.08f; // keep targets a little in from the framing edge

    // Penning: while a run is active OR the camera is parked at the Lake, the animal stays within the
    // Farm framing instead of following the camera around. This is the "no wandering onto the water"
    // barrier, and it keeps every equipped animal home doing its job during a run. If the animal has
    // drifted out (idle wander took it toward the Lake before a run started, say), it briskly walks
    // home first. Non-penned idle wander is unchanged — animals still follow the camera to the
    // Greenhouse/Woods like before.
    [SerializeField] private float returnHomeSpeed = 4f;
    private const float ReturnHomeEpsilonSqr = 0.01f; // ignore <0.1u overshoot so an edge target can't trap it
    private CameraPanController pan;
    private CameraPanController.Location effectiveLocation = CameraPanController.Location.Farm;

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

        // Track the camera's location so we can pen the animal at the Lake / during runs.
        pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (pan != null)
        {
            effectiveLocation = pan.CurrentLocation;
            pan.OnPanStarted += OnPanStarted;     // fires with the TARGET at pan start
            pan.OnPanCompleted += OnPanCompleted;
        }
    }

    private void OnPanStarted(CameraPanController.Location target) => effectiveLocation = target;
    private void OnPanCompleted(CameraPanController.Location loc) => effectiveLocation = loc;

    // Penned = confined to the Farm framing (no camera-following). True during a run, or whenever the
    // camera is heading to / sitting at the Lake.
    private bool IsPenned()
    {
        bool runActive = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        return runActive || effectiveLocation == CameraPanController.Location.Lake;
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
        HandleClickTap();

        // While a hop is playing we briefly own the transform (see Hop) — skip wander so the two
        // don't fight over position; the hop is short enough that pausing the roam is invisible.
        if (isHopping) return;

        if (data == null)
        {
            ApplyAnimState(false);
            return;
        }
        if (PauseWander) return;

        // While penned, keep inside the Farm framing — walk home first if we've drifted out.
        // NOTE: compare against the CLAMPED home point, not Rect.Contains(). Rect.Contains treats the
        // max edges as exclusive while our clamp is inclusive, so a target sitting exactly on xMax/yMax
        // reads as "outside" with a zero correction vector — that used to trap the animal walking in
        // place at the edge (facing its last direction). Only return home when the correction is real.
        if (IsPenned() && pan != null)
        {
            Rect farm = pan.GetViewRect(CameraPanController.Location.Farm, WANDER_INSET);
            Vector3 home = ClampToRect(transform.position, farm);
            if ((home - transform.position).sqrMagnitude > ReturnHomeEpsilonSqr)
            {
                Vector3 dir = home - transform.position;
                UpdateFacing(dir);
                ApplyAnimState(true);
                transform.position = Vector3.MoveTowards(transform.position, home, returnHomeSpeed * Time.deltaTime);
                isPaused = false;      // don't resume a stale pause mid-return
                wanderTarget = home;   // and don't yank back out on the next wander step
                return;
            }
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

        // Penned → confine to the Farm framing; otherwise free-follow the current camera view
        // (so idle animals still trail you to the Greenhouse / Woods).
        wanderTarget = (IsPenned() && pan != null)
            ? ClampToRect(candidate, pan.GetViewRect(CameraPanController.Location.Farm, WANDER_INSET))
            : ClampToScreenBounds(candidate);
    }

    private static Vector3 ClampToRect(Vector3 position, Rect rect)
    {
        position.x = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
        position.y = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
        position.z = 0;
        return position;
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

    // ── Tap to react (hop + sound) ───────────────

    private void HandleClickTap()
    {
        if (spriteRenderer == null) return;
        if (!TryReadTap(out Vector2 screenPos)) return;
        if (UITapBlocker.PointerOverUI(screenPos)) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

        Bounds b = spriteRenderer.bounds;
        b.Expand(new Vector3(TapPadding * 2f, TapPadding * 2f, 0f)); // forgiving hit area for a small target
        if (world.x < b.min.x || world.x > b.max.x || world.y < b.min.y || world.y > b.max.y) return;

        Hop();
        if (SfxManager.Instance != null && data != null) SfxManager.Instance.PlayRandom(data.clickSounds);
    }

    // A quick vertical bounce. Wander is gated (isHopping) so it doesn't fight the tween; on complete we
    // snap Y back to the pre-hop value so the roam resumes cleanly from where it was.
    private void Hop()
    {
        if (isHopping) return;
        isHopping = true;
        float baseY = transform.position.y;
        LeanTween.moveY(gameObject, baseY + 0.18f, 0.11f)
            .setEase(LeanTweenType.easeOutQuad)
            .setLoopPingPong(1)
            .setOnComplete(() =>
            {
                if (this == null) return;
                Vector3 p = transform.position;
                p.y = baseY;
                transform.position = p;
                isHopping = false;
            });
    }

    private static bool TryReadTap(out Vector2 screenPos)
    {
        screenPos = default;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
        }
        return false;
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

        // Tap the egg directly on the farm to collect it (in addition to the HUD collect button).
        eggInstance.AddComponent<DropClickCollector>();

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

        // Tap the gem directly on the farm to collect it (in addition to the HUD collect button).
        gemInstance.AddComponent<DropClickCollector>();

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
        if (pan != null)
        {
            pan.OnPanStarted -= OnPanStarted;
            pan.OnPanCompleted -= OnPanCompleted;
        }

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
