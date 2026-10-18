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
