// Copyright (c) 2026 John B. Shull. See LICENSE.md.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("com.fuzzphyte.utility.editor.tests")]
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using UnityEngine;

    internal enum FPVideoProjection { Equirectangular, Cylindrical, Perspective }
    internal enum FPVideoSourceProjection { DualFisheye, Equirectangular }
    internal enum FPVideoToneCurve { Neutral, SoftFilm, FlatLog113, CustomGamma, Vivid, ShadowDetail, VividShadowDetail }
    internal enum FPVideoCalibrationView { Composite, LensA, LensB, OverlapOverlay, Difference, Coverage, FlowVectors, FlowConfidence, WarpedDifference }

    [Serializable]
    internal sealed class FPVideoUnwrapSettings
    {
        public FPVideoSourceProjection sourceProjection;
        public FPVideoProjection projection;
        public int width = 2048, height = 1024;
        public float videoBitrateMbps = 40;
        public float horizontalFov = 100, verticalFov = 100;
        public Vector3 rotation;
        public float lensFov = 200, blendDegrees = 10;
        // Centers and radii are normalized against the complete source image.
        public Vector2 leftCenter = new Vector2(.25f, .5f), rightCenter = new Vector2(.75f, .5f);
        public Vector2 lensRadius = new Vector2(.25f, .5f);
        public Vector3 leftRotation, rightRotation = new Vector3(0, 180, 0);
        public bool independentRightLens;
        public string calibrationName = "Generic dual-fisheye (uncalibrated)";
        public bool useFactoryCalibration;
        public FPVideoFactoryCalibration factoryCalibration;
        public bool stabilizeMotion, levelHorizon = true, lockDirection;
        public float motionSmoothingSeconds = .3f, motionOffsetMilliseconds;
        public double motionReferenceSeconds;
        public float rightLensFov = 200;
        public Vector2 rightLensRadius = new Vector2(.25f, .5f);
        public bool useDirectionalSeam;
        public float seamYaw, seamWidth = 10;
        public bool opticalFlow;
        public int flowResolution = 512, flowIterations = 5;
        public float flowMaxWarpDegrees = 2, flowStrength = 1, flowMinConfidence = .5f;
        public bool flowTemporalSmoothing;
        public float flowSmoothingSeconds = .08f, flowTemporalRejectDegrees = 1;
        public bool flipX, flipY;
        public bool colorTuning;
        public float colorExposure, colorTemperature, colorTint;
        public float colorContrast = 1, colorSaturation = 1;
        public FPVideoToneCurve colorCurve;
        public float colorCurveStrength = 1, colorGamma = 1;

        internal FPVideoUnwrapSettings Snapshot()
        {
            var copy = (FPVideoUnwrapSettings)MemberwiseClone();
            copy.factoryCalibration = factoryCalibration?.Snapshot(); return copy;
        }

        internal bool SameFlowHistory(FPVideoUnwrapSettings other) => other != null &&
            useFactoryCalibration == other.useFactoryCalibration && (!useFactoryCalibration || (factoryCalibration != null && factoryCalibration.SameModel(other.factoryCalibration))) &&
            lensFov == other.lensFov && leftCenter.Equals(other.leftCenter) && rightCenter.Equals(other.rightCenter) &&
            lensRadius.Equals(other.lensRadius) && leftRotation.Equals(other.leftRotation) && rightRotation.Equals(other.rightRotation) &&
            independentRightLens == other.independentRightLens && rightLensFov == other.rightLensFov && rightLensRadius.Equals(other.rightLensRadius) &&
            flipX == other.flipX && flipY == other.flipY && seamYaw == other.seamYaw && seamWidth == other.seamWidth &&
            flowResolution == other.flowResolution && flowIterations == other.flowIterations && flowMaxWarpDegrees == other.flowMaxWarpDegrees &&
            flowStrength == other.flowStrength && flowMinConfidence == other.flowMinConfidence && flowTemporalSmoothing == other.flowTemporalSmoothing &&
            flowSmoothingSeconds == other.flowSmoothingSeconds && flowTemporalRejectDegrees == other.flowTemporalRejectDegrees;

        internal void Validate()
        {
            if (stabilizeMotion && (!useFactoryCalibration || factoryCalibration == null || !factoryCalibration.reverseTracks || factoryCalibration.streamType != 4))
                throw new ArgumentException("Motion stabilization requires the supported X6 factory projection and declared reversed track order.");
            if (!Finite(motionSmoothingSeconds) || motionSmoothingSeconds < .05f || motionSmoothingSeconds > 3 || !Finite(motionOffsetMilliseconds) || Math.Abs(motionOffsetMilliseconds) > 100 ||
                double.IsNaN(motionReferenceSeconds) || double.IsInfinity(motionReferenceSeconds) || motionReferenceSeconds < 0)
                throw new ArgumentException("Motion smoothing requires 0.05–3 seconds, timing offset ±100 ms and a finite nonnegative heading reference.");
            if (useFactoryCalibration)
            {
                if (sourceProjection != FPVideoSourceProjection.DualFisheye || factoryCalibration == null) throw new ArgumentException("Factory projection requires a dual-fisheye source and an X6 calibration.");
                factoryCalibration.Validate();
            }
            if (colorTuning && (!Finite(colorExposure) || Math.Abs(colorExposure) > 4 ||
                !Finite(colorTemperature) || Math.Abs(colorTemperature) > 1 || !Finite(colorTint) || Math.Abs(colorTint) > 1 ||
                !Finite(colorContrast) || colorContrast < 0 || colorContrast > 2 ||
                !Finite(colorSaturation) || colorSaturation < 0 || colorSaturation > 2))
                throw new ArgumentException("Color tuning requires exposure ±4 stops, temperature/tint ±1, contrast/saturation 0–2.");
            if (colorTuning && (!Enum.IsDefined(typeof(FPVideoToneCurve),colorCurve) ||
                !Finite(colorCurveStrength) || colorCurveStrength < 0 || colorCurveStrength > 1 ||
                !Finite(colorGamma) || colorGamma < .5f || colorGamma > 2.5f))
                throw new ArgumentException("Color curve requires a supported preset, strength 0–1 and midtone gamma 0.5–2.5.");
            if (!Finite(videoBitrateMbps) || videoBitrateMbps < 1 || videoBitrateMbps > 500)
                throw new ArgumentException("Video bitrate must be between 1 and 500 Mbps.");
            if (width < 16 || height < 16 || width > 8192 || height > 8192 || width % 2 != 0 || height % 2 != 0)
                throw new ArgumentException("Output dimensions must be even, between 16 and 8192 pixels.");
            if (projection == FPVideoProjection.Equirectangular && width != height * 2)
                throw new ArgumentException("Equirectangular output must have a 2:1 aspect ratio.");
            if (!Finite(horizontalFov) || !Finite(verticalFov) || horizontalFov < 1 || horizontalFov >= 179 || verticalFov < 1 || verticalFov >= 179)
                throw new ArgumentException("View fields of view must be between 1 and 179 degrees (exclusive upper bound).");
            if (!Finite(lensFov) || lensFov < 180 || lensFov > 240 || !Finite(blendDegrees) || blendDegrees < .1f || blendDegrees > 30)
                throw new ArgumentException("Lens FOV must be 180–240 degrees and blend width 0.1–30 degrees.");
            if (!Finite(lensRadius.x) || !Finite(lensRadius.y) || lensRadius.x <= 0 || lensRadius.y <= 0 ||
                !Inside(leftCenter) || !Inside(rightCenter) || lensRadius.x > 1 || lensRadius.y > 1)
                throw new ArgumentException("Lens centers must be inside the image and radii must be positive normalized values up to 1.");
            if (!Finite(rotation) || !Finite(leftRotation) || !Finite(rightRotation)) throw new ArgumentException("Rotations must be finite.");
            if (independentRightLens && (!Finite(rightLensFov) || rightLensFov < 180 || rightLensFov > 240 ||
                !Finite(rightLensRadius.x) || !Finite(rightLensRadius.y) || rightLensRadius.x <= 0 || rightLensRadius.y <= 0 || rightLensRadius.x > 1 || rightLensRadius.y > 1))
                throw new ArgumentException("Lens B requires FOV 180–240 degrees and positive normalized radii up to 1.");
            if (!Finite(seamYaw) || Math.Abs(seamYaw) > 180 || !Finite(seamWidth) || seamWidth < 0 || seamWidth > 60)
                throw new ArgumentException("Seam yaw must be within ±180 degrees and seam width within 0–60 degrees.");
            if (opticalFlow && (sourceProjection != FPVideoSourceProjection.DualFisheye || !useDirectionalSeam))
                throw new ArgumentException("Seam flow requires a dual-fisheye source and directional seam.");
            if (opticalFlow && (flowResolution < 128 || flowResolution > 1024 || (flowResolution & (flowResolution - 1)) != 0 || flowIterations < 1 || flowIterations > 8 ||
                !Finite(flowMaxWarpDegrees) || flowMaxWarpDegrees < .1f || flowMaxWarpDegrees > 10 || !Finite(flowStrength) || flowStrength < 0 || flowStrength > 1 ||
                !Finite(flowMinConfidence) || flowMinConfidence < .05f || flowMinConfidence > .95f))
                throw new ArgumentException("Flow requires resolution 128/256/512/1024, 1–8 iterations, 0.1–10° warp, strength 0–1 and confidence 0.05–0.95.");
            if (opticalFlow && flowTemporalSmoothing && (!Finite(flowSmoothingSeconds) || flowSmoothingSeconds < .01f || flowSmoothingSeconds > .5f ||
                !Finite(flowTemporalRejectDegrees) || flowTemporalRejectDegrees < .1f || flowTemporalRejectDegrees > 5))
                throw new ArgumentException("Temporal smoothing requires response 0.01–0.5 seconds and motion rejection 0.1–5 degrees.");
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Inside(Vector2 v) => Finite(v.x) && Finite(v.y) && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1;

        internal Vector3 OutputDirection(Vector2 uv)
        {
            double longitude = (uv.x - .5) * Math.PI * 2;
            Vector3 direction;
            if (projection == FPVideoProjection.Perspective)
            {
                double tangent = Math.Tan(horizontalFov * Math.PI / 360);
                direction = new Vector3((float)((uv.x * 2 - 1) * tangent), (float)((uv.y * 2 - 1) * tangent * height / width), 1).normalized;
            }
            else if (projection == FPVideoProjection.Cylindrical)
                direction = new Vector3((float)Math.Sin(longitude), (float)((uv.y * 2 - 1) * Math.Tan(verticalFov * Math.PI / 360)), (float)Math.Cos(longitude)).normalized;
            else
            {
                double latitude = (uv.y - .5) * Math.PI;
                direction = new Vector3((float)(Math.Sin(longitude) * Math.Cos(latitude)), (float)Math.Sin(latitude), (float)(Math.Cos(longitude) * Math.Cos(latitude)));
            }
            return Quaternion.Euler(rotation) * direction;
        }
    }
}
