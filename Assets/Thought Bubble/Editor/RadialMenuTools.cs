using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor tooling for radial (wheel-shaped) poke menus. Menu: Thought Bubble/Radial Menu/.
///
/// 1. Create Poke Wedge Button Prefab: makes "Poke Wedge Button Prefab", a Prefab
///    Variant of Poke Button Prefab. It is a variant rather than a copy so the colours,
///    poke thresholds, PokeInteractableToggle and event wrapper keep following the base
///    prefab. The variant swaps only the two parts that assume a rectangle:
///      - the hit area:  ClippedPlaneSurface + BoundsClipper  ->  WedgeSurface
///      - the drawing:   the built-in Quad mesh               ->  a mesh PokeWedge generates
///    It also squares the corners and makes the label shrink to fit on one line.
///
/// 2. Build Emotion Wheel Under Selection: builds the Junto Institute Emotion & Feeling
///    Wheel (6 cores, 34 feelings, 68 words) as three RadialRings of wedge buttons under
///    the selected object, which must be inside a Canvas because the labels are
///    TextMeshProUGUI. Undo removes the whole wheel in one step.
/// </summary>
public static class RadialMenuTools
{
    private const string BasePrefabPath = "Assets/Thought Bubble/Prefabs/Poke Button Prefab.prefab";
    private const string WedgePrefabPath = "Assets/Thought Bubble/Prefabs/Poke Wedge Button Prefab.prefab";

    private const string SurfacePath = "Model/Surface";
    private const string ButtonVisualPath = "Visuals/ButtonVisual";

    // The wheel starts at the left edge of Anger, measured off the printed wheel
    // (about 29 degrees anticlockwise of 12 o'clock). All three rings share it, so their
    // boundaries line up.
    private const float WheelStartAngle = 331f;

    // Ring proportions measured off the printed wheel (centre hole 0.105, cores 0.35,
    // feelings 0.665, words 1.0 of the full radius), scaled to a 6-unit radius.
    // Under the menu Canvases that is 30 cm, so the wheel is 60 cm across.
    private const float CoreInner = 0.6f, CoreOuter = 2.1f;
    private const float FeelingOuter = 4.0f;
    private const float WordOuter = 6.0f;

    // The Junto Institute "Emotion & Feeling Wheel", clockwise from the left edge of Anger.
    // Every feeling has exactly two outer-ring words. That is what makes some cores wider
    // than others on the printed wheel: Joy has 8 feelings, Sadness 6, the rest 5.
    // "Satisfied" and "Dismayed" each appear twice on the printed wheel, so they do here too.
    private static readonly (string Core, (string Feeling, string[] Words)[] Feelings)[] JuntoWheel =
    {
        ("Anger", new[]
        {
            ("Rage", new[] { "Hate", "Hostile" }),
            ("Exasperated", new[] { "Agitated", "Frustrated" }),
            ("Irritable", new[] { "Annoyed", "Aggravated" }),
            ("Envy", new[] { "Resentful", "Jealous" }),
            ("Disgust", new[] { "Contempt", "Revolted" }),
        }),
        ("Sadness", new[]
        {
            ("Suffering", new[] { "Agony", "Hurt" }),
            ("Sadness", new[] { "Depressed", "Sorrow" }),
            ("Disappointed", new[] { "Dismayed", "Displeased" }),
            ("Shameful", new[] { "Regretful", "Guilty" }),
            ("Neglected", new[] { "Isolated", "Lonely" }),
            ("Despair", new[] { "Grief", "Powerless" }),
        }),
        ("Surprise", new[]
        {
            ("Stunned", new[] { "Shocked", "Dismayed" }),
            ("Confused", new[] { "Disillusioned", "Perplexed" }),
            ("Amazed", new[] { "Astonished", "Awe-struck" }),
            ("Overcome", new[] { "Speechless", "Astounded" }),
            ("Moved", new[] { "Stimulated", "Touched" }),
        }),
        ("Joy", new[]
        {
            ("Content", new[] { "Pleased", "Satisfied" }),
            ("Happy", new[] { "Amused", "Delighted" }),
            ("Cheerful", new[] { "Jovial", "Blissful" }),
            ("Proud", new[] { "Triumphant", "Illustrious" }),
            ("Optimistic", new[] { "Eager", "Hopeful" }),
            ("Enthusiastic", new[] { "Excited", "Zeal" }),
            ("Elation", new[] { "Euphoric", "Jubilation" }),
            ("Enthralled", new[] { "Enchanted", "Rapture" }),
        }),
        ("Love", new[]
        {
            ("Affectionate", new[] { "Romantic", "Fondness" }),
            ("Longing", new[] { "Sentimental", "Attracted" }),
            ("Desire", new[] { "Passion", "Infatuation" }),
            ("Tenderness", new[] { "Caring", "Compassionate" }),
            ("Peaceful", new[] { "Relieved", "Satisfied" }),
        }),
        ("Fear", new[]
        {
            ("Scared", new[] { "Frightened", "Helpless" }),
            ("Terror", new[] { "Panic", "Hysterical" }),
            ("Insecure", new[] { "Inferior", "Inadequate" }),
            ("Nervous", new[] { "Worried", "Anxious" }),
            ("Horror", new[] { "Mortified", "Dread" }),
        }),
    };

    [MenuItem("Thought Bubble/Radial Menu/1 - Create Poke Wedge Button Prefab")]
    private static void CreateWedgePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WedgePrefabPath) != null)
        {
            Debug.LogWarning($"[RadialMenuTools] '{WedgePrefabPath}' already exists, so it was not touched. " +
                             "Delete it first if you want it rebuilt from scratch.");
            return;
        }

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null)
        {
            Debug.LogError($"[RadialMenuTools] Base prefab not found at '{BasePrefabPath}'.");
            return;
        }

        // Check every part exists before creating anything, so a mismatch can't leave a
        // half-converted variant behind.
        if (!HasExpectedParts(basePrefab))
            return;

        // Step 1: an unmodified variant. Saving an instance of a prefab as a new prefab
        // makes a variant of it. The instance goes in a throwaway preview scene so the open
        // scene is not touched or marked unsaved.
        var previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, previewScene);
            PrefabUtility.SaveAsPrefabAsset(temp, WedgePrefabPath, out bool saved);
            if (!saved)
            {
                Debug.LogError($"[RadialMenuTools] Could not save '{WedgePrefabPath}'.");
                return;
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        // Step 2: edit the variant's contents in isolation. This is the scripted equivalent
        // of opening it in Prefab Mode, so removals are recorded as variant overrides.
        GameObject root = PrefabUtility.LoadPrefabContents(WedgePrefabPath);
        try
        {
            ConvertToWedge(root);
            PrefabUtility.SaveAsPrefabAsset(root, WedgePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(WedgePrefabPath);
        Debug.Log($"[RadialMenuTools] Created '{WedgePrefabPath}' (a variant of Poke Button Prefab).");
    }

    private static bool HasExpectedParts(GameObject prefab)
    {
        Transform surface = prefab.transform.Find(SurfacePath);
        Transform visual = prefab.transform.Find(ButtonVisualPath);

        bool ok = prefab.GetComponent<PokeInteractable>() != null
                  && surface != null && surface.GetComponent<PlaneSurface>() != null
                  && visual != null && visual.GetComponent<MeshFilter>() != null
                  && visual.GetComponent<RoundedBoxProperties>() != null
                  && visual.GetComponentInChildren<TextMeshProUGUI>(true) != null;

        if (!ok)
            Debug.LogError("[RadialMenuTools] Poke Button Prefab no longer has the expected layout " +
                           $"(PokeInteractable on the root, PlaneSurface on '{SurfacePath}', MeshFilter + " +
                           $"RoundedBoxProperties + TextMeshProUGUI label on '{ButtonVisualPath}'). Nothing was created.");
        return ok;
    }

    private static void ConvertToWedge(GameObject root)
    {
        Transform surface = root.transform.Find(SurfacePath);
        Transform visual = root.transform.Find(ButtonVisualPath);
        var meshFilter = visual.GetComponent<MeshFilter>();
        var label = visual.GetComponentInChildren<TextMeshProUGUI>(true);

        // Shape + drawing.
        var wedge = root.AddComponent<PokeWedge>();
        var wedgeSo = new SerializedObject(wedge);
        wedgeSo.FindProperty("_meshFilter").objectReferenceValue = meshFilter;
        wedgeSo.FindProperty("_label").objectReferenceValue = label.rectTransform;
        wedgeSo.ApplyModifiedPropertiesWithoutUndo();

        // Hit area: point the PokeInteractable at the wedge instead of the rectangle.
        var wedgeSurface = surface.gameObject.AddComponent<WedgeSurface>();
        var surfaceSo = new SerializedObject(wedgeSurface);
        surfaceSo.FindProperty("_planeSurface").objectReferenceValue = surface.GetComponent<PlaneSurface>();
        surfaceSo.FindProperty("_wedge").objectReferenceValue = wedge;
        surfaceSo.ApplyModifiedPropertiesWithoutUndo();

        var pokeSo = new SerializedObject(root.GetComponent<PokeInteractable>());
        pokeSo.FindProperty("_surfacePatch").objectReferenceValue = wedgeSurface;
        pokeSo.ApplyModifiedPropertiesWithoutUndo();

        // The rectangle-only parts are now unreferenced. Remove them so nobody has to
        // wonder which surface is live.
        RemoveOrDisable(surface.GetComponent<ClippedPlaneSurface>());
        RemoveOrDisable(surface.GetComponent<BoundsClipper>());

        // PokeWedge generates the mesh when the button is enabled. Leave the slot empty in
        // the asset so instances don't each record a mesh override against the Quad.
        meshFilter.sharedMesh = null;

        // Square corners, like the printed wheel. The shader rounds corners in UV space,
        // which on a wedge would stretch them into long ovals.
        var roundedSo = new SerializedObject(visual.GetComponent<RoundedBoxProperties>());
        roundedSo.FindProperty("_radiusTopLeft").floatValue = 0f;
        roundedSo.FindProperty("_radiusTopRight").floatValue = 0f;
        roundedSo.FindProperty("_radiusBottomLeft").floatValue = 0f;
        roundedSo.FindProperty("_radiusBottomRight").floatValue = 0f;
        roundedSo.ApplyModifiedPropertiesWithoutUndo();

        // One line, shrinking to fit the wedge. Wrapping would stack a long word into lines
        // that can't fit across a thin wedge. Never bigger than the regular buttons' text.
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.fontSizeMax = label.fontSize;
        label.fontSizeMin = 4f;
        label.enableAutoSizing = true;
        label.text = "Wedge";
    }

    private static void RemoveOrDisable(MonoBehaviour component)
    {
        if (component == null)
            return;

        try
        {
            Object.DestroyImmediate(component);
        }
        catch (System.Exception e)
        {
            // Nothing references it any more, so a disabled leftover is harmless.
            component.enabled = false;
            Debug.LogWarning($"[RadialMenuTools] Could not remove {component.GetType().Name} from the variant " +
                             $"({e.Message}). Disabled it instead.");
        }
    }

    [MenuItem("Thought Bubble/Radial Menu/2 - Build Emotion Wheel Under Selection")]
    private static void BuildEmotionWheel()
    {
        Transform parent = Selection.activeTransform;
        if (parent == null)
        {
            Debug.LogError("[RadialMenuTools] Select the object to build the wheel under first. " +
                           "It must be inside a Canvas, because the wedge labels are TextMeshProUGUI.");
            return;
        }

        if (parent.GetComponentInParent<Canvas>(true) == null)
        {
            Debug.LogError($"[RadialMenuTools] '{parent.name}' is not inside a Canvas. The wedge labels are " +
                           "TextMeshProUGUI and would not render. Select an object inside a Canvas.", parent);
            return;
        }

        GameObject wedgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WedgePrefabPath);
        if (wedgePrefab == null)
        {
            Debug.LogError("[RadialMenuTools] Poke Wedge Button Prefab not found. Run " +
                           "'1 - Create Poke Wedge Button Prefab' first.");
            return;
        }

        int wordCount = 0;
        foreach (var core in JuntoWheel)
            foreach (var feeling in core.Feelings)
                wordCount += feeling.Words.Length;
        float wordArc = 360f / wordCount;

        var wheel = new GameObject("Emotion Wheel");
        wheel.layer = parent.gameObject.layer;
        wheel.transform.SetParent(parent, false);

        RadialRing coreRing = CreateRing(wheel.transform, "Ring 1 - Core", CoreInner, CoreOuter);
        RadialRing feelingRing = CreateRing(wheel.transform, "Ring 2 - Feelings", CoreOuter, FeelingOuter);
        RadialRing wordRing = CreateRing(wheel.transform, "Ring 3 - Words", FeelingOuter, WordOuter);

        PokeWedge firstWedge = null;
        int wedgeCount = 0;

        foreach (var core in JuntoWheel)
        {
            int coreWords = 0;
            foreach (var feeling in core.Feelings)
                coreWords += feeling.Words.Length;

            PokeWedge coreWedge = CreateWedge(wedgePrefab, coreRing, core.Core, wordArc * coreWords);
            firstWedge ??= coreWedge;
            wedgeCount++;

            foreach (var feeling in core.Feelings)
            {
                CreateWedge(wedgePrefab, feelingRing, feeling.Feeling, wordArc * feeling.Words.Length);
                wedgeCount++;

                foreach (string word in feeling.Words)
                {
                    CreateWedge(wedgePrefab, wordRing, word, wordArc);
                    wedgeCount++;
                }
            }
        }

        // Registered last, so a single Undo removes the wheel and everything built under it.
        Undo.RegisterCreatedObjectUndo(wheel, "Build Emotion Wheel");
        Selection.activeGameObject = wheel;

        float diameter = 2f * WordOuter * firstWedge.transform.lossyScale.x;
        Debug.Log($"[RadialMenuTools] Built '{wheel.name}' under '{parent.name}': {wedgeCount} wedge buttons " +
                  $"in 3 rings, {diameter:0.00} m across. Scale the 'Emotion Wheel' object to resize it.", wheel);
    }

    private static RadialRing CreateRing(Transform wheel, string name, float innerRadius, float outerRadius)
    {
        var go = new GameObject(name);
        go.layer = wheel.gameObject.layer;
        go.transform.SetParent(wheel, false);

        var ring = go.AddComponent<RadialRing>();
        var so = new SerializedObject(ring);
        so.FindProperty("_innerRadius").floatValue = innerRadius;
        so.FindProperty("_outerRadius").floatValue = outerRadius;
        so.FindProperty("_startAngle").floatValue = WheelStartAngle;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ring;
    }

    private static PokeWedge CreateWedge(GameObject prefab, RadialRing ring, string label, float arcDegrees)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ring.transform);
        go.name = $"[Wedge] {label}";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var wedge = go.GetComponent<PokeWedge>();
        var so = new SerializedObject(wedge);
        so.FindProperty("_arcDegrees").floatValue = arcDegrees;
        so.ApplyModifiedPropertiesWithoutUndo();

        TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
        text.text = label;
        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        return wedge;
    }
}
