﻿namespace RtspMulticaster
{
    using Rtsp;
    using Rtsp.Messages;
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    public class TCPtoUDPForwader : Forwarder
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private Thread _forwardCThread;

        public TCPtoUDPForwader() : base()
        {
            ForwardInterleavedCommand = -1;
        }



        /// <summary>
        /// Gets or sets the forward command port.
        /// </summary>
        /// <value>The forward command.</value>
        public RtspListener ForwardCommand { get; set; }
        /// <summary>
        /// Gets or sets the source interleaved video.
        /// </summary>
        /// <value>The source interleaved video.</value>
        public int SourceInterleavedVideo { get; set; }
        /// <summary>
        /// Gets or sets the forward interleaved command.
        /// </summary>
        /// <value>The forward interleaved command.</value>
        public int ForwardInterleavedCommand { get; set; }


        /// <summary>
        /// Starts this instance.
        /// </summary>
        public override void Start()
        {
            _logger.Debug("Forward from TCP channel:{0} => {1}:{2}", SourceInterleavedVideo, ForwardHostVideo, ForwardPortVideo);
            ForwardVUdpPort.Connect(ForwardHostVideo, ForwardPortVideo);

            ForwardCommand.DataReceived += HandleDataReceive;

            if (ForwardInterleavedCommand >= 0)
            {
                _forwardCThread = new Thread(new ThreadStart(DoCommandJob));
                _forwardCThread.Start();
            }
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public override void Stop()
        {
            if (ToMulticast && ForwardInterleavedCommand >= 0)
            {
                IPAddress multicastAdress = IPAddress.Parse(ForwardHostVideo);
                ListenCUdpPort.DropMulticastGroup(multicastAdress);
            }

            ForwardCommand.DataReceived -= HandleDataReceive;

            ListenCUdpPort.Close();
            ForwardVUdpPort.Close();
        }

        /// <summary>
        /// Does the command job.
        /// </summary>
        private void DoCommandJob()
        {
            IPEndPoint udpEndPoint = new(IPAddress.Any, ListenCommandPort);
            if (ToMulticast)
            {
                IPAddress multicastAdress = IPAddress.Parse(ForwardHostVideo);
                ListenCUdpPort.JoinMulticastGroup(multicastAdress);
                _logger.Debug("Forward Command from multicast  {0}:{1} => TCP interleaved {2}", ForwardHostVideo, ListenCommandPort, ForwardInterleavedCommand);

            }
            else
            {
                _logger.Debug("Forward Command from {0} => TCP interleaved {1}", ListenCommandPort, ForwardInterleavedCommand);
            }

            byte[] frame;
            try
            {
                do
                {
                    frame = ListenCUdpPort.Receive(ref udpEndPoint);
                    ForwardCommand.BeginSendData(ForwardInterleavedCommand, frame, new AsyncCallback(EndSendCommand), frame);
                }
                while (true);
                //The break of the loop is made by close wich raise an exception
            }
            catch (ObjectDisposedException)
            {
                _logger.Debug("Forward command closed");
            }
            catch (SocketException)
            {
                _logger.Debug("Forward command closed");
            }
        }

        /// <summary>
        /// Ends the send command.
        /// </summary>
        /// <param name="result">The result.</param>
        private void EndSendCommand(IAsyncResult result)
        {
            try
            {
                ForwardCommand.EndSendData(result);
                byte[] frame = (byte[])result.AsyncState;
                CommandFrameSended(frame);
            }
            catch (Exception error)
            {
                _logger.Error(error, "Error during command forwarding");
            }
        }

        /// <summary>
        /// Handles the data receive.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RTSP.RTSPChunkEventArgs"/> instance containing the event data.</param>
        public void HandleDataReceive(object sender, RtspChunkEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            Contract.EndContractBlock();
            try
            {
                if (e.Message is RtspData data)
                {
                    ReadOnlyMemory<byte> frame = data.Data;
                    if (data.Channel == SourceInterleavedVideo)
                    {
                        ForwardVUdpPort.BeginSend(frame.ToArray(), frame.Length, new AsyncCallback(EndSendVideo), frame);
                    }
                }
            }
            catch (Exception error)
            {

                _logger.Warn(error, "Error during frame forwarding");
            }
        }

        /// <summary>
        /// Ends the send video.
        /// </summary>
        /// <param name="result">The result.</param>
        private void EndSendVideo(IAsyncResult result)
        {
            try
            {
                int nbOfByteSend = ForwardVUdpPort.EndSend(result);
                byte[] frame = (byte[])result.AsyncState;
                VideoFrameSended(nbOfByteSend, frame);
            }
            catch (Exception error)
            {
                _logger.Error(error, "Error during video forwarding");
            }
        }
    }
}
