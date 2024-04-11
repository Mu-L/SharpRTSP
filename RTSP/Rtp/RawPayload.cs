﻿using Rtsp.Onvif;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace Rtsp.Rtp
{
    public class RawPayload : IPayloadProcessor
    {
        private readonly MemoryPool<byte> _memoryPool;

        public RawPayload(MemoryPool<byte>? memoryPool = null)
        {
            _memoryPool = memoryPool ?? MemoryPool<byte>.Shared;
        }

        public IList<ReadOnlyMemory<byte>> ProcessRTPPacket(RtpPacket packet, out DateTime? timestamp)
        {
            timestamp = DateTime.MinValue;
            return [packet.Payload.ToArray()];
        }

        public RawMediaFrame ProcessPacket(RtpPacket packet)
        {
            var owner = _memoryPool.Rent(packet.PayloadSize);
            var memory = owner.Memory[..packet.PayloadSize];
            packet.Payload.CopyTo(memory.Span);
            return new RawMediaFrame([memory], [owner])
            {
                ClockTimestamp = RtpPacketOnvifUtils.ProcessRTPTimestampExtension(packet.Extension, headerPosition: out _),
                RtpTimestamp = packet.Timestamp,
            };
        }
    }
}
