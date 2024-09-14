﻿using Rtsp.Messages;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Rtsp
{
    public class UDPSocket : IRtpTransport
    {
        protected readonly UdpClient dataSocket;
        protected readonly UdpClient controlSocket;

        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private Task? _dataReadTask;
        private Task? _controlReadTask;
        private IPEndPoint? _dataEndPoint;
        private IPEndPoint? _controlEndPoint;

        private bool disposedValue;

        public int DataPort { get; protected set; }
        public int ControlPort { get; protected set; }

        public PortCouple Ports => new(DataPort, ControlPort);

        /// <summary>
        /// Initializes a new instance of the <see cref="UDPSocket"/> class.
        /// Creates two new UDP sockets using the start and end Port range
        /// </summary>
        public UDPSocket(int startPort, int endPort)
        {
            // open a pair of UDP sockets - one for data (video or audio) and one for the status channel (RTCP messages)
            DataPort = startPort;
            ControlPort = startPort + 1;

            bool ok = false;
            while (!ok && (ControlPort < endPort))
            {
                // Video/Audio port must be odd and command even (next one)
                try
                {
                    dataSocket = new UdpClient(DataPort);
                    controlSocket = new UdpClient(ControlPort);
                    ok = true;
                }
                catch (SocketException)
                {
                    // Fail to allocate port, try again
                    dataSocket?.Close();
                    controlSocket?.Close();

                    // try next data or control port
                    DataPort += 2;
                    ControlPort += 2;
                }

                if (ok)
                {
                    dataSocket!.Client.ReceiveBufferSize = 100 * 1024;
                    dataSocket!.Client.SendBufferSize = 65535; // default is 8192. Make it as large as possible for large RTP packets which are not fragmented
                    controlSocket!.Client.DontFragment = false;
                }
            }

            if (dataSocket == null || controlSocket == null)
            {
                throw new InvalidOperationException("UDP socket was not initialized (can't find free UDP port), can't continue");
            }
        }

        protected UDPSocket(UdpClient dataSocket, UdpClient controlSocket)
        {
            this.dataSocket = dataSocket;
            this.controlSocket = controlSocket;
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            if (_dataReadTask != null)
            {
                throw new InvalidOperationException("Forwarder was stopped, can't restart it");
            }

            _dataReadTask = Task.Factory.StartNew(async () =>
                await DoWorkerJobAsync(dataSocket, OnDataReceived, DataPort, _cancellationTokenSource.Token).ConfigureAwait(false),
                TaskCreationOptions.LongRunning);
            _controlReadTask = Task.Factory.StartNew(async () =>
                await DoWorkerJobAsync(controlSocket, OnControlReceived, ControlPort, _cancellationTokenSource.Token).ConfigureAwait(false),
                TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public virtual void Stop()
        {
            _cancellationTokenSource.Cancel();
            dataSocket.Close();
            controlSocket.Close();
        }

        /// <summary>
        /// Occurs when data is received.
        /// </summary>
        public event EventHandler<RtspDataEventArgs>? DataReceived;

        /// <summary>
        /// Raises the <see cref="E:DataReceived"/> event.
        /// </summary>
        protected void OnDataReceived(RtspDataEventArgs rtspDataEventArgs)
        {
            DataReceived?.Invoke(this, rtspDataEventArgs);
        }

        /// <summary>
        /// Occurs when control is received.
        /// </summary>
        public event EventHandler<RtspDataEventArgs>? ControlReceived;

        /// <summary>
        /// Raises the <see cref="E:ControlReceived"/> event.
        /// </summary>
        protected void OnControlReceived(RtspDataEventArgs rtspDataEventArgs)
        {
            ControlReceived?.Invoke(this, rtspDataEventArgs);
        }

        /// <summary>
        /// Does the video job.
        /// </summary>
        private static async Task DoWorkerJobAsync(UdpClient client, Action<RtspDataEventArgs> handler, int port, CancellationToken cancellation)
        {
            try
            {
                // to be compatible with netstandard2.0 we can't use the memory directly for the receive call 
                byte[] buffer = new byte[65536];
                // loop until we get an exception eg the socket closed or the cancellation token is set
                while (!cancellation.IsCancellationRequested)
                {
#if NET7_0_OR_GREATER
                    var size = await client.Client.ReceiveAsync(buffer, cancellation).ConfigureAwait(false);
#else
                    // Task to prevent warning and keep the same code than .NET 8
                    var size = await Task.FromResult(client.Client.Receive(buffer)).ConfigureAwait(false);
#endif
                    var bufferOwner = MemoryPool<byte>.Shared.Rent(size);
                    buffer.AsSpan()[..size].CopyTo(bufferOwner.Memory.Span);

                    handler(new RtspDataEventArgs(new RtspData(bufferOwner, size)
                    {
                        Channel = port,
                    }));
                }
            }
            catch(OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        public void SetDataDestination(string hostname, int port)
        {
            var adresses = Dns.GetHostAddresses(hostname);
            if (adresses.Length == 0)
            {
                throw new ArgumentException("No IP address found for the hostname", nameof(hostname));
            }
            _dataEndPoint = new IPEndPoint(adresses[0], port);
        }

        public void SetControlDestination(string hostname, int port)
        {
            var adresses = Dns.GetHostAddresses(hostname);
            if (adresses.Length == 0)
            {
                throw new ArgumentException("No IP address found for the hostname", nameof(hostname));
            }
            _controlEndPoint = new IPEndPoint(adresses[0], port);
        }

        public void WriteToControlPort(ReadOnlySpan<byte> data) => controlSocket.Send(data, _controlEndPoint);

        public Task WriteToControlPortAsync(ReadOnlyMemory<byte> data)
            => controlSocket.SendAsync(data, _controlEndPoint, _cancellationTokenSource.Token).AsTask();

        public void WriteToDataPort(ReadOnlySpan<byte> data) => dataSocket.Send(data, _dataEndPoint);

        public Task WriteToDataPortAsync(ReadOnlyMemory<byte> data)
            => dataSocket.SendAsync(data, _dataEndPoint, _cancellationTokenSource.Token).AsTask();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Ne changez pas ce code. Placez le code de nettoyage dans la méthode 'Dispose(bool disposing)'
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
