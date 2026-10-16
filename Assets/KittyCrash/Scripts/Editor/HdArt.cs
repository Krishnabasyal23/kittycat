using System.IO;
using UnityEngine;

namespace KittyCrash.EditorTools
{
    /// <summary>
    /// The generated art in Art/Generated was re-rendered at 4x resolution with smooth edges (marker file _HD.txt).
    /// Every installer that imports that art asks this class for Pixels-Per-Unit and filtering, so sprites keep
    /// their size in the world but look sharp instead of blocky.
    /// </summary>
    public static class HdArt
    {
        public const string ArtDir = "Assets/KittyCrash/Art/Generated";
        public const int Scale = 4;

        public static bool IsHd => File.Exists(ArtDir + "/_HD.txt");
        /// <summary>Crisp 2x pixel art (Scale2x): same look, half-size pixels.</summary>
        public static bool IsX2 => File.Exists(ArtDir + "/_X2.txt");

        public static int Factor => IsHd ? Scale : IsX2 ? 2 : 1;

        /// <summary>Weather sprites use ppu = image size (1 sprite = 1 unit), so they don't need the factor.</summary>
        public static int Ppu(string name, int basePpu) => !name.StartsWith("weather_") ? basePpu * Factor : basePpu;

        public static FilterMode Filter => IsHd ? FilterMode.Bilinear : FilterMode.Point;
    }
}
