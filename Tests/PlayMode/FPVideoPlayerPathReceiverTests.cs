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
    using System.IO;
    using System.Reflection;
    using FuzzPhyte.Utility.Video;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.TestTools;
    using UnityEngine.Video;

    public sealed class FPVideoPlayerPathReceiverTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic =
            BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void HasReachedEnd_DetectsCompletedNonLoopingPlayback()
        {
            MethodInfo method = typeof(FPVideoPlayerPathReceiver).GetMethod(
                "HasReachedEnd",
                PrivateStatic,
                null,
                new[]
                {
                    typeof(bool),
                    typeof(long),
                    typeof(ulong),
                    typeof(double),
                    typeof(double)
                },
                null);
            Assert.That(method, Is.Not.Null);

            bool lastFrameReached = (bool)method.Invoke(
                null,
                new object[] { false, 1249L, 1250UL, 41.0d, 41.8d });
            bool endTimeReached = (bool)method.Invoke(
                null,
                new object[] { false, 1000L, 1250UL, 41.78d, 41.8d });
            bool loopingAtEnd = (bool)method.Invoke(
                null,
                new object[] { true, 1249L, 1250UL, 41.8d, 41.8d });
            bool stillPlaying = (bool)method.Invoke(
                null,
                new object[] { false, 500L, 1250UL, 15d, 41.8d });

            Assert.That(lastFrameReached, Is.True);
            Assert.That(endTimeReached, Is.True);
            Assert.That(loopingAtEnd, Is.False);
            Assert.That(stillPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator SetVideoPath_AssignsProvidedLocalPathWithoutAddingAFileScheme()
        {
            CreateReceiver(
                out GameObject receiverObject,
                out VideoPlayer videoPlayer,
                out FPVideoPlayerPathReceiver receiver);

            try
            {
                string localPath = Path.Combine(
                    Application.temporaryCachePath,
                    "FPVideoPlayerPathReceiverTests",
                    "test.mp4");
                SetField(receiver, "prepareOnSet", false);

                receiver.SetVideoPath(localPath);

                Assert.That(videoPlayer.source, Is.EqualTo(VideoSource.Url));
                Assert.That(videoPlayer.url, Is.EqualTo(localPath));
                Assert.That(receiver.LastAssignedPath, Is.EqualTo(localPath));
            }
            finally
            {
                Object.Destroy(receiverObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SetVideoPath_WithEmptyPath_ClearsUrlAndBroadcastsFailure()
        {
            CreateReceiver(
                out GameObject receiverObject,
                out VideoPlayer videoPlayer,
                out FPVideoPlayerPathReceiver receiver);

            try
            {
                string failureMessage = null;
                UnityEvent<string> failureEvent =
                    GetField<UnityEvent<string>>(receiver, "onPathAssignmentFailed");
                failureEvent.AddListener(message => failureMessage = message);
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = Path.Combine(
                    Application.temporaryCachePath,
                    "FPVideoPlayerPathReceiverTests",
                    "previous.mp4");

                LogAssert.Expect(
                    LogType.Warning,
                    "[FPVideoPlayerPathReceiver] Resolved video path was empty.");

                receiver.SetVideoPath(" ");

                Assert.That(videoPlayer.url, Is.Empty);
                Assert.That(receiver.LastAssignedPath, Is.Empty);
                Assert.That(failureMessage, Is.EqualTo("Resolved video path was empty."));
            }
            finally
            {
                Object.Destroy(receiverObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlaybackCallbacks_BroadcastStartedOnlyAfterUnityStartedCallback()
        {
            CreateReceiver(
                out GameObject receiverObject,
                out VideoPlayer videoPlayer,
                out FPVideoPlayerPathReceiver receiver);

            try
            {
                int preparedCount = 0;
                int startedCount = 0;
                GetField<UnityEvent>(receiver, "onPrepared")
                    .AddListener(() => preparedCount++);
                GetField<UnityEvent>(receiver, "onStartedPlaying")
                    .AddListener(() => startedCount++);

                InvokeCallback(receiver, "HandlePrepareCompleted", videoPlayer);

                Assert.That(preparedCount, Is.EqualTo(1));
                Assert.That(startedCount, Is.Zero);

                InvokeCallback(receiver, "HandleStarted", videoPlayer);

                Assert.That(startedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(receiverObject);
            }

            yield return null;
        }

        private static void CreateReceiver(
            out GameObject receiverObject,
            out VideoPlayer videoPlayer,
            out FPVideoPlayerPathReceiver receiver)
        {
            receiverObject = new GameObject("FP Video Player Path Receiver Test");
            receiverObject.SetActive(false);
            videoPlayer = receiverObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            receiver = receiverObject.AddComponent<FPVideoPlayerPathReceiver>();
            SetField(receiver, "targetVideoPlayer", videoPlayer);
            receiverObject.SetActive(true);
        }

        private static T GetField<T>(
            FPVideoPlayerPathReceiver receiver,
            string fieldName)
        {
            FieldInfo field = typeof(FPVideoPlayerPathReceiver).GetField(
                fieldName,
                PrivateInstance);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            return (T)field.GetValue(receiver);
        }

        private static void SetField<T>(
            FPVideoPlayerPathReceiver receiver,
            string fieldName,
            T value)
        {
            FieldInfo field = typeof(FPVideoPlayerPathReceiver).GetField(
                fieldName,
                PrivateInstance);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(receiver, value);
        }

        private static void InvokeCallback(
            FPVideoPlayerPathReceiver receiver,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = typeof(FPVideoPlayerPathReceiver).GetMethod(
                methodName,
                PrivateInstance);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
            method.Invoke(receiver, arguments);
        }
    }
}
