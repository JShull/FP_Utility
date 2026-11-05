// Copyright (c) 2026 John B. Shull. GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.Timeline;
    using Object = UnityEngine.Object;

    internal sealed class FPDirectorReferenceLocation
    {
        internal PlayableDirector Director;
        internal Object Asset, Target;
        internal string Location, Kind, Detail;
        internal bool TargetsOld;
    }

    internal static class FPDirectorReferenceAudit
    {
        internal static void Scan(FPSceneReferencePlan plan, PlayableDirector director)
        {
            plan.AuditedDirectors++;
            // Asset traversal is limited to this director's playable, tracks and clips, never project dependencies.
            var assets = new List<KeyValuePair<Object, string>>();
            var tracks = new HashSet<TrackAsset>();
            if (director.playableAsset != null) assets.Add(new KeyValuePair<Object, string>(director.playableAsset, "Playable asset"));
            if (director.playableAsset is TimelineAsset timeline)
                foreach (var root in timeline.GetRootTracks()) AddTrack(root, "", assets, tracks);
            foreach (var pair in assets)
            {
                try
                {
                    ScanProperties(plan, director, pair.Key, pair.Value);
                }
                catch (Exception ex)
                {
                    plan.Warnings.Add("Director audit incomplete: " + FPSceneReferencePlanner.Describe(director) + " / " + pair.Value + ": " + ex.Message);
                    plan.DirectorAuditFailures++;
                }
            }
        }

        private static void ScanProperties(FPSceneReferencePlan plan, PlayableDirector director, Object asset, string location)
        {
            using (var so = new SerializedObject(asset))
            {
                var p = so.GetIterator();
                bool enter = true;
                while (p.Next(enter))
                {
                    enter = true;
                    if (p.propertyType == SerializedPropertyType.ExposedReference)
                    {
                        var key = p.FindPropertyRelative("exposedName");
                        var fallback = p.FindPropertyRelative("defaultValue");
                        string name = key?.stringValue ?? "";
                        bool valid = false;
                        var target = string.IsNullOrEmpty(name) ? null : director.GetReferenceValue(new PropertyName(name), out valid);
                        if (!valid) target = fallback?.objectReferenceValue;
                        Add(plan, director, asset, target, location + " / " + p.propertyPath, "Exposed reference",
                            "Key: " + (string.IsNullOrEmpty(name) ? "(empty)" : name) + (valid ? " | Director override" : " | default/unresolved (no Director override)"));
                        enter = false;
                    }
                    else if (p.propertyType == SerializedPropertyType.ObjectReference && p.propertyPath != "m_Script" && p.objectReferenceValue != null)
                        Add(plan, director, asset, p.objectReferenceValue, location + " / " + p.propertyPath, "Asset-held reference", "Read-only asset field");
                }
            }
        }

        private static void AddTrack(TrackAsset track, string parent, List<KeyValuePair<Object, string>> assets, HashSet<TrackAsset> tracks)
        {
            if (track == null || !tracks.Add(track)) return;
            string path = parent + "/" + track.name;
            assets.Add(new KeyValuePair<Object, string>(track, "Track " + path));
            int index = 0;
            foreach (var clip in track.GetClips())
                if (clip.asset != null) assets.Add(new KeyValuePair<Object, string>(clip.asset, "Track " + path + " / clip [" + index++ + "] " + clip.displayName + " @ " + clip.start.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "s"));
            foreach (var child in track.GetChildTracks()) AddTrack(child, path, assets, tracks);
        }

        private static void Add(FPSceneReferencePlan plan, PlayableDirector director, Object asset, Object target, string location, string kind, string detail)
        {
            if (plan.DirectorReferences.Any(r => r.Director == director && r.Asset == asset && r.Location == location && r.Kind == kind)) return;
            plan.DirectorReferences.Add(new FPDirectorReferenceLocation { Director = director, Asset = asset, Target = target,
                Location = location, Kind = kind, Detail = detail, TargetsOld = FPSceneReferencePlanner.Inside(target, plan.OldRoot) });
        }
    }
}
