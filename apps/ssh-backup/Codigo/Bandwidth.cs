using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SSHBackup
{
    public sealed class BandwidthLimiter
    {
        readonly object sync = new object();
        readonly double bytesPerSecond;
        double nextTick;
        public long BytesPerSecond { get { return (long)bytesPerSecond; } }
        public BandwidthLimiter(long rate)
        {
            if (rate <= 0) throw new ArgumentOutOfRangeException("rate");
            bytesPerSecond = rate;
        }
        public static long Rate(decimal value, string unit)
        {
            if (value < 0.1m || value > 100000m || (unit != "Mbps" && unit != "MB/s")) throw new ArgumentException("El límite debe estar entre 0,1 y 100.000 Mbps o MB/s.");
            return checked((long)(value * (unit == "Mbps" ? 125000m : 1000000m)));
        }
        public async Task WaitAsync(int bytes, CancellationToken token)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException("bytes");
            token.ThrowIfCancellationRequested();
            double end;
            lock (sync)
            {
                double now = Stopwatch.GetTimestamp();
                nextTick = Math.Max(now, nextTick) + bytes * (double)Stopwatch.Frequency / bytesPerSecond;
                end = nextTick;
            }
            while (true)
            {
                token.ThrowIfCancellationRequested();
                double remaining = (end - Stopwatch.GetTimestamp()) * 1000 / Stopwatch.Frequency;
                if (remaining <= 0) return;
                await Task.Delay((int)Math.Min(1000, Math.Max(1, Math.Ceiling(remaining))), token).ConfigureAwait(false);
            }
        }
    }

    // All SSH clients in a run share this loopback SOCKS5 transport. Throttling the
    // encrypted receive stream also accounts for SSH.NET's speculative SFTP reads.
    public sealed class LimitedSshTransport : IDisposable
    {
        readonly TcpListener listener;
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly ConcurrentDictionary<TcpClient, byte> clients = new ConcurrentDictionary<TcpClient, byte>();
        readonly HashSet<string> allowed;
        readonly BandwidthLimiter limiter;
        readonly Task accepting;
        public int Port { get; private set; }
        long delivered;
        public long DeliveredBytes { get { return Interlocked.Read(ref delivered); } }
        public LimitedSshTransport(IEnumerable<ServerConfig> servers, long bytesPerSecond)
        {
            limiter = new BandwidthLimiter(bytesPerSecond);
            allowed = new HashSet<string>(servers.Select(s => Key(s.Host, s.Port)), StringComparer.OrdinalIgnoreCase);
            listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            accepting = AcceptAsync();
            Observe(accepting);
        }
        static void Observe(Task task)
        { task.ContinueWith(t => { var error = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); }
        static string Key(string host, int port) { return host.Trim('[', ']').ToLowerInvariant() + "|" + port; }
        async Task AcceptAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { if (stop.IsCancellationRequested) return; throw; }
                clients.TryAdd(client, 0);
                // Each connection contains only encrypted SSH data, never SSH credentials.
                Observe(HandleAsync(client));
            }
        }
        static async Task<byte[]> Exact(Stream stream, int count, CancellationToken token)
        {
            var bytes = new byte[count]; int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(bytes, offset, count - offset, token).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException(); offset += read;
            }
            return bytes;
        }
        async Task Pump(Stream source, Stream destination, bool limited, CancellationToken token)
        {
            int chunk = limited ? (int)Math.Max(256, Math.Min(32768, limiter.BytesPerSecond / 10)) : 32768;
            var buffer = new byte[chunk]; int read;
            while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
            {
                if (limited) await limiter.WaitAsync(read, token).ConfigureAwait(false);
                await destination.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                if (limited) Interlocked.Add(ref delivered, read);
            }
        }
        async Task HandleAsync(TcpClient client)
        {
            TcpClient remote = null;
            try
            {
                client.NoDelay = true;
                using (var stream = client.GetStream())
                {
                    var greeting = await Exact(stream, 2, stop.Token).ConfigureAwait(false);
                    if (greeting[0] != 5 || greeting[1] == 0) return;
                    var methods = await Exact(stream, greeting[1], stop.Token).ConfigureAwait(false);
                    if (!methods.Contains((byte)0)) return;
                    await stream.WriteAsync(new byte[] { 5, 0 }, 0, 2, stop.Token).ConfigureAwait(false);
                    var request = await Exact(stream, 4, stop.Token).ConfigureAwait(false);
                    if (request[0] != 5 || request[1] != 1) return;
                    string host;
                    if (request[3] == 1) host = new IPAddress(await Exact(stream, 4, stop.Token).ConfigureAwait(false)).ToString();
                    else if (request[3] == 4) host = new IPAddress(await Exact(stream, 16, stop.Token).ConfigureAwait(false)).ToString();
                    else if (request[3] == 3)
                    {
                        int length = (await Exact(stream, 1, stop.Token).ConfigureAwait(false))[0];
                        if (length == 0) return;
                        host = Encoding.ASCII.GetString(await Exact(stream, length, stop.Token).ConfigureAwait(false));
                    }
                    else return;
                    var ports = await Exact(stream, 2, stop.Token).ConfigureAwait(false);
                    int port = ports[0] * 256 + ports[1];
                    if (!allowed.Contains(Key(host, port))) return;
                    remote = new TcpClient { NoDelay = true, ReceiveBufferSize = 32768 };
                    clients.TryAdd(remote, 0);
                    await remote.ConnectAsync(host, port).ConfigureAwait(false);
                    var reply = new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0, 0 };
                    await stream.WriteAsync(reply, 0, reply.Length, stop.Token).ConfigureAwait(false);
                    using (var wire = remote.GetStream())
                    {
                        var inbound = Pump(wire, stream, true, stop.Token);
                        var outbound = Pump(stream, wire, false, stop.Token);
                        await Task.WhenAny(inbound, outbound).ConfigureAwait(false);
                        client.Close(); remote.Close();
                        try { await Task.WhenAll(inbound, outbound).ConfigureAwait(false); } catch (Exception) { }
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is OperationCanceledException || e is ObjectDisposedException || e is ArgumentException) { }
            finally
            {
                byte ignored; clients.TryRemove(client, out ignored); client.Close();
                if (remote != null) { clients.TryRemove(remote, out ignored); remote.Close(); }
            }
        }
        public void Dispose()
        {
            stop.Cancel(); listener.Stop();
            foreach (var client in clients.Keys) try { client.Close(); } catch (ObjectDisposedException) { }
            // Active operations still observe this token while their sockets finish closing.
        }
    }
}
