// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.IO;
    using UnityEngine;

    /// <summary>Versioned calibration-only JSON; applying it preserves output, trim and view settings.</summary>
    [Serializable]
    internal sealed class FPVideoLensCalibration
    {
        [Serializable] private sealed class Header { public string format; public int version; }
        public string format = "FPVideoLensCalibration";
        public int version = 3;
        public string name;
        public float lensFov, rightLensFov, blendDegrees, seamYaw, seamWidth;
        public Vector2 leftCenter, rightCenter, lensRadius, rightLensRadius;
        public Vector3 leftRotation, rightRotation;
        public bool independentRightLens, useDirectionalSeam, flipX, flipY;
        public bool opticalFlow;
        public string flowMethod = FPVideoFarneback.MethodVersion;
        public int flowResolution = 512, flowIterations = 5;
        public float flowMaxWarpDegrees = 2, flowStrength = 1, flowMinConfidence = .5f;
        public bool flowTemporalSmoothing;
        public float flowSmoothingSeconds = .08f, flowTemporalRejectDegrees = 1;

        internal static FPVideoLensCalibration Capture(FPVideoUnwrapSettings s)
        {
            s.Validate();
            return new FPVideoLensCalibration
            {
                name = s.calibrationName, lensFov = s.lensFov, rightLensFov = s.rightLensFov, blendDegrees = s.blendDegrees,
                seamYaw = s.seamYaw, seamWidth = s.seamWidth, leftCenter = s.leftCenter, rightCenter = s.rightCenter,
                lensRadius = s.lensRadius, rightLensRadius = s.rightLensRadius, leftRotation = s.leftRotation, rightRotation = s.rightRotation,
                independentRightLens = s.independentRightLens, useDirectionalSeam = s.useDirectionalSeam, flipX = s.flipX, flipY = s.flipY,
                opticalFlow = s.opticalFlow, flowResolution = s.flowResolution, flowIterations = s.flowIterations,
                flowMaxWarpDegrees = s.flowMaxWarpDegrees, flowStrength = s.flowStrength, flowMinConfidence = s.flowMinConfidence,
                flowTemporalSmoothing = s.flowTemporalSmoothing, flowSmoothingSeconds = s.flowSmoothingSeconds, flowTemporalRejectDegrees = s.flowTemporalRejectDegrees
            };
        }
        internal static FPVideoLensCalibration Parse(string json)
        {
            var header = JsonUtility.FromJson<Header>(json);
            if (header == null || header.format != "FPVideoLensCalibration" || header.version < 1 || header.version > 3)
                throw new InvalidDataException("Choose a version 1, 2 or 3 FP Video lens calibration JSON file.");
            var calibration = JsonUtility.FromJson<FPVideoLensCalibration>(json);
            if (calibration == null) throw new InvalidDataException("Empty calibration file.");
            calibration.Apply(new FPVideoUnwrapSettings());
            return calibration;
        }
        internal void Apply(FPVideoUnwrapSettings settings)
        {
            if (format != "FPVideoLensCalibration" || version < 1 || version > 3) throw new InvalidDataException("Unsupported calibration format/version.");
            if (opticalFlow && (version < 2 || flowMethod != FPVideoFarneback.MethodVersion)) throw new InvalidDataException("Unsupported flow method/version.");
            var candidate = settings.Snapshot(); Write(candidate); candidate.Validate(); Write(settings);
        }
        private void Write(FPVideoUnwrapSettings s)
        {
            s.calibrationName = name; s.lensFov = lensFov; s.rightLensFov = rightLensFov; s.blendDegrees = blendDegrees;
            s.seamYaw = seamYaw; s.seamWidth = seamWidth; s.leftCenter = leftCenter; s.rightCenter = rightCenter;
            s.lensRadius = lensRadius; s.rightLensRadius = rightLensRadius; s.leftRotation = leftRotation; s.rightRotation = rightRotation;
            s.independentRightLens = independentRightLens; s.useDirectionalSeam = useDirectionalSeam; s.flipX = flipX; s.flipY = flipY;
            s.opticalFlow = version >= 2 && opticalFlow; s.flowResolution = flowResolution; s.flowIterations = flowIterations;
            s.flowMaxWarpDegrees = flowMaxWarpDegrees; s.flowStrength = flowStrength; s.flowMinConfidence = flowMinConfidence;
            s.flowTemporalSmoothing = version >= 3 && flowTemporalSmoothing;
            s.flowSmoothingSeconds = version >= 3 ? flowSmoothingSeconds : .08f;
            s.flowTemporalRejectDegrees = version >= 3 ? flowTemporalRejectDegrees : 1;
        }
    }
}
