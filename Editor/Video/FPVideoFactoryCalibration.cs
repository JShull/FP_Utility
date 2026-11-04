// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using UnityEngine;

    /// <summary>X6 offset_v6 unified-sphere / 13-term distortion model. Infinite-distance projection only.</summary>
    [Serializable]
    internal sealed class FPVideoFactoryCalibration
    {
        [Serializable] internal sealed class Lens { public double[] values; }
        public string fingerprint;
        public int packedFlags, streamType;
        public bool reverseTracks;
        public Lens[] lenses;
        private static readonly string[][] Names = {
            new[] { "_FactoryAI", "_FactoryAC", "_FactoryAD0", "_FactoryAD1", "_FactoryAD2", "_FactoryAD3" },
            new[] { "_FactoryBI", "_FactoryBC", "_FactoryBD0", "_FactoryBD1", "_FactoryBD2", "_FactoryBD3" } };
        internal static FPVideoFactoryCalibration Parse(string text, int streamType)
        {
            if (text == null || text.Length > 8192) throw new InvalidDataException("Invalid factory calibration text.");
            string[] tokens = text.Split('_');
            if (tokens.Length != 56 || tokens[0] != "2" || !int.TryParse(tokens[55], NumberStyles.Integer, CultureInfo.InvariantCulture, out int flags) ||
                flags >> 16 != 6 || (streamType != 3 && streamType != 4)) throw new InvalidDataException("Factory projection requires a two-track X6 v6 calibration.");
            var result = new FPVideoFactoryCalibration { packedFlags = flags, streamType = streamType, reverseTracks = streamType == 4, lenses = new Lens[2] };
            for (int lens = 0; lens < 2; lens++)
            {
                var values = new double[27];
                for (int j = 0; j < values.Length; j++)
                    if (!double.TryParse(tokens[1 + lens * 27 + j], NumberStyles.Float, CultureInfo.InvariantCulture, out values[j])) throw new InvalidDataException("Invalid factory calibration number.");
                result.lenses[lens] = new Lens { values = values };
            }
            using (var sha = SHA256.Create()) result.fingerprint = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text + "|" + streamType.ToString(CultureInfo.InvariantCulture)))).Replace("-", "").ToLowerInvariant();
            result.Validate(); return result;
        }
        internal void Validate()
        {
            int mirror = (packedFlags >> 8) & 255;
            if (packedFlags >> 16 != 6 || (packedFlags & 255) != 0 || (mirror != 4 && mirror != 5) ||
                (streamType != 3 && streamType != 4) || lenses == null || lenses.Length != 2)
                throw new InvalidDataException("Unsupported factory model or track layout.");
            for (int i = 0; i < 2; i++)
            {
                var v = lenses[i]?.values;
                if (v == null || v.Length != 27) throw new InvalidDataException("Factory lens requires 27 parameters.");
                foreach (double x in v) if (double.IsNaN(x) || double.IsInfinity(x) || Math.Abs(x) > 1000000) throw new InvalidDataException("Invalid factory lens parameter.");
                if (v[0] <= 1 || v[0] > 10 || v[1] <= 0 || v[2] <= 0 || v[24] < 16 || v[24] > 65536 || v[25] < 16 ||
                    v[24] != v[25] * 2 || v[25] != Math.Round(v[25]) || v[26] != 193 ||
                    v[3] < i * v[25] || v[3] > (i + 1) * v[25] || v[4] < 0 || v[4] > v[25])
                    throw new InvalidDataException("Unsupported X6 factory intrinsics or calibration dimensions.");
                for (int j = 8; j < 11; j++) if (Math.Abs(v[j]) > 1) throw new InvalidDataException("Invalid factory lens origin.");
                for (int j = 5; j < 8; j++) if (Math.Abs(v[j]) > 360) throw new InvalidDataException("Invalid factory lens rotation.");
            }
            if (lenses[0].values[24] != lenses[1].values[24] || lenses[0].values[25] != lenses[1].values[25]) throw new InvalidDataException("Factory lens frames differ.");
        }
        internal FPVideoFactoryCalibration Snapshot()
        {
            // Unity's inline serialization can materialize a null reference as an empty object.
            if (packedFlags == 0) return null;
            Validate();
            var copy = (FPVideoFactoryCalibration)MemberwiseClone(); copy.lenses = new Lens[2];
            for (int i = 0; i < 2; i++) copy.lenses[i] = new Lens { values = (double[])lenses[i].values.Clone() };
            return copy;
        }
        internal bool SameModel(FPVideoFactoryCalibration other)
        {
            if (other == null || packedFlags != other.packedFlags || reverseTracks != other.reverseTracks || lenses == null || other.lenses == null || lenses.Length != 2 || other.lenses.Length != 2) return false;
            for (int i = 0; i < 2; i++)
            {
                var a = lenses[i]?.values; var b = other.lenses[i]?.values;
                if (a == null || b == null || a.Length != 27 || b.Length != 27) return false;
                for (int j = 0; j < 27; j++) if (a[j] != b[j]) return false;
            }
            return true;
        }
        internal int LensIndex(int track) => reverseTracks ? 1 - track : track;
        private static Quaternion VendorRotation(double yaw, double pitch, double roll, int lens) =>
            Quaternion.AngleAxis((float)pitch + 90, Vector3.up) * Quaternion.AngleAxis((float)yaw, Vector3.forward) *
            Quaternion.AngleAxis((float)roll, Vector3.right) * (lens == 0 ? Quaternion.AngleAxis(180, Vector3.forward) : Quaternion.identity);
        internal Matrix4x4 WorldToLens(int track)
        {
            int index = LensIndex(track); var v = lenses[index].values;
            Quaternion baseline = VendorRotation(0, 0, 90, LensIndex(0));
            var flip = Matrix4x4.Scale(new Vector3(1, -1, 1));
            return flip * Matrix4x4.Rotate(VendorRotation(v[5], v[6], v[7], index) * Quaternion.Inverse(baseline)) * flip;
        }
        internal Vector2 Project(Vector3 worldRay, int track)
        {
            int index = LensIndex(track); var v = lenses[index].values;
            Vector3 ray = WorldToLens(track).MultiplyVector(worldRay.normalized);
            double denominator = ray.z + v[0], x = ray.x / denominator, y = -ray.y / denominator;
            double r2 = x * x + y * y, r4 = r2 * r2;
            double radial = 1 + r2 * (v[11] + r2 * (v[12] + r2 * (v[13] + r2 * (v[14] + r2 * v[15]))));
            double tx = v[16] + v[18] * r2, ty = v[17] + v[19] * r2;
            double dx = x * radial + (r2 + 2 * x * x) * tx + 2 * x * y * ty + v[20] * r2 + v[22] * r4;
            double dy = y * radial + (r2 + 2 * y * y) * ty + 2 * x * y * tx + v[21] * r2 + v[23] * r4;
            if (index == 0 && ((packedFlags >> 8) & 255) == 5) dx = -dx;
            double u = (v[1] * dx + v[3] - index * v[25]) / v[25], vv = 1 - (v[2] * dy + v[4]) / v[25];
            return new Vector2((float)((track + u) * .5), (float)vv);
        }
        internal static void Bind(Material material, FPVideoUnwrapSettings settings)
        {
            bool enabled = settings.useFactoryCalibration && settings.sourceProjection == FPVideoSourceProjection.DualFisheye;
            material.SetFloat("_FactoryEnabled", enabled ? 1 : 0);
            if (!enabled) return;
            var model = settings.factoryCalibration;
            if (model == null) throw new InvalidDataException("Load an X6 factory calibration before enabling it.");
            model.Validate();
            material.SetFloat("_RightLensFov", settings.lensFov);
            material.SetMatrix("_LeftRotation", model.WorldToLens(0)); material.SetMatrix("_RightRotation", model.WorldToLens(1));
            for (int track = 0; track < 2; track++)
            {
                int index = model.LensIndex(track); var v = model.lenses[index].values; var names = Names[track];
                float mirror = index == 0 && ((model.packedFlags >> 8) & 255) == 5 ? -1 : 1;
                material.SetVector(names[0], new Vector4((float)v[0], (float)(v[1] / v[25]), (float)(v[2] / v[25]), mirror));
                material.SetVector(names[1], new Vector4((float)((v[3] - index * v[25]) / v[25]), (float)(v[4] / v[25]), track * .5f, .5f));
                for (int k = 0; k < 4; k++) material.SetVector(names[2 + k], new Vector4((float)v[11 + k * 4], k == 3 ? 0 : (float)v[12 + k * 4], k == 3 ? 0 : (float)v[13 + k * 4], k == 3 ? 0 : (float)v[14 + k * 4]));
            }
        }
    }
}
