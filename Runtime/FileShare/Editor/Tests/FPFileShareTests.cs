// Copyright (c) 2026 John B. Shull. FuzzPhyte LLC.
// Public license: GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.FileShare.Editor.Tests
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    public class FPFileShareTests
    {
        private string root;
        private string source;
        private string inbox;
        private FPFileShareReceiver receiver;
        private int port;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "FPFileShareTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            inbox = Path.Combine(root, "Inbox");
            source = Path.Combine(root, "capture.json");
            File.WriteAllText(source, "{\"RecordingName\":\"Test\",\"FaceSnapshots\":[]}", new UTF8Encoding(false));
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            receiver = new FPFileShareReceiver("127.0.0.1", port, inbox, 8 * 1024 * 1024, 5);
        }

        [TearDown]
        public void TearDown()
        {
            receiver?.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static IEnumerator Wait(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Transfer timed out in the test harness.");
        }

        private Task<FPFileShareReceipt> Send(Guid id, string token = null, CancellationToken cancellation = default)
            => FPFileShareSender.SendAsync(source, receiver.Endpoint, token ?? receiver.PairingToken, id,
                cancellationToken: cancellation, timeoutSeconds: 8);

        [UnityTest]
        public IEnumerator RuntimeSender_ReceivesIdenticalBytes_AndRetrySurvivesReceiverRestart()
        {
            var id = Guid.NewGuid();
            var send = Send(id);
            yield return Wait(send);
            Assert.That(send.Status, Is.EqualTo(TaskStatus.RanToCompletion), send.Exception?.ToString());
            Assert.That(send.Result.AlreadyReceived, Is.False);
            Assert.That(receiver.TryDequeue(out var received), Is.True);
            CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(received.Path));
            Assert.That(File.Exists(source), Is.True);
            receiver.Dispose();
            receiver = new FPFileShareReceiver("127.0.0.1", port, inbox);
            var retry = Send(id);
            yield return Wait(retry);
            Assert.That(retry.Status, Is.EqualTo(TaskStatus.RanToCompletion), retry.Exception?.ToString());
            Assert.That(retry.Result.AlreadyReceived, Is.True);
            Assert.That(Directory.GetFiles(inbox).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RuntimeSender_MultiMegabyteFile_PreservesContent()
        {
            var data = new byte[5 * 1024 * 1024];
            new Random(42).NextBytes(data);
            File.WriteAllBytes(source, data);
            var send = Send(Guid.NewGuid());
            yield return Wait(send);
            Assert.That(send.Status, Is.EqualTo(TaskStatus.RanToCompletion), send.Exception?.ToString());
            Assert.That(receiver.TryDequeue(out var received), Is.True);
            CollectionAssert.AreEqual(data, File.ReadAllBytes(received.Path));
        }

        [UnityTest]
        public IEnumerator RuntimeSender_WrongToken_DoesNotSave()
        {
            var send = Send(Guid.NewGuid(), "wrong-token");
            yield return Wait(send);
            Assert.That(send.IsFaulted, Is.True);
            Assert.That(Directory.GetFiles(inbox), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RuntimeSender_PreCancelled_DoesNotSaveOrDeleteSource()
        {
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                var send = Send(Guid.NewGuid(), cancellation: cancel.Token);
                yield return Wait(send);
                Assert.That(send.IsCanceled, Is.True);
                Assert.That(File.Exists(source), Is.True);
                Assert.That(Directory.GetFiles(inbox), Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator SameIdDifferentContents_RejectsWithoutOverwriting()
        {
            var id = Guid.NewGuid();
            var first = Send(id);
            yield return Wait(first);
            Assert.That(first.IsFaulted, Is.False, first.Exception?.ToString());
            Assert.That(receiver.TryDequeue(out var received), Is.True);
            string original = File.ReadAllText(received.Path);
            File.WriteAllText(source, "different");
            var second = Send(id);
            yield return Wait(second);
            Assert.That(second.IsFaulted, Is.True);
            Assert.That(File.ReadAllText(received.Path), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(inbox, "*.part", SearchOption.AllDirectories), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RuntimeSender_CancelWhileAwaitingReceipt_RetainsSource()
        {
            receiver.Dispose();
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using (var cancel = new CancellationTokenSource())
            {
                TcpClient client = null;
                try
                {
                    var accept = listener.AcceptTcpClientAsync();
                    var send = Send(Guid.NewGuid(), cancellation: cancel.Token);
                    yield return Wait(accept);
                    client = accept.Result;
                    // Receiving request bytes proves the sender reached UnityWebRequest, beyond hashing.
                    var read = client.GetStream().ReadAsync(new byte[1], 0, 1);
                    yield return Wait(read);
                    Assert.That(read.Result, Is.EqualTo(1));
                    cancel.Cancel();
                    yield return Wait(send);
                    Assert.That(send.IsCanceled, Is.True);
                    Assert.That(File.Exists(source), Is.True);
                }
                finally { client?.Dispose(); listener.Stop(); }
            }
        }

        [UnityTest]
        public IEnumerator RuntimeSender_RejectsSuccessWithoutReceipt()
        {
            receiver.Dispose();
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            TcpClient client = null;
            try
            {
                var accept = listener.AcceptTcpClientAsync();
                var send = Send(Guid.NewGuid());
                yield return Wait(accept);
                client = accept.Result;
                var read = client.GetStream().ReadAsync(new byte[4096], 0, 4096);
                yield return Wait(read);
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 201 Created\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                client.GetStream().Write(response, 0, response.Length);
                yield return Wait(send);
                Assert.That(send.IsFaulted, Is.True);
                Assert.That(send.Exception.ToString(), Does.Contain("matching saved-file receipt"));
                Assert.That(File.Exists(source), Is.True);
            }
            finally { client?.Dispose(); listener.Stop(); }
        }

        private Task<int> RawUpload(string name, string hash, long? declaredLength = null)
        {
            string endpoint = receiver.Endpoint;
            string token = receiver.PairingToken;
            return Task.Run(() =>
            {
                var request = (HttpWebRequest)WebRequest.Create(endpoint.TrimEnd('/') + FPFileShareProtocol.Route + Guid.NewGuid().ToString("N"));
                request.Proxy = null;
                request.Method = "POST";
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;
                request.KeepAlive = false;
                request.Headers[FPFileShareProtocol.TokenHeader] = token;
                request.Headers[FPFileShareProtocol.NameHeader] = Convert.ToBase64String(Encoding.UTF8.GetBytes(name));
                request.Headers[FPFileShareProtocol.HashHeader] = hash;
                var bytes = File.ReadAllBytes(source);
                request.ContentLength = declaredLength ?? bytes.Length;
                try
                {
                    using (var body = request.GetRequestStream()) body.Write(bytes, 0, bytes.Length);
                    using (var response = (HttpWebResponse)request.GetResponse()) return (int)response.StatusCode;
                }
                catch (WebException ex) when (ex.Response is HttpWebResponse)
                {
                    using (var response = (HttpWebResponse)ex.Response) return (int)response.StatusCode;
                }
            });
        }

        [UnityTest]
        public IEnumerator TraversalName_IsRejected()
        {
            var upload = RawUpload("../escape.json", FPFileShareProtocol.HashFile(source));
            yield return Wait(upload);
            Assert.That(upload.Status, Is.EqualTo(TaskStatus.RanToCompletion), upload.Exception?.ToString());
            Assert.That(upload.Result, Is.EqualTo(400));
            Assert.That(File.Exists(Path.Combine(root, "escape.json")), Is.False);
            Assert.That(Directory.GetFiles(inbox), Is.Empty);
        }

        [UnityTest]
        public IEnumerator BadChecksum_IsRejectedAndPartialRemoved()
        {
            var upload = RawUpload("capture.json", new string('0', 64));
            yield return Wait(upload);
            Assert.That(upload.Status, Is.EqualTo(TaskStatus.RanToCompletion), upload.Exception?.ToString());
            Assert.That(upload.Result, Is.EqualTo(422));
            Assert.That(Directory.GetFiles(inbox), Is.Empty);
            Assert.That(Directory.GetFiles(inbox, "*.part", SearchOption.AllDirectories), Is.Empty);
        }

        [UnityTest]
        public IEnumerator SizeLimit_IsEnforced()
        {
            receiver.Dispose();
            receiver = new FPFileShareReceiver("127.0.0.1", port, inbox, 1);
            var upload = Send(Guid.NewGuid());
            yield return Wait(upload);
            Assert.That(upload.IsFaulted, Is.True);
            Assert.That(upload.Exception.ToString(), Does.Contain("413"));
            Assert.That(Directory.GetFiles(inbox), Is.Empty);
        }

        [UnityTest]
        public IEnumerator StopDuringPartialUpload_RemovesStagingAndReleasesPort()
        {
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, port);
                string header = "POST " + FPFileShareProtocol.Route + Guid.NewGuid().ToString("N") + " HTTP/1.1\r\n"
                    + "Host: 127.0.0.1:" + port + "\r\nContent-Length: 100000\r\n"
                    + FPFileShareProtocol.TokenHeader + ": " + receiver.PairingToken + "\r\n"
                    + FPFileShareProtocol.NameHeader + ": " + FPFileShareProtocol.EncodeFileName("partial.json") + "\r\n"
                    + FPFileShareProtocol.HashHeader + ": " + new string('0', 64) + "\r\n\r\npartial";
                byte[] bytes = Encoding.ASCII.GetBytes(header);
                client.GetStream().Write(bytes, 0, bytes.Length);
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (Directory.GetFiles(inbox, "*.part", SearchOption.AllDirectories).Length == 0 && DateTime.UtcNow < deadline) yield return null;
                Assert.That(Directory.GetFiles(inbox, "*.part", SearchOption.AllDirectories).Length, Is.EqualTo(1));
                receiver.Dispose();
                yield return Wait(receiver.Completion);
            }
            Assert.That(Directory.GetFiles(inbox), Is.Empty);
            Assert.That(Directory.GetFiles(inbox, "*.part", SearchOption.AllDirectories), Is.Empty);
            receiver = new FPFileShareReceiver("127.0.0.1", port, inbox);
            Assert.That(receiver.IsRunning, Is.True);
        }

        [TestCase("a/b.json")]
        [TestCase("a\\b.json")]
        [TestCase("C:escape.json")]
        [TestCase("file.json.")]
        [TestCase("\r\nheader")]
        public void InvalidNames_AreRejected(string name)
            => Assert.Throws<ArgumentException>(() => FPFileShareProtocol.ValidateFileName(name));
    }
}
