using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Generates AnimationClips + AnimatorController for the white chicken spritesheet.
// Mirrors the Crow/Deer convention: single int parameter "AnimState" driving transitions.
// AnimState values: 0..3 = Idle R/U/L/D, 4..7 = Walk R/U/L/D.
public static class ChickenAnimGenerator
{
    private const string SPRITESHEET_PATH = "Assets/Sprites/Animals/Chickens_and_Roosters_32x32/Chicken_White_32x32.png";
    private const string OUTPUT_DIR = "Assets/Sprites/Animations/Chicken";
    private const string CONTROLLER_PATH = "Assets/Sprites/Animations/Chicken/Chicken.controller";
    private const float FRAME_RATE = 12f;
    // State playback speed multiplier (1 = authored speed, 0.5 = half speed, etc.)
    private const float STATE_SPEED = 0.5f;

    // Ordered to match AnimState integer values (index == AnimState value).
    private static readonly (string stateName, string spritePrefix)[] STATES = new (string, string)[]
    {
        ("Chicken_IdleR", "Chicken_White_32x32_IdleR"),
        ("Chicken_IdleU", "Chicken_White_32x32_IdleU"),
        ("Chicken_IdleL", "Chicken_White_32x32_IdleL"),
        ("Chicken_IdleD", "Chicken_White_32x32_IdleD"),
        ("Chicken_WalkR", "Chicken_White_32x32_WalkR"),
        ("Chicken_WalkU", "Chicken_White_32x32_WalkU"),
        ("Chicken_WalkL", "Chicken_White_32x32_WalkL"),
        ("Chicken_WalkD", "Chicken_White_32x32_WalkD"),
    };

    [MenuItem("Tools/IdleFarm/Generate Chicken Anims")]
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
            Debug.LogError($"ChickenAnimGenerator: no sprites found at {SPRITESHEET_PATH}");
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
        Debug.Log("ChickenAnimGenerator: clips + controller generated at " + OUTPUT_DIR);
    }

    private static string BuildClip(string stateName, string spritePrefix, Dictionary<string, Sprite> spriteMap)
    {
        List<Sprite> frames = new List<Sprite>();
        for (int f = 1; f <= 6; f++)
        {
            string key = spritePrefix + f;
            if (!spriteMap.ContainsKey(key))
            {
                // Known typo on one frame: "Chicken_White_32x32WalkR4" (missing underscore)
                string typoKey = "Chicken_White_32x32" + spritePrefix.Substring("Chicken_White_32x32_".Length) + f;
                if (spriteMap.ContainsKey(typoKey)) key = typoKey;
            }
            if (spriteMap.TryGetValue(key, out Sprite sp)) frames.Add(sp);
            else Debug.LogWarning($"ChickenAnimGenerator: missing frame '{spritePrefix}{f}'");
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
            keys[f] = new ObjectReferenceKeyframe
            {
                time = f / FRAME_RATE,
                value = frames[f]
            };
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

        // Default to IdleD (facing down is natural for top-down sprites).
        sm.defaultState = states[3];

        // One AnyState transition per state, fired when AnimState equals that state's index.
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(states[i]);
            t.AddCondition(AnimatorConditionMode.Equals, i, "AnimState");
            t.duration = 0f;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
        }
    }
}
