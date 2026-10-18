// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System.Collections;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class FPLookAtLockAxisTests
    {
        private GameObject driver;
        private GameObject target;
        private FP_LookAtLockAxis motion;
        private Quaternion original;

        [SetUp]
        public void SetUp()
        {
            driver = new GameObject("Lock axis test");
            target = new GameObject("Look target");
            original = Quaternion.Euler(23, 41, -17);
            driver.transform.rotation = original;
            motion = driver.AddComponent<FP_LookAtLockAxis>();
            SetField("playOnSetup", false);
            SetField("loop", true);
            motion.SetupLockAxis(driver.transform, target.transform);
            SetField("isPaused", false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(driver);
            Object.DestroyImmediate(target);
        }

        [TestCase(1, 0, 0)]
        [TestCase(0, 1, 0)]
        [TestCase(0, 0, 1)]
        public void SingleAxis_TracksTwoFullTurnsInBothDirections(int x, int y, int z)
        {
            SetField("_zeroOutAxis", new Vector3(x, y, z));
            Vector3 axis = new Vector3(x, y, z);
            Vector3 reference = z == 1 ? Vector3.up : Vector3.back;
            IEnumerator routine = Routine();
            for (int step = 0; step <= 576; step++)
            {
                int angle = step <= 288 ? -720 + step * 5 : 720 - (step - 288) * 5;
                Quaternion expected = original * Quaternion.AngleAxis(angle, axis);
                target.transform.position = driver.transform.position + expected * reference * 3;
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(Quaternion.Angle(driver.transform.rotation, expected), Is.LessThan(0.1f), "Angle " + angle);
            }
        }

        [TestCase(1, 1, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(0, 1, 1)]
        [TestCase(1, 1, 1)]
        public void MultipleAxes_FullOrbitThroughPolesHasNoHalfTurnFlip(int x, int y, int z)
        {
            SetField("_zeroOutAxis", new Vector3(x, y, z));
            IEnumerator routine = Routine();
            Vector3 axis = x == 1 ? Vector3.right : Vector3.up;
            for (int angle = 0; angle <= 720; angle += 5)
            {
                Quaternion previous = driver.transform.rotation;
                Vector3 direction = original * Quaternion.AngleAxis(angle, axis) * Vector3.back;
                target.transform.position = driver.transform.position + direction * 3;
                routine.MoveNext();
                Assert.That(Vector3.Dot(-driver.transform.forward, direction), Is.GreaterThan(0.9999f));
                Assert.That(Quaternion.Angle(previous, driver.transform.rotation), Is.LessThan(6f), "Angle " + angle);
            }
        }

        [TestCase(1, 1, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(0, 1, 1)]
        [TestCase(1, 1, 1)]
        public void MultipleAxes_AimAcrossTheWholeSphere(int x, int y, int z)
        {
            SetField("_zeroOutAxis", new Vector3(x, y, z));
            IEnumerator routine = Routine();
            for (int latitude = -90; latitude <= 90; latitude += 15)
            {
                for (int longitude = 0; longitude < 360; longitude += 15)
                {
                    Vector3 direction = original * Quaternion.Euler(latitude, longitude, 0) * Vector3.forward;
                    target.transform.position = driver.transform.position + direction * 3;
                    routine.MoveNext();
                    Assert.That(Vector3.Dot(-driver.transform.forward, direction), Is.GreaterThan(0.9999f),
                        "Latitude " + latitude + ", longitude " + longitude);
                }
            }
        }

        [Test]
        public void TargetPosition_FollowsLocationAndIgnoresTargetRotation()
        {
            Vector3 direction = new Vector3(2, 1, -3).normalized;
            target.transform.position = driver.transform.position + direction * 4;
            IEnumerator routine = Routine();
            routine.MoveNext();
            Quaternion previous = driver.transform.rotation;
            target.transform.rotation = Quaternion.Euler(84, 165, 32);
            routine.MoveNext();
            Assert.That(Vector3.Dot(-driver.transform.forward, direction), Is.GreaterThan(0.9999f));
            Assert.That(Quaternion.Angle(previous, driver.transform.rotation), Is.LessThan(0.1f));

            target.transform.position = driver.transform.position - direction * 4;
            routine.MoveNext();
            Assert.That(Vector3.Dot(-driver.transform.forward, -direction), Is.GreaterThan(0.9999f));
        }

        [Test]
        public void TargetDirection_FollowsForwardAndIgnoresTargetPosition()
        {
            FieldInfo field = typeof(FP_LookAtLockAxis).GetField("targetMode", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(motion, System.Enum.Parse(field.FieldType, "TargetDirection"));
            IEnumerator routine = Routine();
            for (int angle = 0; angle <= 360; angle += 15)
            {
                target.transform.rotation = Quaternion.Euler(25, angle, 12);
                // Direction mode must also work when both transforms occupy the same position.
                target.transform.position = driver.transform.position;
                routine.MoveNext();
                Assert.That(Vector3.Dot(-driver.transform.forward, target.transform.forward), Is.GreaterThan(0.9999f));
                Quaternion previous = driver.transform.rotation;
                target.transform.position = new Vector3(angle, -15, 7);
                routine.MoveNext();
                Assert.That(Quaternion.Angle(previous, driver.transform.rotation), Is.LessThan(0.1f));
            }
        }

        [Test]
        public void TargetPosition_ParentedAndUnparentedTargetsProduceSameRotation()
        {
            GameObject parent = new GameObject("Separate target parent");
            try
            {
                parent.transform.SetPositionAndRotation(new Vector3(8, -3, 6), Quaternion.Euler(31, 137, -23));
                parent.transform.localScale = new Vector3(2, 0.5f, 3);
                target.transform.SetParent(parent.transform, false);
                target.transform.localPosition = new Vector3(1, 2, -4);
                IEnumerator routine = Routine();
                routine.MoveNext();
                Quaternion parentedRotation = driver.transform.rotation;
                Vector3 worldPosition = target.transform.position;
                Assert.That(Vector3.Dot(-driver.transform.forward,
                    (worldPosition - driver.transform.position).normalized), Is.GreaterThan(0.9999f));

                target.transform.SetParent(null, true);
                driver.transform.rotation = original;
                routine.MoveNext();
                Assert.That(Quaternion.Angle(driver.transform.rotation, parentedRotation), Is.LessThan(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TargetPosition_FollowsMovingSeparateParent()
        {
            GameObject parent = new GameObject("Moving target parent");
            try
            {
                target.transform.SetParent(parent.transform, false);
                target.transform.localPosition = new Vector3(1, 2, -4);
                IEnumerator routine = Routine();
                for (int angle = 0; angle <= 360; angle += 15)
                {
                    parent.transform.SetPositionAndRotation(new Vector3(3, 2, -5), Quaternion.Euler(17, angle, 24));
                    routine.MoveNext();
                    Vector3 direction = (target.transform.position - driver.transform.position).normalized;
                    Assert.That(Vector3.Dot(-driver.transform.forward, direction), Is.GreaterThan(0.9999f));
                }
            }
            finally
            {
                target.transform.SetParent(null, true);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void PositiveZForward_FacesMarkerLocationRegardlessOfMarkerRotation()
        {
            SetField("usePositiveZForward", true);
            target.transform.position = new Vector3(-2, 1, 3);
            IEnumerator routine = Routine();
            routine.MoveNext();
            Vector3 direction = (target.transform.position - driver.transform.position).normalized;
            Assert.That(Vector3.Dot(driver.transform.forward, direction), Is.GreaterThan(0.9999f));
            Quaternion previous = driver.transform.rotation;
            target.transform.rotation = Quaternion.Euler(0, 180, 0);
            routine.MoveNext();
            Assert.That(Quaternion.Angle(previous, driver.transform.rotation), Is.LessThan(0.1f));
            target.transform.position = -target.transform.position;
            routine.MoveNext();
            Assert.That(Vector3.Dot(driver.transform.forward,
                (target.transform.position - driver.transform.position).normalized), Is.GreaterThan(0.9999f));
        }

        [TestCase(1, 0, 0)]
        [TestCase(0, 1, 0)]
        [TestCase(1, 1, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(0, 1, 1)]
        [TestCase(1, 1, 1)]
        public void PositiveZForward_TracksFullCircleWithAxisConstraints(int x, int y, int z)
        {
            SetField("usePositiveZForward", true);
            SetField("_zeroOutAxis", new Vector3(x, y, z));
            Vector3 axis = x == 1 ? Vector3.right : Vector3.up;
            IEnumerator routine = Routine();
            for (int angle = 0; angle <= 360; angle += 5)
            {
                Vector3 direction = original * Quaternion.AngleAxis(angle, axis) * Vector3.forward;
                target.transform.position = direction * 3;
                routine.MoveNext();
                Assert.That(Vector3.Dot(driver.transform.forward, direction), Is.GreaterThan(0.9999f), "Angle " + angle);
            }
        }

        [Test]
        public void AllLocked_PreservesStartingPose()
        {
            SetField("_zeroOutAxis", Vector3.zero);
            target.transform.position = Vector3.one;
            Routine().MoveNext();
            Assert.That(Quaternion.Angle(driver.transform.rotation, original), Is.LessThan(0.1f));
        }

        [Test]
        public void UndefinedDirection_RetainsLastRotation()
        {
            SetField("_zeroOutAxis", Vector3.right);
            Quaternion previous = original * Quaternion.AngleAxis(137, Vector3.right);
            driver.transform.rotation = previous;
            target.transform.position = driver.transform.position;
            IEnumerator routine = Routine();
            routine.MoveNext();
            Assert.That(Quaternion.Angle(driver.transform.rotation, previous), Is.LessThan(0.1f));
            target.transform.position = driver.transform.position + original * Vector3.right;
            routine.MoveNext();
            Assert.That(Quaternion.Angle(driver.transform.rotation, previous), Is.LessThan(0.1f));
        }

        [Test]
        public void SetupMotion_UsesOwnTransformAndCapturesNewStartingPose()
        {
            motion.TargetObject = null;
            driver.transform.rotation = Quaternion.Euler(52, -83, 12);
            motion.SetupMotion();
            Assert.That(motion.TargetObject, Is.EqualTo(driver.transform));
            SetField("_zeroOutAxis", Vector3.zero);
            SetField("isPaused", false);
            target.transform.position = Vector3.one;
            Quaternion expected = driver.transform.rotation;
            Routine().MoveNext();
            Assert.That(Quaternion.Angle(driver.transform.rotation, expected), Is.LessThan(0.1f));
        }

        private IEnumerator Routine()
        {
            return (IEnumerator)typeof(FP_LookAtLockAxis).GetMethod("MotionRoutine", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, null);
        }

        private void SetField(string name, object value)
        {
            typeof(FP_LookAtLockAxis).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(motion, value);
        }
    }
}
