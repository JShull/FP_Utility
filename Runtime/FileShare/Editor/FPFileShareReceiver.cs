// Copyright (c) 2026 John B. Shull. FuzzPhyte LLC.
// Public license: GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.FileShare.Editor
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class FPFileShareReceivedFile
    {
        public FPFileShareReceipt Receipt { get; }
        public string Path { get; }
        public FPFileShareReceivedFile(FPFileShareReceipt receipt, string path) { Receipt = receipt; Path = path; }
    }

    /// <summary>Single-request-at-a-time receiver. No Unity API is used on its worker thread.</summary>
    public sealed class FPFileShareReceiver : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly ConcurrentQueue<FPFileShareReceivedFile> received = new ConcurrentQueue<FPFileShareReceivedFile>();
        private readonly string inbox;
        private readonly string staging;
        private readonly FileStream inboxLease;
        private readonly long maximumBytes;
        private readonly int requestTimeoutSeconds;
        private volatile string status = "Listening";
        private int disposed;

        public string Endpoint { get; }
        public string PairingToken { get; }
        public string Status => status;
        public Task Completion { get; }
        public bool IsRunning => !Completion.IsCompleted && !stop.IsCancellationRequested;
        public bool TryDequeue(out FPFileShareReceivedFile file) => received.TryDequeue(out file);

        public FPFileShareReceiver(string address, int port, string inboxPath,
            long maximumBytes = FPFileShareProtocol.DefaultMaximumBytes, int requestTimeoutSeconds = 120)
        {
            if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
                || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast))
                throw new ArgumentException("Choose one explicit IPv4 interface address.");
            if (port < 1024 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            if (requestTimeoutSeconds < 1) throw new ArgumentOutOfRangeException(nameof(requestTimeoutSeconds));
            inbox = Path.GetFullPath(inboxPath);
            Directory.CreateDirectory(inbox);
            RejectLinkedDirectories(inbox);
            staging = Path.Combine(inbox, ".fp-fileshare-staging");
            Directory.CreateDirectory(staging);
            RejectLinkedDirectories(staging);
            this.maximumBytes = maximumBytes;
            this.requestTimeoutSeconds = requestTimeoutSeconds;
            Endpoint = $"http://{ip}:{port}/";
            PairingToken = FPFileShareProtocol.CreateToken();
            try
            {
                inboxLease = new FileStream(Path.Combine(staging, "receiver.lock"), FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, System.IO.FileShare.None);
                listener.Prefixes.Add(Endpoint);
                listener.Start();
                Completion = Task.Run(RunAsync);
            }
            catch
            {
                listener.Close();
                inboxLease?.Dispose();
                stop.Dispose();
                throw;
            }
        }

        private static void RejectLinkedDirectories(string path)
        {
            for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Choose an inbox without symbolic links or junctions.");
        }

        private async Task RunAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync().ConfigureAwait(false);
                    await ReceiveAsync(context).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                status = stop.IsCancellationRequested ? "Stopped" : "Receiver stopped: " + ex.Message;
            }
            finally
            {
                listener.Close();
                inboxLease.Dispose();
            }
        }

        private async Task ReceiveAsync(HttpListenerContext context)
        {
            string temporary = null;
            var request = context.Request;
            var response = context.Response;
            response.KeepAlive = false;
            response.ContentLength64 = 0;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(requestTimeoutSeconds));
                // Closing the stream also interrupts Mono implementations that ignore ReadAsync cancellation.
                using (timeout.Token.Register(() => { try { request.InputStream.Close(); response.Abort(); } catch { } }))
                try
                {
                    if (request.Headers[FPFileShareProtocol.TokenHeader] != PairingToken)
                    { response.StatusCode = 401; return; }
                    if (request.HttpMethod != "POST" || !string.IsNullOrEmpty(request.Url.Query)
                        || !request.Url.AbsolutePath.StartsWith(FPFileShareProtocol.Route, StringComparison.Ordinal)
                        || !Guid.TryParseExact(request.Url.AbsolutePath.Substring(FPFileShareProtocol.Route.Length), "N", out var id)
                        || id == Guid.Empty)
                    { response.StatusCode = 400; return; }
                    if (request.ContentLength64 < 0) { response.StatusCode = 411; return; }
                    if (request.ContentLength64 > maximumBytes) { response.StatusCode = 413; return; }
                    string hash = request.Headers[FPFileShareProtocol.HashHeader];
                    if (!FPFileShareProtocol.IsSha256(hash)) { response.StatusCode = 400; return; }
                    string name;
                    try { name = FPFileShareProtocol.DecodeFileName(request.Headers[FPFileShareProtocol.NameHeader]); }
                    catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
                    { response.StatusCode = 400; return; }

                    string destination = Path.Combine(inbox, id.ToString("N") + "_" + name);
                    temporary = Path.Combine(staging, Guid.NewGuid().ToString("N") + ".part");
                    long count = 0;
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, System.IO.FileShare.None))
                    {
                        var buffer = new byte[65536];
                        while (count < request.ContentLength64)
                        {
                            int read = await request.InputStream.ReadAsync(buffer, 0,
                                (int)Math.Min(buffer.Length, request.ContentLength64 - count), timeout.Token).ConfigureAwait(false);
                            if (read == 0) throw new IOException("Upload ended before the declared length.");
                            await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
                            count += read;
                            status = $"Receiving {name}: {count:N0} / {request.ContentLength64:N0} bytes";
                        }
                        output.Flush(true);
                    }
                    timeout.Token.ThrowIfCancellationRequested();
                    if (FPFileShareProtocol.HashFile(temporary, timeout.Token) != hash)
                    { status = "Rejected upload: checksum mismatch."; response.StatusCode = 422; return; }

                    // Persisted file names carry the ID, so retries remain idempotent across receiver restarts.
                    var prior = Directory.GetFiles(inbox, id.ToString("N") + "_*");
                    bool duplicate = prior.Length != 0;
                    if (duplicate && (prior.Length != 1 || !string.Equals(Path.GetFileName(prior[0]), Path.GetFileName(destination), StringComparison.Ordinal)
                        || (File.GetAttributes(prior[0]) & FileAttributes.ReparsePoint) != 0
                        || new FileInfo(prior[0]).Length != count || FPFileShareProtocol.HashFile(prior[0], timeout.Token) != hash))
                    { status = "Rejected upload: transfer ID already belongs to a different file."; response.StatusCode = 409; return; }
                    timeout.Token.ThrowIfCancellationRequested();
                    if (!duplicate) File.Move(temporary, destination); // Same-volume commit; never overwrite.
                    var receipt = new FPFileShareReceipt(id, name, count, hash, duplicate);
                    response.StatusCode = duplicate ? 200 : 201;
                    response.Headers[FPFileShareProtocol.IdHeader] = id.ToString("N");
                    response.Headers[FPFileShareProtocol.NameHeader] = FPFileShareProtocol.EncodeFileName(name);
                    response.Headers[FPFileShareProtocol.HashHeader] = hash;
                    response.Headers[FPFileShareProtocol.LengthHeader] = count.ToString(CultureInfo.InvariantCulture);
                    received.Enqueue(new FPFileShareReceivedFile(receipt, destination));
                    // Bound retained notifications when the UI is not polling.
                    while (received.Count > 100) received.TryDequeue(out _);
                    status = duplicate ? $"Already received: {name}" : $"Saved: {name}";
                }
                catch (Exception ex)
                {
                    status = timeout.IsCancellationRequested ? "Upload cancelled or timed out." : "Upload failed: " + ex.Message;
                    try { response.StatusCode = 500; } catch { }
                }
                finally
                {
                    if (temporary != null)
                    {
                        try { if (File.Exists(temporary)) File.Delete(temporary); }
                        catch (IOException) { status = "Could not remove a partial file from the staging folder."; }
                    }
                    try { response.Close(); } catch { }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            stop.Cancel();
            listener.Close();
            // Worker continuations do not use Unity's synchronization context.
            if (Completion.Wait(TimeSpan.FromSeconds(2))) stop.Dispose();
            status = "Stopped";
        }
    }
}
