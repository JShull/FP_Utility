namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;
    public sealed class FPRuntimeGridPlane : MonoBehaviour
    {
        private static readonly List<FPRuntimeGridPlane> s_Active = new();
        public static IReadOnlyList<FPRuntimeGridPlane> Active => s_Active;

        [Header("Appearance")]
        public Color MinorColor = new Color(1, 1, 1, 0.25f);
        public Color MajorColor = new Color(1, 1, 1, 0.45f);
        [Min(0f)] public float Opacity = 1f;

        [Header("Grid Params (World Units)")]
        public UnitOfMeasure Units = UnitOfMeasure.Inch;
        [Min(0.0001f)]
        public float SpacingInUnits = 1f;

        [Min(0.0001f)]
        public float MajorSpacingInUnits = 1.0f; // optional: e.g. 1 m major line, 12 in, etc.
        public bool UseMajorSpacing = false;

        [Tooltip("For Units=Custom: meters per custom unit.")]
        [Min(0.0000001f)]
        public float CustomMetersPerUnit = 1f;

        // --- Render-facing cached values (meters) ---
        [SerializeField, HideInInspector] private float _spacingWorldMeters = 0.1f;
        [SerializeField, HideInInspector] private float _majorSpacingWorldMeters = 1.0f;
        public float SpacingWorldMeters => _spacingWorldMeters;
        public float MajorSpacingWorldMeters => _majorSpacingWorldMeters;

        [Header("Line Thickness (Pixels)")]
        [Min(0.1f)] public float MinorThicknessPx = 1.0f;
        [Min(0.1f)] public float MajorThicknessPx = 1.8f;

        [Header("Plane Extents (World Units)")]
        public Vector2 ExtentsWorld = new Vector2(50f, 50f);

        [Header("Enable")]
        public bool IsEnabled = true;

        private void OnValidate()
        {
            RecalculateWorldSpacing();
        }
        private void Awake()
        {
            // In case values are set by code at runtime
            RecalculateWorldSpacing();
        }
        public int MajorEveryComputed
        {
            get
            {
                float spacing = Mathf.Max(0.000001f, SpacingWorldMeters);
                float major = Mathf.Max(spacing, MajorSpacingWorldMeters);
                return Mathf.Max(1, Mathf.RoundToInt(major / spacing));
            }
        }

        public void RecalculateWorldSpacing()
        {
            // Convert spacing from Units -> meters
            var (okS, metersS) = FP_UtilityData.ConvertValue(
                SpacingInUnits,
                Units,
                UnitOfMeasure.Meter,
                customFrom: CustomMetersPerUnit,
                customTo: 1f);

            _spacingWorldMeters = okS ? Mathf.Max(0.000001f, metersS) : Mathf.Max(0.000001f, SpacingInUnits);

            if (UseMajorSpacing)
            {
                var (okM, metersM) = FP_UtilityData.ConvertValue(
                    MajorSpacingInUnits,
                    Units,
                    UnitOfMeasure.Meter,
                    customFrom: CustomMetersPerUnit,
                    customTo: 1f);

                _majorSpacingWorldMeters = okM ? Mathf.Max(_spacingWorldMeters, metersM) : Mathf.Max(_spacingWorldMeters, MajorSpacingInUnits);
            }
            else
            {
                // If you’re still using MajorEvery in the shader, you can ignore this.
                _majorSpacingWorldMeters = _spacingWorldMeters * 10f;
            }
        }
        private void OnEnable()
        {
            if (!s_Active.Contains(this)) s_Active.Add(this);
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
        }
    }
}
