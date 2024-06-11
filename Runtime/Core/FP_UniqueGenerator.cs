using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FuzzPhyte.Utility
{
    /// <summary>
    /// Generate a unique ID based on a project name, item name, and color
    /// </summary>
    public static class FP_UniqueGenerator 
    {
        private static readonly char[] invalidFilenameChars;
        private static readonly char[] invalidPathChars;

        static FP_UniqueGenerator()
        {
            invalidFilenameChars = Path.GetInvalidFileNameChars();
            invalidPathChars = Path.GetInvalidPathChars();
            Array.Sort(invalidFilenameChars);
            Array.Sort(invalidPathChars);
        }

        public static string Encode(string projectName, string itemName, Color color)
        {
            // Validate inputs
            ValidateInput(projectName, invalidFilenameChars, nameof(projectName));
            ValidateInput(itemName, invalidFilenameChars, nameof(itemName));

            // Convert the color to a string representation
            string colorString = $"{color.r},{color.g},{color.b},{color.a}";

            // Combine all parts into a single string
            string combined = $"{projectName}|{itemName}|{colorString}";

            // Convert to a Base64 string for compact representation
            byte[] bytesToEncode = Encoding.UTF8.GetBytes(combined);
            return Convert.ToBase64String(bytesToEncode);
        }

        public static (string projectName, string itemName, Color color) Decode(string encodedString)
        {
            // Decode the Base64 string
            byte[] decodedBytes = Convert.FromBase64String(encodedString);
            string decodedString = Encoding.UTF8.GetString(decodedBytes);

            // Split the decoded string into parts
            string[] parts = decodedString.Split('|');
            if (parts.Length != 3)
            {
                throw new ArgumentException("Invalid encoded string");
            }

            // Extract the project name and item name
            string projectName = parts[0];
            string itemName = parts[1];

            // Convert the color string back to a Color object
            string[] colorParts = parts[2].Split(',');
            if (colorParts.Length != 4)
            {
                throw new ArgumentException("Invalid color data in encoded string");
            }

            float r = float.Parse(colorParts[0]);
            float g = float.Parse(colorParts[1]);
            float b = float.Parse(colorParts[2]);
            float a = float.Parse(colorParts[3]);
            Color color = new Color(r, g, b, a);

            return (projectName, itemName, color);
        }

        private static void ValidateInput(string input, char[] invalidChars, string paramName)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentException($"{paramName} cannot be null or empty");
            }

            if (input.Any(c => Array.BinarySearch(invalidChars, c) >= 0))
            {
                throw new ArgumentException($"{paramName} contains invalid characters");
            }
        }
    }
}
