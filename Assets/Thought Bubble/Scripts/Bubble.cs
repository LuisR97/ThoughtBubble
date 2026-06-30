using UnityEngine;

[System.Serializable]
public class Bubble
{
    // current position
    public double currentX, currentY, currentZ;
    // bubbleColor, 0–255 per channel
    public int r, g, b, a;
    public string transcription;
    public bool isMovingClockwise;

    public Bubble(double currentX, double currentY, double currentZ, int r, int g, int b, int a, string transcription, bool isMovingClockwise)
    {
        this.currentX = currentX;
        this.currentY = currentY;
        this.currentZ = currentZ;
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
        this.transcription = transcription;
        this.isMovingClockwise = isMovingClockwise;
    }
}
