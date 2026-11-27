using System;
using System.Collections.Generic;
using System.IO;
using Stone_Client.Event;

namespace Stone_Client.Networking.Iot
{
    public enum IotImageEncoding { Jpeg, H264 }

    internal enum IotPayloadKind { None, Image, Result, VideoChunk }

    /// <summary>ImageFrame (iot_image.proto).</summary>
    internal sealed class IotImageFrame
    {
        internal uint SchemaVersion;
        internal string SessionId = String.Empty;
        internal ulong SequenceNumber;
        internal long ProducedAtUnixMs;
        internal uint ImageWidth;
        internal uint ImageHeight;
        internal string Encoding = String.Empty;
        internal byte[] JpegImage = new byte[0];
    }

    /// <summary>H264TransportChunk (iot_image.proto).</summary>
    internal sealed class IotVideoChunk
    {
        internal uint SchemaVersion;
        internal string SessionId = String.Empty;
        internal ulong SequenceNumber;
        internal long ProducedAtUnixMs;
        internal string Container = String.Empty;
        internal string Codec = String.Empty;
        internal byte[] Data = new byte[0];
    }

    /// <summary>RockPercentage (iot_result.proto).</summary>
    internal sealed class IotRockPercentage
    {
        internal string RockTypeId = String.Empty;
        internal double Percentage;
    }

    /// <summary>InferenceResult (iot_result.proto).</summary>
    internal sealed class IotInferenceResult
    {
        internal uint SchemaVersion;
        internal string SessionId = String.Empty;
        internal ulong SequenceNumber;
        internal long FinalizedAtUnixMs;
        internal readonly List<IotRockPercentage> RockPercentages = new List<IotRockPercentage>();
        internal double AccumulatedWeight;
        internal string WeightUnit = String.Empty;
    }

    /// <summary>IotEnvelope (iot_result.proto). Exactly one payload is set unless Kind is None.</summary>
    internal sealed class IotEnvelope
    {
        internal uint SchemaVersion;
        internal IotPayloadKind Kind;
        internal IotImageFrame Image;
        internal IotInferenceResult Result;
        internal IotVideoChunk VideoChunk;
    }

    internal sealed class IotHandshakeConfirmation
    {
        internal string ClientId;
        internal int TcpPort;
        internal IotImageEncoding Encoding;
    }

    /// <summary>
    /// Client side of the contract in 0_UI_Process/Stone_Application/Networking/Iot/IotProtocol.cs
    /// and Protobuf/*.proto. Proto3 semantics apply: the server omits zero/empty
    /// fields, so absent fields decode to their defaults.
    /// </summary>
    internal static class IotProtocol
    {
        internal const uint JpegSchemaVersion = 2;
        internal const uint H264SchemaVersion = 3;
        internal const string HandshakePrefix = "SEARCHING FOR SERVER - my current id is:";
        internal const string ConfirmationPrefix = "SERVER CONFIRMED -";
        internal static readonly string[] RockTypeIds = { "MiSang", "1x2", "2x4", "4x6" };

        // ---------------------------------------------------------------- handshake

        internal static bool IsValidClientId(string p_clientId)
        {
            // The server takes everything before the first ';' as the id and rejects
            // empty ids, ids over 128 characters, and CR/LF/NUL.
            return !String.IsNullOrWhiteSpace(p_clientId) &&
                   p_clientId.Trim().Length == p_clientId.Length &&
                   p_clientId.Length <= Config.MAX_CLIENT_ID_LENGTH &&
                   p_clientId.IndexOfAny(new[] { ';', '\r', '\n', '\0' }) < 0;
        }

        /// <summary>
        /// The IP declaration is deliberately omitted: the server rejects the whole
        /// handshake when a declared address differs from the datagram's source,
        /// which happens on multi-homed machines.
        /// </summary>
        internal static string CreateHandshake(string p_clientId, IotImageEncoding p_encoding)
        {
            if (!IsValidClientId(p_clientId)) throw new ArgumentException("Invalid client id.", "p_clientId");
            return HandshakePrefix + p_clientId + "; image encoding is:" + (p_encoding == IotImageEncoding.H264 ? "h264" : "jpeg");
        }

        /// <summary>
        /// Parses "SERVER CONFIRMED - client id: X; tcp port: 50002; image encoding: JPEG|H264_MPEG_TS".
        /// A reply without an encoding is treated as JPEG (servers before H.264 negotiation).
        /// </summary>
        internal static bool TryParseConfirmation(string p_message, out IotHandshakeConfirmation p_confirmation)
        {
            p_confirmation = null;
            if (String.IsNullOrWhiteSpace(p_message)) return false;
            string message = p_message.Trim('\0', '\r', '\n', ' ');
            if (!message.StartsWith(ConfirmationPrefix, StringComparison.OrdinalIgnoreCase)) return false;

            var confirmation = new IotHandshakeConfirmation { Encoding = IotImageEncoding.Jpeg };
            bool hasPort = false;
            foreach (string rawPart in message.Substring(ConfirmationPrefix.Length).Split(';'))
            {
                string part = rawPart.Trim();
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                string key = part.Substring(0, colon).Trim();
                string value = part.Substring(colon + 1).Trim();

                if (String.Equals(key, "client id", StringComparison.OrdinalIgnoreCase))
                {
                    confirmation.ClientId = value;
                }
                else if (String.Equals(key, "tcp port", StringComparison.OrdinalIgnoreCase))
                {
                    int port;
                    if (!Int32.TryParse(value, System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
                        return false;
                    confirmation.TcpPort = port;
                    hasPort = true;
                }
                else if (String.Equals(key, "image encoding", StringComparison.OrdinalIgnoreCase))
                {
                    if (String.Equals(value, "H264_MPEG_TS", StringComparison.OrdinalIgnoreCase))
                        confirmation.Encoding = IotImageEncoding.H264;
                    else if (String.Equals(value, "JPEG", StringComparison.OrdinalIgnoreCase))
                        confirmation.Encoding = IotImageEncoding.Jpeg;
                    else
                        return false;
                }
            }

            if (String.IsNullOrEmpty(confirmation.ClientId) || !hasPort) return false;
            p_confirmation = confirmation;
            return true;
        }

        // ---------------------------------------------------------------- framing

        /// <summary>Reads the 4-byte unsigned big-endian length that precedes each envelope.</summary>
        internal static uint ReadLengthPrefix(byte[] p_header)
        {
            if (p_header == null || p_header.Length < 4) throw new ArgumentException("A length prefix has four bytes.");
            return (uint)p_header[0] << 24 | (uint)p_header[1] << 16 | (uint)p_header[2] << 8 | p_header[3];
        }

        // ---------------------------------------------------------------- envelope

        internal static IotEnvelope DecodeEnvelope(byte[] p_buffer, int p_offset, int p_count)
        {
            var reader = new ProtoReader(p_buffer, p_offset, p_count);
            var envelope = new IotEnvelope { Kind = IotPayloadKind.None };
            int field, wireType;
            while (reader.TryReadTag(out field, out wireType))
            {
                switch (field)
                {
                    case 1:
                        ProtoReader.Expect(field, wireType, ProtoReader.WireVarint);
                        envelope.SchemaVersion = reader.ReadUInt32();
                        break;
                    // oneof payload: the last one on the wire wins.
                    case 10:
                        ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited);
                        SetPayload(envelope, IotPayloadKind.Image);
                        envelope.Image = DecodeImageFrame(reader.ReadMessage());
                        break;
                    case 11:
                        ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited);
                        SetPayload(envelope, IotPayloadKind.Result);
                        envelope.Result = DecodeInferenceResult(reader.ReadMessage());
                        break;
                    case 12:
                        ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited);
                        SetPayload(envelope, IotPayloadKind.VideoChunk);
                        envelope.VideoChunk = DecodeVideoChunk(reader.ReadMessage());
                        break;
                    default:
                        reader.Skip(wireType);
                        break;
                }
            }
            return envelope;
        }

        private static void SetPayload(IotEnvelope p_envelope, IotPayloadKind p_kind)
        {
            p_envelope.Kind = p_kind;
            p_envelope.Image = null;
            p_envelope.Result = null;
            p_envelope.VideoChunk = null;
        }

        private static IotImageFrame DecodeImageFrame(ProtoReader p_reader)
        {
            var frame = new IotImageFrame();
            int field, wireType;
            while (p_reader.TryReadTag(out field, out wireType))
            {
                switch (field)
                {
                    case 1: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); frame.SchemaVersion = p_reader.ReadUInt32(); break;
                    case 2: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); frame.SessionId = p_reader.ReadString(); break;
                    case 3: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); frame.SequenceNumber = p_reader.ReadUInt64(); break;
                    case 4: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); frame.ProducedAtUnixMs = p_reader.ReadInt64(); break;
                    case 5: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); frame.ImageWidth = p_reader.ReadUInt32(); break;
                    case 6: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); frame.ImageHeight = p_reader.ReadUInt32(); break;
                    case 7: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); frame.Encoding = p_reader.ReadString(); break;
                    case 8: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); frame.JpegImage = p_reader.ReadBytes(); break;
                    default: p_reader.Skip(wireType); break;
                }
            }
            return frame;
        }

        private static IotVideoChunk DecodeVideoChunk(ProtoReader p_reader)
        {
            var chunk = new IotVideoChunk();
            int field, wireType;
            while (p_reader.TryReadTag(out field, out wireType))
            {
                switch (field)
                {
                    case 1: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); chunk.SchemaVersion = p_reader.ReadUInt32(); break;
                    case 2: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); chunk.SessionId = p_reader.ReadString(); break;
                    case 3: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); chunk.SequenceNumber = p_reader.ReadUInt64(); break;
                    case 4: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); chunk.ProducedAtUnixMs = p_reader.ReadInt64(); break;
                    case 5: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); chunk.Container = p_reader.ReadString(); break;
                    case 6: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); chunk.Codec = p_reader.ReadString(); break;
                    case 7: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); chunk.Data = p_reader.ReadBytes(); break;
                    default: p_reader.Skip(wireType); break;
                }
            }
            return chunk;
        }

        private static IotInferenceResult DecodeInferenceResult(ProtoReader p_reader)
        {
            var result = new IotInferenceResult();
            int field, wireType;
            while (p_reader.TryReadTag(out field, out wireType))
            {
                switch (field)
                {
                    case 1: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); result.SchemaVersion = p_reader.ReadUInt32(); break;
                    case 2: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); result.SessionId = p_reader.ReadString(); break;
                    case 3: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); result.SequenceNumber = p_reader.ReadUInt64(); break;
                    case 4: ProtoReader.Expect(field, wireType, ProtoReader.WireVarint); result.FinalizedAtUnixMs = p_reader.ReadInt64(); break;
                    case 5: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); result.RockPercentages.Add(DecodeRockPercentage(p_reader.ReadMessage())); break;
                    case 6: ProtoReader.Expect(field, wireType, ProtoReader.WireFixed64); result.AccumulatedWeight = p_reader.ReadDouble(); break;
                    case 7: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); result.WeightUnit = p_reader.ReadString(); break;
                    default: p_reader.Skip(wireType); break;
                }
            }
            return result;
        }

        private static IotRockPercentage DecodeRockPercentage(ProtoReader p_reader)
        {
            var rock = new IotRockPercentage();
            int field, wireType;
            while (p_reader.TryReadTag(out field, out wireType))
            {
                switch (field)
                {
                    case 1: ProtoReader.Expect(field, wireType, ProtoReader.WireLengthDelimited); rock.RockTypeId = p_reader.ReadString(); break;
                    case 2: ProtoReader.Expect(field, wireType, ProtoReader.WireFixed64); rock.Percentage = p_reader.ReadDouble(); break;
                    default: p_reader.Skip(wireType); break;
                }
            }
            return rock;
        }

        // ---------------------------------------------------------------- mapping

        /// <summary>
        /// Maps the wire result to the UI payload and records contract deviations
        /// (missing/unknown/duplicate rock types, non-finite numbers) as warnings
        /// instead of dropping the result.
        /// </summary>
        internal static IResult ToResult(IotInferenceResult p_result)
        {
            var warnings = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var mapped = new IResult
            {
                schemaVersion = p_result.SchemaVersion,
                sessionId = String.IsNullOrEmpty(p_result.SessionId) ? "(none)" : p_result.SessionId,
                sequenceNumber = p_result.SequenceNumber,
                finalizedAt = DateTime.MinValue,
                accumulatedWeight = p_result.AccumulatedWeight,
                weightUnit = p_result.WeightUnit
            };

            if (p_result.FinalizedAtUnixMs > 0)
            {
                try { mapped.finalizedAt = DateTimeOffset.FromUnixTimeMilliseconds(p_result.FinalizedAtUnixMs).LocalDateTime; }
                catch (ArgumentOutOfRangeException) { warnings.Add("finalized_at_unix_ms is out of range."); }
            }

            foreach (IotRockPercentage rock in p_result.RockPercentages)
            {
                if (!seen.Add(rock.RockTypeId)) { warnings.Add("Duplicate rock type '" + rock.RockTypeId + "'."); continue; }
                if (Double.IsNaN(rock.Percentage) || Double.IsInfinity(rock.Percentage))
                    warnings.Add("Rock type '" + rock.RockTypeId + "' has a non-finite percentage.");

                switch (rock.RockTypeId)
                {
                    case "MiSang": mapped.resultPerctMiSang = rock.Percentage; break;
                    case "1x2": mapped.resultPerct1x2 = rock.Percentage; break;
                    case "2x4": mapped.resultPerct2x4 = rock.Percentage; break;
                    case "4x6": mapped.resultPerct4x6 = rock.Percentage; break;
                    default: warnings.Add("Unknown rock type '" + rock.RockTypeId + "' was ignored."); break;
                }
            }
            foreach (string id in RockTypeIds)
                if (!seen.Contains(id)) warnings.Add("Rock type '" + id + "' is missing; shown as 0.");

            if (Double.IsNaN(p_result.AccumulatedWeight) || Double.IsInfinity(p_result.AccumulatedWeight))
                warnings.Add("The accumulated weight is not a finite number.");
            if (p_result.SchemaVersion != JpegSchemaVersion && p_result.SchemaVersion != H264SchemaVersion)
                warnings.Add("Unexpected schema version " + p_result.SchemaVersion + ".");

            mapped.warnings = warnings.Count == 0 ? null : warnings.ToArray();
            return mapped;
        }
    }
}
