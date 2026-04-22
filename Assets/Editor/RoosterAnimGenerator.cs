using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Generates AnimationClips + AnimatorController for the rooster spritesheet.
// Same AnimState convention as chicken: 0..3 = Idle R/U/L/D, 4..7 = Walk R/U/L/D.
public static class RoosterAnimGenerator
{
    private const string SPRITESHEET_PATH = "Assets/Sprites/Animals/Chickens_and_Roosters_32x32/Rooster_Black_and_Brown_32x32.png";
    private const string OUTPUT_DIR = "Assets/Sprites/Animations/Rooster";
    private const string CONTROLLER_PATH = "Assets/Sprites/Animations/Rooster/Rooster.controller";
    private const float FRAME_RATE = 12f;
    private const float STATE_SPEED = 0.5f;
    private const string BASE = "Rooster_Black_and_Brown_32x32";

    private static readonly (string stateName, string spritePrefix)[] STATES = new (string, string)[]
    {
        ("Rooster_IdleR", BASE + "_IdleR"),
        ("Rooster_IdleU", BASE + "_IdleU"),
        ("Rooster_IdleL", BASE + "_IdleL"),
        ("Rooster_IdleD", BASE + "_IdleD"),
        ("Rooster_WalkR", BASE + "_WalkR"),
        ("Rooster_WalkU", BASE + "_WalkU"),
        ("Rooster_WalkL", BASE + "_WalkL"),
        ("Rooster_WalkD", BASE + "_WalkD"),
    };

    [MenuItem("Tools/IdleFarm/Generate Rooster Anims")]
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
            Debug.LogError($"RoosterAnimGenerator: no sprites found at {SPRITESHEET_PATH}. Make sure the sheet is sliced.");
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
        Debug.Log("RoosterAnimGenerator: clips + controller generated at " + OUTPUT_DIR);
    }

    private static string BuildClip(string stateName, string spritePrefix, Dictionary<string, Sprite> spriteMap)
    {
        List<Sprite> frames = new List<Sprite>();
        for (int f = 1; f <= 6; f++)
        {
            string key = spritePrefix + f;
            if (spriteMap.TryGetValue(key, out Sprite sp)) frames.Add(sp);
            else Debug.LogWarning($"RoosterAnimGenerator: missing frame '{key}'");
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
}
