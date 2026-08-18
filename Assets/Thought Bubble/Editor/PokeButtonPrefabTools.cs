using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tooling for retrofitting the hand-built poke buttons in MainScene onto
/// Poke Button Prefab. Two steps, both operating on the current Hierarchy selection.
///
/// 1. Normalize Labels — the scene buttons carry their label as a direct child of the
///    root under a per-button name ("Play TMP_Text", "CANCEL NEW BUBBLE TMP_Text", and
///    one misnamed "STOP RECORDING TMP_Text" on the PLAY LAST RECORDING button), while
///    the prefab nests it under Visuals/ButtonVisual as "BUTTON LABEL TMP_Text" so it
///    rides the press animation. ConvertToPrefabInstance matches children by name, so
///    the label has to be renamed and reparented first — otherwise it fails to match
///    and every button ends up with two labels: the prefab's, plus its own kept as an
///    added-GameObject override.
///
/// 2. Convert To Prefab Instances — PrefabUtility.ConvertToPrefabInstance turns an
///    existing scene GameObject into an instance of a prefab *without destroying it*,
///    so every inbound reference survives: ScenePropReference, BubbleMenu, MainMenu,
///    BubbleDeleter, and the UnityEvent wiring the buttons use on each other.
///
/// Do not run these on the two wrist buttons or the Bubble Menu opener — they have a
/// different component set (WristControlBehavior / InteractableUnityEventWrapper) and
/// no label child.
/// </summary>
public static class PokeButtonPrefabTools
{
    private const string PrefabPath = "Assets/Thought Bubble/Prefabs/Poke Button Prefab.prefab";
    private const string LabelName = "BUTTON LABEL TMP_Text";
    private const string LabelParentPath = "Visuals/ButtonVisual";

    // Matches the label placement authored in the prefab, so all twelve land identically
    // instead of inheriting whatever offset each hand-built button happened to have.
    private static readonly Vector3 LabelLocalPosition = new Vector3(0f, 0f, -0.009f);
    private static readonly Vector3 LabelLocalScale = new Vector3(0.01f, 0.01f, 0.01f);

    [MenuItem("Thought Bubble/Poke Buttons/1 - Normalize Labels On Selection")]
    private static void NormalizeLabels()
    {
        int changed = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                Debug.LogWarning($"[PokeButtonTools] '{go.name}' has no TMP_Text child — skipped.", go);
                continue;
            }

            Transform parent = go.transform.Find(LabelParentPath);
            if (parent == null)
            {
                Debug.LogWarning($"[PokeButtonTools] '{go.name}' has no '{LabelParentPath}' — skipped.", go);
                continue;
            }

            Undo.RecordObject(label.gameObject, "Rename Button Label");
            label.gameObject.name = LabelName;

            if (label.transform.parent != parent)
                Undo.SetTransformParent(label.transform, parent, "Reparent Button Label");

            Undo.RecordObject(label.transform, "Place Button Label");
            label.transform.localPosition = LabelLocalPosition;
            label.transform.localRotation = Quaternion.identity;
            label.transform.localScale = LabelLocalScale;

            EditorUtility.SetDirty(go);
            changed++;
        }

        Debug.Log($"[PokeButtonTools] Normalized {changed} label(s). " +
                  "Check the text is still centred on each plate before converting.");
    }

    [MenuItem("Thought Bubble/Poke Buttons/2 - Convert Selection To Prefab Instances")]
    private static void ConvertSelection()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null)
        {
            Debug.LogError($"[PokeButtonTools] No prefab found at {PrefabPath}");
            return;
        }

        var settings = new ConvertToPrefabInstanceSettings
        {
            // The scene buttons order their children differently from each other
            // (Model/Label/Visuals vs Visuals/Label/Model), so match on names rather
            // than on hierarchy position.
            objectMatchMode = ObjectMatchMode.ByName,

            // Keep anything the prefab doesn't have, rather than deleting it — e.g. the
            // BubbleInstantiator component that lives on the CONFIRM NEW BUBBLE button.
            componentsNotMatchedBecomesOverride = true,
            gameObjectsNotMatchedBecomesOverride = true,

            // Keep per-button values as overrides: transform, label text, _startDisabled,
            // and the _whenSelect UnityEvent wiring.
            recordPropertyOverridesOfMatches = true,

            // Keep the descriptive scene names instead of renaming everything to the asset.
            changeRootNameToAssetName = false,

            logInfo = true,
        };

        int converted = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            if (PrefabUtility.IsPartOfAnyPrefab(go))
            {
                Debug.LogWarning($"[PokeButtonTools] '{go.name}' is already part of a prefab — skipped.", go);
                continue;
            }

            PrefabUtility.ConvertToPrefabInstance(go, asset, settings, InteractionMode.UserAction);
            converted++;
        }

        Debug.Log($"[PokeButtonTools] Converted {converted} button(s). " +
                  "Read the per-object match report above for anything left unmatched.");
    }
}
