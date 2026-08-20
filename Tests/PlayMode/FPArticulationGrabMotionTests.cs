// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Tests
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class FPArticulationGrabMotionTests
    {
        [UnityTest]
        public IEnumerator BeginAndReleaseGrab_CreatesJointAndBroadcastsLifecycle()
        {
            GameObject articulationObject = new GameObject("Articulation");
            ArticulationBody articulation =
                articulationObject.AddComponent<ArticulationBody>();
            GameObject gripObject = new GameObject("Grip");
            gripObject.transform.SetParent(articulationObject.transform, false);
            GameObject handObject = new GameObject("Hand Target");
            GameObject driverObject = new GameObject("Grab Driver");
            FP_ArticulationGrabMotion driver =
                driverObject.AddComponent<FP_ArticulationGrabMotion>();

            int connectedCount = 0;
            int releasedCount = 0;
            driver.GrabConnected += _ => connectedCount++;
            driver.GrabReleased += _ => releasedCount++;

            try
            {
                driver.Configure(articulation, gripObject.transform);

                bool connected = driver.TryBeginGrab(handObject.transform);

                Assert.That(connected, Is.True);
                Assert.That(driver.IsConnected, Is.True);
                Assert.That(driver.State, Is.EqualTo(FPArticulationGrabState.Connected));
                Assert.That(driver.PhysicsAnchor, Is.Not.Null);
                Assert.That(driver.PhysicsAnchor.isKinematic, Is.True);
                Assert.That(driver.ActiveJoint, Is.Not.Null);
                Assert.That(
                    driver.ActiveJoint.connectedArticulationBody,
                    Is.SameAs(articulation));
                Assert.That(connectedCount, Is.EqualTo(1));

                driver.ReleaseGrab();

                Assert.That(driver.IsConnected, Is.False);
                Assert.That(driver.ActiveJoint, Is.Null);
                Assert.That(driver.State, Is.EqualTo(FPArticulationGrabState.Disconnected));
                Assert.That(releasedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(driverObject);
                Object.Destroy(handObject);
                Object.Destroy(articulationObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FollowTargetMovement_MovesTheKinematicAnchor()
        {
            GameObject articulationObject = new GameObject("Articulation");
            ArticulationBody articulation =
                articulationObject.AddComponent<ArticulationBody>();
            GameObject gripObject = new GameObject("Grip");
            gripObject.transform.SetParent(articulationObject.transform, false);
            GameObject handObject = new GameObject("Hand Target");
            GameObject driverObject = new GameObject("Grab Driver");
            FP_ArticulationGrabMotion driver =
                driverObject.AddComponent<FP_ArticulationGrabMotion>();

            try
            {
                driver.Configure(
                    articulation,
                    gripObject.transform,
                    handObject.transform);
                Assert.That(driver.TryBeginGrab(handObject.transform), Is.True);
                Vector3 startingAnchorPosition = driver.PhysicsAnchor.position;

                handObject.transform.position += Vector3.right * 0.25f;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Assert.That(
                    driver.PhysicsAnchor.position.x,
                    Is.EqualTo(startingAnchorPosition.x + 0.25f).Within(0.01f));
            }
            finally
            {
                Object.Destroy(driverObject);
                Object.Destroy(handObject);
                Object.Destroy(articulationObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BeginGrab_WithStartupSnap_AlignsGripBeforeConnecting()
        {
            GameObject articulationObject = new GameObject("Articulation");
            ArticulationBody articulation =
                articulationObject.AddComponent<ArticulationBody>();
            articulationObject.transform.SetPositionAndRotation(
                new Vector3(-1f, 0.5f, 2f),
                Quaternion.Euler(0f, 15f, 0f));

            GameObject gripObject = new GameObject("Grip");
            gripObject.transform.SetParent(articulationObject.transform, false);
            gripObject.transform.localPosition = new Vector3(0.25f, 1.25f, -0.1f);

            GameObject handObject = new GameObject("Hand Target");
            handObject.transform.SetPositionAndRotation(
                new Vector3(2f, 3f, -1f),
                Quaternion.Euler(0f, 90f, 0f));

            GameObject driverObject = new GameObject("Grab Driver");
            FP_ArticulationGrabMotion driver =
                driverObject.AddComponent<FP_ArticulationGrabMotion>();

            try
            {
                driver.Configure(
                    articulation,
                    gripObject.transform,
                    handObject.transform);
                driver.SetSnapGrabPointToFollowTargetOnStart(true);

                Assert.That(driver.TryBeginGrab(), Is.True);

                Assert.That(driver.SnapGrabPointToFollowTargetOnStart, Is.True);
                Assert.That(
                    Vector3.Distance(gripObject.transform.position, handObject.transform.position),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(gripObject.transform.rotation, handObject.transform.rotation),
                    Is.LessThan(0.01f));
                Assert.That(
                    Vector3.Distance(driver.PhysicsAnchor.position, handObject.transform.position),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.Destroy(driverObject);
                Object.Destroy(handObject);
                Object.Destroy(articulationObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BeginGrab_WithoutArticulation_BroadcastsFailure()
        {
            GameObject gripObject = new GameObject("Grip");
            GameObject handObject = new GameObject("Hand Target");
            GameObject driverObject = new GameObject("Grab Driver");
            FP_ArticulationGrabMotion driver =
                driverObject.AddComponent<FP_ArticulationGrabMotion>();
            int failureCount = 0;
            driver.GrabFailed += (_, __) => failureCount++;

            try
            {
                driver.Configure(null, gripObject.transform, handObject.transform);

                LogAssert.Expect(
                    LogType.Error,
                    "[FP Articulation Grab] An ArticulationBody must be assigned before beginning an articulation grab.");
                bool connected = driver.TryBeginGrab(handObject.transform);

                Assert.That(connected, Is.False);
                Assert.That(driver.State, Is.EqualTo(FPArticulationGrabState.InvalidConfiguration));
                Assert.That(failureCount, Is.EqualTo(1));
                Assert.That(driver.LastFailureMessage, Does.Contain("ArticulationBody"));
            }
            finally
            {
                Object.Destroy(driverObject);
                Object.Destroy(handObject);
                Object.Destroy(gripObject);
            }

            yield return null;
        }
    }
}
