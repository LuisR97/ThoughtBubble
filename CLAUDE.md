# ThoughtBubble

Unity VR app for Meta Quest, built for both the headset and for PC over Quest
Link. Voice-recorded "thought bubbles" that persist, orbit, and can be grabbed,
coloured, and deleted in-headset.

## Stack

    Unity                 6000.3.17f1
    Meta XR SDK           203.0.0  (core, interaction, interaction.ovr,
                                    platform, haptics, mrutilitykit)
    Interaction           Meta XR Interaction SDK (ISDK)
    Company / product     CuervoWorks / ThoughtBubble

**Not Unity XRI.** That migration is finished. `Assets/XRI/` and `Assets/XR/`
still exist on disk but are not what this project uses — do not suggest XRI
APIs or assume XRI components are in play.

## Build targets

Ships to **both** targets. Neither one is the "real" build — a change has to
work on both.

    PC-Link.asset         Windows Standalone x64, played over Quest Link
    MobileVRBuild.asset   Android APK, sideloaded to Quest. ARM64, ASTC,
                          minSdk 32, APK not AAB (an AAB cannot be adb-installed)

Both use IL2CPP, and both ship the same MainScene — the profiles inherit the
global scene list rather than overriding it.

**Link is not a preview of Quest.** Platform behaviour genuinely differs.
Measured example: `Microphone.Start()` costs ~1000 ms on PC/Link, because of
the Windows WASAPI device-open, versus ~27 ms on the Quest native build. Paths
that diverge are gated with `#if !UNITY_ANDROID`. Never conclude how something
behaves on Quest from testing over Link, or the reverse — measure on the target
in question, using `adb logcat -s Unity` for the headset.

## Layout

    runtime scripts       Assets/Thought Bubble/Scripts/
    editor tools          Assets/Thought Bubble/Editor/
    prefabs / materials   Assets/Thought Bubble/Prefabs/, .../Materials/
    main scene            Assets/Thought Bubble/Scenes/MainScene.unity
    build profiles        Assets/Settings/Build Profiles/  (see Build targets)

`Assets/MainScene.unity` is a STALE leftover from June and is not in the build.
The live scene is the one under `Assets/Thought Bubble/Scenes/`. Do not open or
edit the root-level one.

Read-only, never edit:

    Library/PackageCache/     Meta SDK source. Read it to check API behaviour.
    Assets/Oculus/            vendored SDK
    Assets/InteractionSDK/    vendored SDK
    Assets/_Recovery/         old copies kept for comparison

Saved app data lives outside the repo, in Unity's persistentDataPath:

    C:/Users/rayga/AppData/LocalLow/CuervoWorks/ThoughtBubble/
        bubbles.json        saved bubble records
        recordings/*.wav    one audio file per bubble
        Player.log          runtime log

## Hard rules

- **Never edit `.meta` files.** Unity generates them; hand-editing breaks the
  GUID links between assets and silently detaches references.
- **Never hand-edit `.unity`, `.prefab`, or `.asset` YAML.** Read them to
  inspect wiring, but make changes through the Unity Editor or an editor
  script. Hand-edited YAML corrupts prefab instances in ways that are hard
  to spot and hard to undo.
- **ISDK interactables initialize off `.enabled`.** Setting `.enabled` is the
  SDK's initialization signal. Calling `Enable()` / `Disable()` directly
  bypasses registry sync and leaves the interactable in a broken state —
  this is what caused the grey-button bug.
- Deleting a bubble must clear three things: the scene object, its record in
  `bubbles.json`, and its `.wav` in `recordings/`.

## Conventions

Match the surrounding code. Existing scripts are plain MonoBehaviours with
`[SerializeField]` private fields and `[Tooltip]` attributes wired in the
Inspector, `/// <summary>` doc comments on public members, and explanatory
comments that say WHY rather than what.

Buttons are instances of the Poke Button Prefab. Changes affecting all buttons
belong on the prefab, not on individual instances.
