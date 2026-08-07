using System.Collections;
using System.IO;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Deletes a bubble completely: the live GameObject, its entry in
/// <see cref="SavedBubbleData.bubbles"/>, its entry in bubbles.json, and its WAV
/// recording on disk.
///
/// Put this on the Bubble Menu object (next to <see cref="BubbleMenu"/>) and drag
/// the Delete Bubble button's PointableUnityEventWrapper into
/// <see cref="deleteBubbleButton"/>. It acts on
/// ScenePropReference.currentBubbleBeingGrabbed — the bubble whose menu is open.
/// </summary>
public class BubbleDeleter : MonoBehaviour
{
    [Tooltip("The Delete Bubble button on the bubble menu.")]
    public PointableUnityEventWrapper deleteBubbleButton;

#if !UNITY_ANDROID || UNITY_EDITOR
    private const int WindowsDeleteAttempts = 8;
    private const float WindowsRetryDelay = 0.25f;   // 8 x 0.25s = up to ~2s
#endif

    private ScenePropReference scenePropReference;

    private void Awake()
    {
        if (deleteBubbleButton != null)
            deleteBubbleButton.WhenSelect.AddListener(OnDeletePressed);
    }

    private void OnDestroy()
    {
        if (deleteBubbleButton != null)
            deleteBubbleButton.WhenSelect.RemoveListener(OnDeletePressed);
    }

    private void Start()
    {
        scenePropReference = ScenePropReference.Instance;
    }

    // ---- Entry points ----------------------------------------------------

    /// <summary>Poke-button hookup (PointableUnityEventWrapper.WhenSelect).</summary>
    public void OnDeletePressed(PointerEvent evt) => DeleteCurrentBubble();

    /// <summary>Parameterless hookup for a plain UnityEvent / Inspector button.</summary>
    public void DeleteCurrentBubble()
    {
        if (scenePropReference == null) 
        {
            scenePropReference = ScenePropReference.Instance;
        }

        Bubble bubble = scenePropReference != null ? scenePropReference.currentBubbleBeingGrabbed : null;
        if (bubble == null)
        {
            Debug.LogWarning($"{nameof(BubbleDeleter)}: no bubble is currently open, nothing to delete.");
            return;
        }

        DeleteBubble(bubble);
    }

    /// <summary>
    /// Removes a bubble everywhere: memory, JSON, disk, scene. Safe to call for any
    /// bubble, grabbed or not.
    /// </summary>
    public void DeleteBubble(Bubble bubble)
    {
        if (bubble == null) return;
        if (scenePropReference == null) 
        {
            scenePropReference = ScenePropReference.Instance;
        }

        SavedBubbleData saved = scenePropReference != null ? scenePropReference.savedBubbles : null;
        if (saved == null)
        {
            Debug.LogError($"{nameof(BubbleDeleter)}: no SavedBubbleData reference; aborting delete.");
            return;
        }

        // 1. Remember the recording path before the bubble (and its Data) is gone.
        string audioPath = bubble.BubbleData != null ? bubble.BubbleData.audioFilePath : null;

        // 2. Stop playback and release the streamed AudioClip. On Windows the clip
        //    keeps an open handle on the WAV, and an open handle blocks File.Delete.
        ReleaseAudioPlayback();

        // 3. Drop it from the live list, then rewrite bubbles.json from that list.
        //    Saving immediately (rather than at quit/pause) makes the deletion durable
        //    even if the headset is taken off or the app is killed right after.
        saved.Remove(bubble);
        if (saved.bubbles.Count > 0)
        {
            saved.SaveToFile();
        }
        else
        {
            // Nothing left for the JSON to describe — delete the file itself rather
            // than leaving an empty "[]" behind. Load() already handles it missing.
            DeleteFile(saved.SavePath, "bubble save file");
        }

        // 4. Delete the recording, unless another bubble still points at it.
        if (!string.IsNullOrEmpty(audioPath) && !IsAudioPathInUse(saved, audioPath, bubble))
            DeleteFile(Path.Combine(Application.persistentDataPath, audioPath), "recording");

        // 5. Close the menu and reset the grab state. The bubble is about to be
        //    destroyed, so BubbleGrabBehavior's unselect path can't be relied on to
        //    do this for us.
        CloseMenuAndClearGrabState(saved);

        // 6. Finally remove the object from the scene.
        Destroy(bubble.gameObject);
        Debug.Log($"{nameof(BubbleDeleter)}: bubble deleted. {saved.bubbles.Count} remaining.");
    }

    // ---- Cleanup helpers -------------------------------------------------

    // Stops the bubble's audio and destroys the streamed clip so its file handle is
    // released. Without this the WAV is still open when we try to delete it.
    private void ReleaseAudioPlayback()
    {
        AudioSource source = scenePropReference != null ? scenePropReference.audioSource : null;
        if (source == null) return;

        source.Stop();
        AudioClip clip = source.clip;
        source.clip = null;
        if (clip != null)
            Destroy(clip);
    }

    // Recording file names are GUIDs, so sharing shouldn't happen — but a stale copy
    // of a path in another bubble's data would otherwise silently kill its audio.
    private bool IsAudioPathInUse(SavedBubbleData saved, string audioPath, Bubble ignore)
    {
        foreach (Bubble other in saved.bubbles)
        {
            if (other == null || other == ignore || other.BubbleData == null) continue;
            if (other.BubbleData.audioFilePath == audioPath)
            {
                Debug.LogWarning($"{nameof(BubbleDeleter)}: '{audioPath}' is still used by another bubble; keeping the file.");
                return true;
            }
        }
        return false;
    }

    private void CloseMenuAndClearGrabState(SavedBubbleData saved)
    {
        if (scenePropReference == null) 
        {
            return;
        }

        scenePropReference.currentBubbleBeingGrabbed = null;
        scenePropReference.isBubbleMenuOpen = false;

        // Re-enable grabbing on the bubbles that were locked out while this one was held.
        saved.DisableAllBubbleGrabBehavior(true);

        // Do this last: it deactivates the menu, and this component with it.
        if (scenePropReference.bubbleMenu != null)
            scenePropReference.bubbleMenu.SetActive(false);
    }

    // ---- File deletion ---------------------------------------------------

    /// <summary>
    /// Deletes a file on disk, retrying on the platforms that need it.
    ///
    /// The coroutine is started on ScenePropReference, not on this component:
    /// deleting a bubble closes the Bubble Menu, and this script lives on that menu,
    /// so a coroutine started here would be killed by its own SetActive(false) before
    /// the retries finished. ScenePropReference is the always-active scene singleton,
    /// so the work runs to completion. The routine itself is static and only touches
    /// System.IO / Resources, so it stays valid even after this component is gone.
    /// </summary>
    private void DeleteFile(string absolutePath, string label)
    {
        if (string.IsNullOrEmpty(absolutePath)) return;

        if (!File.Exists(absolutePath))
        {
            Debug.Log($"{nameof(BubbleDeleter)}: {label} already gone: {absolutePath}");
            return;
        }

        MonoBehaviour host = scenePropReference != null ? scenePropReference : this;
        host.StartCoroutine(DeleteFileRoutine(absolutePath, label));
    }

    // Quest/Android and Windows behave differently here and genuinely need different
    // handling:
    //
    // * Quest / Android — persistentDataPath is app-private internal storage, so no
    //   storage permission is involved and scoped storage doesn't apply. ext4 also
    //   unlinks a file that still has open handles, so one File.Delete succeeds
    //   immediately even if Unity's streaming AudioClip hasn't let go yet.
    //
    // * Windows (standalone build, Editor, and PC/Link play) — the OS refuses to
    //   delete a file while any handle on it is open, and Unity's streamed AudioClip
    //   (UnityWebRequest with streamAudio) holds one until the clip is destroyed AND
    //   unloaded. So: wait a frame for the deferred Destroy, force an asset unload,
    //   clear any read-only attribute, and retry for ~2s before giving up.
    private static IEnumerator DeleteFileRoutine(string absolutePath, string label)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (TryDelete(absolutePath, out string error))
            Debug.Log($"{nameof(BubbleDeleter)}: deleted {label}: {absolutePath}");
        else
            Debug.LogError($"{nameof(BubbleDeleter)}: failed to delete {label} '{absolutePath}': {error}");
        yield break;
#else
        // Destroy() is deferred to the end of the frame, so wait one frame for the
        // AudioClip to actually go away before asking Unity to unload it.
        yield return null;
        AsyncOperation unload = Resources.UnloadUnusedAssets();
        while (!unload.isDone)
            yield return null;

        string lastError = null;
        for (int attempt = 1; attempt <= WindowsDeleteAttempts; attempt++)
        {
            if (TryDelete(absolutePath, out lastError))
            {
                Debug.Log($"{nameof(BubbleDeleter)}: deleted {label} on attempt {attempt}: {absolutePath}");
                yield break;
            }

            // Still locked — wait for whatever is holding it to let go, then retry.
            // Realtime so a paused / zero-timescale menu doesn't stall the retries.
            yield return new WaitForSecondsRealtime(WindowsRetryDelay);
        }

        Debug.LogError($"{nameof(BubbleDeleter)}: could not delete {label} '{absolutePath}' " +
                       $"after {WindowsDeleteAttempts} attempts (file still locked): {lastError}");
#endif
    }

    // Separate from the coroutine because C# forbids yielding inside a try/catch.
    private static bool TryDelete(string absolutePath, out string error)
    {
        error = null;
        try
        {
            if (!File.Exists(absolutePath)) return true;   // someone else got there first
#if !UNITY_ANDROID || UNITY_EDITOR
            // A read-only attribute makes File.Delete throw on Windows. Meaningless
            // on Android, so it's skipped there.
            File.SetAttributes(absolutePath, FileAttributes.Normal);
#endif
            File.Delete(absolutePath);
            return !File.Exists(absolutePath);
        }
        catch (System.Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}
