using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Stone_Client.Event;
using Stone_Client.Networking.Iot;
using Stone_Client.Video;

namespace Stone_Client.SelfTest
{
    /// <summary>
    /// Dependency-free checks of the wire contract. Exit code 0 = all passed.
    /// Run: bin\x64\Debug\Stone_Client.SelfTest.exe
    /// </summary>
    internal static class Program
    {
        private static int s_failures;
        private static int s_passed;

        private static int Main()
        {
            var tests = new List<KeyValuePair<string, Action>>
            {
                Test("Result as the server writes it (zero fields omitted)", ResultWithOmittedZeros),
                Test("Result with missing and unknown rock types", ResultContractWarnings),
                Test("H.264-schema result (version 3)", ResultSchemaVersion3),
                Test("JPEG image frame", ImageFrame),
                Test("H.264 transport chunk", VideoChunk),
                Test("Unknown fields are skipped", UnknownFieldsSkipped),
                Test("Envelope without payload", EnvelopeWithoutPayload),
                Test("Last oneof payload wins", LastPayloadWins),
                Test("Truncated input is rejected", TruncatedRejected),
                Test("Wrong wire type is rejected", WrongWireTypeRejected),
                Test("Negative int64 (10-byte varint)", NegativeInt64),
                Test("Length prefix is big-endian", LengthPrefix),
                Test("Handshake request text", HandshakeRequest),
                Test("Client id validation", ClientIdValidation),
                Test("Handshake confirmation parsing", ConfirmationParsing),
                Test("JPEG frame decoding", JpegDecoding),
                Test("Undecodable JPEG is rejected", InvalidJpegRejected),
                Test("BGR24 decoder output to bitmap", Bgr24Conversion),
                Test("FFmpeg decoder arguments", DecoderArguments),
            };

            foreach (var test in tests)
            {
                try
                {
                    test.Value();
                    s_passed++;
                    Console.WriteLine("PASS  " + test.Key);
                }
                catch (Exception ex)
                {
                    s_failures++;
                    Console.WriteLine("FAIL  " + test.Key + "\n      " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine(s_passed + " passed, " + s_failures + " failed.");
            return s_failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------ results

        private static byte[] Rock(string id, double percentage)
        {
            return new TestProtoWriter().String(1, id).Double(2, percentage).ToArray();
        }

        private static byte[] ResultEnvelope(uint schema, string session, ulong sequence, long finalized,
                                             byte[][] rocks, double weight, string unit)
        {
            var result = new TestProtoWriter().UInt32(1, schema).String(2, session).UInt64(3, sequence).Int64(4, finalized);
            foreach (byte[] rock in rocks) result.Message(5, rock);
            result.Double(6, weight).String(7, unit);
            return new TestProtoWriter().UInt32(1, schema).Message(11, result.ToArray()).ToArray();
        }

        private static void ResultWithOmittedZeros()
        {
            byte[] bytes = ResultEnvelope(2, "638700000000000000", 42, 1795000000000,
                new[] { Rock("MiSang", 0.0), Rock("1x2", 33.33), Rock("2x4", 66.67), Rock("4x6", 0.0) }, 0.0, "kg");

            IotEnvelope envelope = IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length);
            Equal(IotPayloadKind.Result, envelope.Kind, "kind");
            Equal(2u, envelope.SchemaVersion, "envelope schema");

            IResult result = IotProtocol.ToResult(envelope.Result);
            Equal("638700000000000000", result.sessionId, "session");
            Equal(42ul, result.sequenceNumber, "sequence");
            Equal(0.0, result.resultPerctMiSang, "MiSang");
            Equal(33.33, result.resultPerct1x2, "1x2");
            Equal(66.67, result.resultPerct2x4, "2x4");
            Equal(0.0, result.resultPerct4x6, "4x6");
            Equal(0.0, result.accumulatedWeight, "weight");
            Equal("kg", result.weightUnit, "unit");
            Equal(DateTimeOffset.FromUnixTimeMilliseconds(1795000000000).LocalDateTime, result.finalizedAt, "finalized");
            True(result.warnings == null, "no warnings expected, got: " + Join(result.warnings));
        }

        private static void ResultContractWarnings()
        {
            byte[] bytes = ResultEnvelope(2, "7", 1, 0,
                new[] { Rock("MiSang", 50), Rock("9x9", 25), Rock("MiSang", 25) }, 12.5, "kg");
            IResult result = IotProtocol.ToResult(IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length).Result);

            Equal(50.0, result.resultPerctMiSang, "first MiSang kept");
            Equal(12.5, result.accumulatedWeight, "weight");
            Equal(DateTime.MinValue, result.finalizedAt, "missing finalized time");
            True(result.warnings != null && result.warnings.Length == 5,
                "expected unknown + duplicate + 3 missing = 5 warnings, got: " + Join(result.warnings));
        }

        private static void ResultSchemaVersion3()
        {
            byte[] bytes = ResultEnvelope(3, "1", 2, 1, new[] { Rock("MiSang", 100), Rock("1x2", 0), Rock("2x4", 0), Rock("4x6", 0) }, 1.25, "kg");
            IotEnvelope envelope = IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length);
            Equal(3u, envelope.SchemaVersion, "schema");
            IResult result = IotProtocol.ToResult(envelope.Result);
            True(result.warnings == null, "no warnings expected, got: " + Join(result.warnings));
            Equal(100.0, result.resultPerctMiSang, "MiSang");
        }

        // ------------------------------------------------------------ live data

        private static void ImageFrame()
        {
            byte[] jpeg = { 0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9 };
            byte[] frame = new TestProtoWriter().UInt32(1, 2).String(2, "55").UInt64(3, 9).Int64(4, 1795000000123)
                .UInt32(5, 1920).UInt32(6, 1200).String(7, "JPEG").Bytes(8, jpeg).ToArray();
            byte[] bytes = new TestProtoWriter().UInt32(1, 2).Message(10, frame).ToArray();

            IotEnvelope envelope = IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length);
            Equal(IotPayloadKind.Image, envelope.Kind, "kind");
            Equal("55", envelope.Image.SessionId, "session");
            Equal(9ul, envelope.Image.SequenceNumber, "sequence");
            Equal(1795000000123L, envelope.Image.ProducedAtUnixMs, "produced");
            Equal(1920u, envelope.Image.ImageWidth, "width");
            Equal(1200u, envelope.Image.ImageHeight, "height");
            Equal("JPEG", envelope.Image.Encoding, "encoding");
            Equal(Convert.ToBase64String(jpeg), Convert.ToBase64String(envelope.Image.JpegImage), "jpeg bytes");
        }

        private static void VideoChunk()
        {
            byte[] ts = new byte[188 * 3];
            ts[0] = 0x47;
            byte[] chunk = new TestProtoWriter().UInt32(1, 3).String(2, "55").UInt64(3, 10).Int64(4, 5)
                .String(5, "MPEG_TS").String(6, "H264").Bytes(7, ts).ToArray();
            byte[] bytes = new TestProtoWriter().UInt32(1, 3).Message(12, chunk).ToArray();

            IotEnvelope envelope = IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length);
            Equal(IotPayloadKind.VideoChunk, envelope.Kind, "kind");
            Equal("MPEG_TS", envelope.VideoChunk.Container, "container");
            Equal("H264", envelope.VideoChunk.Codec, "codec");
            Equal(ts.Length, envelope.VideoChunk.Data.Length, "data length");
            Equal((byte)0x47, envelope.VideoChunk.Data[0], "sync byte");
        }

        // ------------------------------------------------------------ robustness

        private static void UnknownFieldsSkipped()
        {
            byte[] rock = new TestProtoWriter().String(1, "1x2").UInt64(15, 7).Double(2, 10).ToArray();
            byte[] result = new TestProtoWriter().UInt32(1, 2).RawFixed32(20, 0xDEADBEEF).String(2, "s")
                .Message(5, rock).Bytes(30, new byte[] { 1, 2, 3 }).String(7, "kg").ToArray();
            byte[] bytes = new TestProtoWriter().UInt32(1, 2).UInt64(99, 123456789).Bytes(98, new byte[40])
                .Message(11, result).RawFixed32(97, 1).ToArray();

            IotEnvelope envelope = IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length);
            Equal(IotPayloadKind.Result, envelope.Kind, "kind");
            Equal("s", envelope.Result.SessionId, "session");
            Equal(1, envelope.Result.RockPercentages.Count, "rocks");
            Equal(10.0, envelope.Result.RockPercentages[0].Percentage, "percentage");
            Equal("kg", envelope.Result.WeightUnit, "unit");
        }

        private static void EnvelopeWithoutPayload()
        {
            byte[] bytes = new TestProtoWriter().UInt32(1, 2).ToArray();
            Equal(IotPayloadKind.None, IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length).Kind, "kind");
        }

        private static void LastPayloadWins()
        {
            byte[] image = new TestProtoWriter().String(2, "img").ToArray();
            byte[] result = new TestProtoWriter().String(2, "res").ToArray();
            byte[] bytes = new TestProtoWriter().Message(10, image).Message(11, result).ToArray();
            IotEnvelope envelope = IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length);
            Equal(IotPayloadKind.Result, envelope.Kind, "kind");
            True(envelope.Image == null, "image payload must be cleared");
        }

        private static void TruncatedRejected()
        {
            byte[] full = ResultEnvelope(2, "1", 1, 1, new[] { Rock("MiSang", 1) }, 1, "kg");
            for (int length = 1; length < full.Length; length++)
            {
                try
                {
                    IotProtocol.DecodeEnvelope(full, 0, length);
                }
                catch (InvalidDataException)
                {
                    continue;
                }
                // Some prefixes end exactly on a field boundary of the outer message and
                // are valid protobuf; only the full nested message boundary matters here.
                if (length > 2) throw new Exception("A " + length + "-byte prefix of a " + full.Length + "-byte envelope decoded without error.");
            }
            ThrowsInvalidData(() => IotProtocol.DecodeEnvelope(new byte[] { 0x58, 0xFF }, 0, 2), "truncated varint");
        }

        private static void WrongWireTypeRejected()
        {
            byte[] bytes = new TestProtoWriter().RawTag(11, 0).RawBytes(1).ToArray();
            ThrowsInvalidData(() => IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length), "payload as varint");
            byte[] group = new TestProtoWriter().RawTag(50, 3).ToArray();
            ThrowsInvalidData(() => IotProtocol.DecodeEnvelope(group, 0, group.Length), "group wire type");
        }

        private static void NegativeInt64()
        {
            byte[] result = new TestProtoWriter().Int64(4, -5).ToArray();
            Equal(1 + 10, result.Length, "encoded length");
            byte[] bytes = new TestProtoWriter().Message(11, result).ToArray();
            Equal(-5L, IotProtocol.DecodeEnvelope(bytes, 0, bytes.Length).Result.FinalizedAtUnixMs, "value");
        }

        private static void LengthPrefix()
        {
            Equal(258u, IotProtocol.ReadLengthPrefix(new byte[] { 0, 0, 1, 2 }), "small");
            Equal(0x01020304u, IotProtocol.ReadLengthPrefix(new byte[] { 1, 2, 3, 4 }), "order");
            Equal(0xFFFFFFFFu, IotProtocol.ReadLengthPrefix(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }), "max");
        }

        // ------------------------------------------------------------ handshake

        private static void HandshakeRequest()
        {
            Equal("SEARCHING FOR SERVER - my current id is:PC-01; image encoding is:jpeg",
                IotProtocol.CreateHandshake("PC-01", IotImageEncoding.Jpeg), "jpeg");
            Equal("SEARCHING FOR SERVER - my current id is:PC-01; image encoding is:h264",
                IotProtocol.CreateHandshake("PC-01", IotImageEncoding.H264), "h264");
        }

        private static void ClientIdValidation()
        {
            True(IotProtocol.IsValidClientId("PC-01"), "simple id");
            True(IotProtocol.IsValidClientId(new string('a', 128)), "128 characters");
            True(!IotProtocol.IsValidClientId(new string('a', 129)), "129 characters");
            True(!IotProtocol.IsValidClientId("a;b"), "semicolon");
            True(!IotProtocol.IsValidClientId("a\nb"), "newline");
            True(!IotProtocol.IsValidClientId(" a"), "leading space (server trims it)");
            True(!IotProtocol.IsValidClientId(""), "empty");
        }

        private static void ConfirmationParsing()
        {
            IotHandshakeConfirmation confirmation;
            True(IotProtocol.TryParseConfirmation("SERVER CONFIRMED - client id: PC-01; tcp port: 50002; image encoding: JPEG", out confirmation), "jpeg reply");
            Equal("PC-01", confirmation.ClientId, "id");
            Equal(50002, confirmation.TcpPort, "port");
            Equal(IotImageEncoding.Jpeg, confirmation.Encoding, "jpeg");

            True(IotProtocol.TryParseConfirmation("SERVER CONFIRMED - client id: PC-01; tcp port: 50002; image encoding: H264_MPEG_TS", out confirmation), "h264 reply");
            Equal(IotImageEncoding.H264, confirmation.Encoding, "h264");

            True(IotProtocol.TryParseConfirmation("SERVER CONFIRMED - client id: PC-01; tcp port: 50002", out confirmation), "legacy reply");
            Equal(IotImageEncoding.Jpeg, confirmation.Encoding, "legacy defaults to jpeg");

            True(!IotProtocol.TryParseConfirmation("SERVER CONFIRMED - client id: PC-01", out confirmation), "missing port");
            True(!IotProtocol.TryParseConfirmation("SERVER CONFIRMED - client id: PC-01; tcp port: 99999", out confirmation), "port out of range");
            True(!IotProtocol.TryParseConfirmation("SERVER CONFIRMED - client id: PC-01; tcp port: 50002; image encoding: VP9", out confirmation), "unknown encoding");
            True(!IotProtocol.TryParseConfirmation("SEARCHING FOR SERVER - my current id is:PC-01", out confirmation), "echoed request");
        }

        // ------------------------------------------------------------ video

        private static void JpegDecoding()
        {
            byte[] jpeg;
            using (var source = new Bitmap(64, 40, PixelFormat.Format24bppRgb))
            {
                using (Graphics graphics = Graphics.FromImage(source)) graphics.Clear(Color.FromArgb(200, 30, 30));
                using (var stream = new MemoryStream())
                {
                    source.Save(stream, ImageFormat.Jpeg);
                    jpeg = stream.ToArray();
                }
            }

            using (Bitmap decoded = FrameDecoding.DecodeJpeg(jpeg))
            {
                Equal(64, decoded.Width, "width");
                Equal(40, decoded.Height, "height");
                Color centre = decoded.GetPixel(32, 20);
                True(centre.R > 170 && centre.G < 70 && centre.B < 70, "colour survives JPEG: " + centre);
            }
            // The bitmap must not depend on the caller's buffer.
            Bitmap independent = FrameDecoding.DecodeJpeg(jpeg);
            Array.Clear(jpeg, 0, jpeg.Length);
            using (independent) True(independent.GetPixel(32, 20).R > 170, "bitmap is independent of the source bytes");
        }

        private static void InvalidJpegRejected()
        {
            ThrowsInvalidData(() => FrameDecoding.DecodeJpeg(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0xFF, 0xD9 }), "placeholder JPEG");
            ThrowsInvalidData(() => FrameDecoding.DecodeJpeg(new byte[0]), "empty JPEG");

            // A valid JPEG whose header claims 65535x65535 pixels: GDI+ fails with
            // OutOfMemoryException or ArgumentException, which must not escape.
            byte[] huge;
            using (var source = new Bitmap(16, 16, PixelFormat.Format24bppRgb))
            using (var stream = new MemoryStream())
            {
                source.Save(stream, ImageFormat.Jpeg);
                huge = stream.ToArray();
            }
            int sof = -1;
            for (int index = 2; index + 8 < huge.Length; index++)
                if (huge[index] == 0xFF && huge[index + 1] == 0xC0) { sof = index; break; }
            True(sof > 0, "test JPEG has a baseline SOF marker");
            for (int index = sof + 5; index < sof + 9; index++) huge[index] = 0xFF;
            ThrowsInvalidData(() => FrameDecoding.DecodeJpeg(huge).Dispose(), "JPEG claiming 65535x65535");
        }

        private static void Bgr24Conversion()
        {
            // 3x2 frame (odd row width exercises the padded-stride path): B, G, R per pixel.
            byte[] pixels =
            {
                255, 0, 0,    0, 255, 0,    0, 0, 255,
                10, 20, 30,   40, 50, 60,   70, 80, 90
            };
            using (Bitmap bitmap = FrameDecoding.FromBgr24(pixels, 3, 2))
            {
                Equal(Color.FromArgb(0, 0, 255).ToArgb(), bitmap.GetPixel(0, 0).ToArgb(), "blue pixel");
                Equal(Color.FromArgb(0, 255, 0).ToArgb(), bitmap.GetPixel(1, 0).ToArgb(), "green pixel");
                Equal(Color.FromArgb(255, 0, 0).ToArgb(), bitmap.GetPixel(2, 0).ToArgb(), "red pixel");
                Equal(Color.FromArgb(90, 80, 70).ToArgb(), bitmap.GetPixel(2, 1).ToArgb(), "second row");
            }
        }

        private static void DecoderArguments()
        {
            string arguments = FfmpegH264Decoder.BuildArguments(1920, 1200);
            True(arguments.Contains("-f mpegts -i pipe:0"), "reads MPEG-TS from stdin");
            True(arguments.Contains("scale=1920:1200"), "fixed output size");
            True(arguments.Contains("-fps_mode passthrough"), "no duplicate frames after a gap");
            True(!arguments.Contains("nobuffer"), "probed packets, including the first keyframe, are kept");
            True(arguments.Contains("-pix_fmt bgr24 -f rawvideo pipe:1"), "raw BGR24 on stdout");
        }

        // ------------------------------------------------------------ helpers

        private static KeyValuePair<string, Action> Test(string name, Action body)
        {
            return new KeyValuePair<string, Action>(name, body);
        }

        private static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(what + ": expected <" + expected + "> but got <" + actual + ">.");
        }

        private static void True(bool condition, string what)
        {
            if (!condition) throw new Exception(what);
        }

        private static void ThrowsInvalidData(Action action, string what)
        {
            try { action(); }
            catch (InvalidDataException) { return; }
            throw new Exception(what + ": expected InvalidDataException.");
        }

        private static string Join(string[] values)
        {
            return values == null ? "(none)" : String.Join(" | ", values);
        }
    }
}
