# ThoughtBubble
VR Game where you can materialize your thoughts and emotions.

## First-time setup

### Whisper model weights (required — the repo does not contain them)

Bubbles are transcribed on-device by Whisper, a speech-recognition model run through
the [whisper.unity](https://github.com/Macoron/whisper.unity) package. The model's
*weights* — the trained numbers the model needs in order to do anything — live in a
single 31 MB file that is **deliberately not committed**, to keep the repo from
carrying a large binary.

So a fresh clone will compile and run, but transcription will silently do nothing until
you download that file. Run this from the repo root:

    curl -L -o "Assets/StreamingAssets/Whisper/ggml-tiny.en-q5_1.bin" \
      "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en-q5_1.bin"

That is Whisper `tiny.en` — the smallest English-only variant, quantized to 5 bits
(stored at reduced precision, which shrinks the file and speeds up inference at a small
cost in accuracy). It must end up at exactly:

    Assets/StreamingAssets/Whisper/ggml-tiny.en-q5_1.bin

because that path is what the `WhisperManager` component on the **AudioManager**
GameObject in `MainScene` is pointing at. If you swap in a different model file, update
that component's **Model Path** field to match, or it will fail to load.

**How to tell it worked:** with the file present, the Unity console logs the Whisper
model loading on play, and a transcribed bubble gets real text in its `transcription`
field in `bubbles.json`. Without it, you get a load error in the console and bubbles
keep the `transcribing…` placeholder forever.

`Assets/StreamingAssets/Whisper/` is kept in the repo (via a `.gitkeep`) so the folder
exists after cloning; only `*.bin` inside it is ignored.
