using System;
namespace DeepSpaceMusic;
public static class VisualOpacity {
    public static float Lyrics(float value)=>float.IsNaN(value)||float.IsInfinity(value)?.88f:Math.Max(0,Math.Min(1,value));
    public static float Panel(float value)=>float.IsNaN(value)||float.IsInfinity(value)?1:Math.Max(.2f,Math.Min(1,value));
}
