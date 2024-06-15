namespace FuzzPhyte.Utility
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public static class FP_ColorTheory 
    {
        public delegate Color BlendFunction(Color[] colors);
        public static Color[] GenerateAnalogousColors(Color primaryColor)
        {
            float hue, saturation, value;
            Color.RGBToHSV(primaryColor, out hue, out saturation, out value);

            // Adjust the hue to get analogous colors
            float hue1 = (hue + 30f / 360f) % 1f; // 30 degrees on the color wheel
            float hue2 = (hue - 30f / 360f + 1f) % 1f; // -30 degrees on the color wheel

            Color color1 = Color.HSVToRGB(hue1, saturation, value);
            Color color2 = Color.HSVToRGB(hue2, saturation, value);

            return new Color[] { primaryColor, color1, color2 };
        }

        public static Color[] GenerateSingleComplementaryColors(Color primaryColor)
        {
            float hue, saturation, value;
            Color.RGBToHSV(primaryColor, out hue, out saturation, out value);

            // Adjust the hue to get the complementary color
            float hue1 = (hue + 180f / 360f) % 1f; // 180 degrees on the color wheel

            Color color1 = Color.HSVToRGB(hue1, saturation, value);

            return new Color[] { primaryColor, color1 };
        }
        /// <summary>
        /// will generate one new color based on blend of the other colors passed in
        /// if the array has two colors, it will return 4 colors
        /// Max cap is 4 colors in addition to the 4 you pass in so 8 total
        /// </summary>
        /// <param name="primaryColors">would keep this maybe at the 1,2</param>
        /// <returns></returns>
        public static Color[] GenerateComplementaryColors(Color[] primaryColors, BlendFunction blendFunction)
        {
            Color blendedColor = blendFunction(primaryColors);

            float hue, saturation, value;
            Color.RGBToHSV(blendedColor, out hue, out saturation, out value);

            float hueComplementary = (hue + 180f / 360f) % 1f;
            Color complementaryColor = Color.HSVToRGB(hueComplementary, saturation, value);

            var colors = new List<Color>();
            for(int i= 0; i < primaryColors.Length; i++)
            {
                colors.Add(primaryColors[i]);
                
            }
            colors.Add(complementaryColor);
            return colors.ToArray();
        }

        public static Color[] GenerateTriadicColors(Color primaryColor)
        {
            float hue, saturation, value;
            Color.RGBToHSV(primaryColor, out hue, out saturation, out value);

            // Adjust the hue to get triadic colors
            float hue1 = (hue + 120f / 360f) % 1f; // 120 degrees on the color wheel
            float hue2 = (hue - 120f / 360f + 1f) % 1f; // -120 degrees on the color wheel

            Color color1 = Color.HSVToRGB(hue1, saturation, value);
            Color color2 = Color.HSVToRGB(hue2, saturation, value);

            return new Color[] { primaryColor, color1, color2 };
        }
        public static Color SimpleBlendFunction(Color[] colors)
        {
            if (colors == null || colors.Length == 0)
                return Color.black;

            float r = 0, g = 0, b = 0;

            foreach (var color in colors)
            {
                r += color.r;
                g += color.g;
                b += color.b;
            }

            r /= colors.Length;
            g /= colors.Length;
            b /= colors.Length;

            return new Color(r, g, b);
        }
        public static Color BlendColorsHSV(Color[] colors)
        {
            if (colors == null || colors.Length == 0)
                return Color.black;

            float totalHue = 0f, totalSaturation = 0f, totalValue = 0f;

            foreach (var color in colors)
            {
                Color.RGBToHSV(color, out float hue, out float saturation, out float value);
                totalHue += hue;
                totalSaturation += saturation;
                totalValue += value;
            }

            float avgHue = totalHue / colors.Length;
            float avgSaturation = totalSaturation / colors.Length;
            float avgValue = totalValue / colors.Length;

            return Color.HSVToRGB(avgHue, avgSaturation, avgValue);
        }
    }
}
