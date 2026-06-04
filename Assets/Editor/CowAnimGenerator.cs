using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// One-click cow setup: builds AnimationClips + AnimatorController from the cow
// spritesheet, creates a CowVisual prefab wired with SpriteRenderer + Animator +
// AnimalVisual, and assigns the prefab into Animal_Cow.asset's visualPrefab field.
// Same AnimState convention as chicken/rooster: 0..3 Idle R/U/L/D, 4..7 Walk R/U/L/D.
public static class CowAnimGenerator
{
    private const string SPRITESHEET_PATH = "Assets/Sprites/Animals/Cows_32x32/Cow_32x32.png";
    private const string OUTPUT_DIR      = "Assets/Sprites/Animations/Cow";
    private const string CONTROLLER_PATH = "Assets/Sprites/Animations/Cow/Cow.controller";
    private const string PREFAB_PATH     = "Assets/Prefabs/Animals/CowVisual.prefab";
    private const string ANIMAL_DATA_PATH = "Assets/Data/Animals/Animal_Cow.asset";
    private const string BASE = "Cow_32x32";
    private const float FRAME_RATE = 12f;
    private const float STATE_SPEED = 0.5f;
    private const float VISUAL_SCALE = 1.0f; // halved from 2.0 — was too big next to the chicken

    private static readonly (string stateName, string spritePrefix)[] STATES = new (string, string)[]
    {
        ("Cow_IdleR", BASE + "_IdleR"),
        ("Cow_IdleU", BASE + "_IdleU"),
        ("Cow_IdleL", BASE + "_IdleL"),
        ("Cow_IdleD", BASE + "_IdleD"),
        ("Cow_WalkR", BASE + "_WalkR"),
        ("Cow_WalkU", BASE + "_WalkU"),
        ("Cow_WalkL", BASE + "_WalkL"),
        ("Cow_WalkD", BASE + "_WalkD"),
    };

    [MenuItem("Tools/IdleFarm/Generate Cow Anims")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
        {
            Directory.CreateDirectory(OUTPUT_DIR);
            AssetDatabase.Refresh();
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SPRITESHEET_PATH);
        Dictionary<string, Sprite> spriteMap = new Dictionary<string, Sprite>();
        foreach (Object obj in assets)
        {
            if (obj is Sprite spr) spriteMap[spr.name] = spr;
        }

        if (spriteMap.Count == 0)
        {
            Debug.LogError($"CowAnimGenerator: no sprites found at {SPRITESHEET_PATH}. Make sure the sheet is sliced.");
            return;
        }

        string[] clipPaths = new string[STATES.Length];
        for (int i = 0; i < STATES.Length; i++)
        {
            clipPaths[i] = BuildClip(STATES[i].stateName, STATES[i].spritePrefix, spriteMap);
        }

        BuildController(clipPaths);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Pick a sane default-facing sprite for the prefab's SpriteRenderer so it isn't
        // a blank square in the inspector before Play.
        Sprite defaultSprite = null;
        spriteMap.TryGetValue(BASE + "_IdleD1", out defaultSprite);

        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        BuildPrefab(ctrl, defaultSprite);
        WireIntoAnimalData();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CowAnimGenerator: clips + controller + prefab generated; Animal_Cow.asset wired.");
    }

    private static string BuildClip(string stateName, string spritePrefix, Dictionary<string, Sprite> spriteMap)
    {
        List<Sprite> frames = new List<Sprite>();
        for (int f = 1; f <= 6; f++)
        {
            string key = spritePrefix + f;
            if (spriteMap.TryGetValue(key, out Sprite sp)) frames.Add(sp);
            else Debug.LogWarning($"CowAnimGenerator: missing frame '{key}'");
        }

        AnimationClip clip = new AnimationClip { frameRate = FRAME_RATE };
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Count];
        for (int f = 0; f < frames.Count; f++)
        {
            keys[f] = new ObjectReferenceKeyframe { time = f / FRAME_RATE, value = frames[f] };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        string clipPath = $"{OUTPUT_DIR}/{stateName}.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clipPath;
    }

    private static void BuildController(string[] clipPaths)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH) != null)
            AssetDatabase.DeleteAsset(CONTROLLER_PATH);

        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
        ctrl.AddParameter("AnimState", AnimatorControllerParameterType.Int);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState[] states = new AnimatorState[STATES.Length];

        for (int i = 0; i < STATES.Length; i++)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPaths[i]);
            AnimatorState state = sm.AddState(STATES[i].stateName);
            state.motion = clip;
            state.writeDefaultValues = false;
            state.speed = STATE_SPEED;
            states[i] = state;
        }

        sm.defaultState = states[3]; // IdleD

        for (int i = 0; i < states.Length; i++)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(states[i]);
            t.AddCondition(AnimatorConditionMode.Equals, i, "AnimState");
            t.duration = 0f;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
        }
    }

    private static void BuildPrefab(AnimatorController ctrl, Sprite defaultSprite)
    {
        if (ctrl == null)
        {
            Debug.LogError("CowAnimGenerator: cannot build prefab without controller.");
            return;
        }

        // Ensure Prefabs/Animals folder exists.
        string prefabDir = Path.GetDirectoryName(PREFAB_PATH);
        if (!AssetDatabase.IsValidFolder(prefabDir))
        {
            Directory.CreateDirectory(prefabDir);
            AssetDatabase.Refresh();
        }

        GameObject go = new GameObject("CowVisual");
        go.transform.localScale = new Vector3(VISUAL_SCALE, VISUAL_SCALE, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        if (defaultSprite != null) sr.sprite = defaultSprite;

        Animator anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        // AnimalVisual is found by type — it's the generic wander/anim driver.
        go.AddComponent<AnimalVisual>();

        // Save (overwrite if exists).
        PrefabUtility.SaveAsPrefabAsset(go, PREFAB_PATH);
        Object.DestroyImmediate(go);
    }

    private static void WireIntoAnimalData()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        Object animalData = AssetDatabase.LoadAssetAtPath<Object>(ANIMAL_DATA_PATH);
        if (prefab == null || animalData == null)
        {
            Debug.LogError("CowAnimGenerator: prefab or Animal_Cow.asset missing — cannot wire.");
            return;
        }

        SerializedObject so = new SerializedObject(animalData);
        SerializedProperty visualProp = so.FindProperty("visualPrefab");
        if (visualProp != null)
        {
            visualProp.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(animalData);
        }
        else
        {
            Debug.LogError("CowAnimGenerator: visualPrefab field not found on Animal_Cow.asset.");
        }
    }
}
