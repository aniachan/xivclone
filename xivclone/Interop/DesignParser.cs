using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Text;

namespace xivclone.Interop
{
    public class DesignParser
    {
        public const int Base64SizeV1 = 86;
        public const int Base64SizeV2 = 91;
        public const int Base64SizeV4 = 95;

        public JObject FromBase64(string base64)
        {
            var version = 0;

            var bytes = Convert.FromBase64String(base64);
            version = bytes[0];
            switch (version)
            {
                case (byte)'{':
                    var jObj1 = JObject.Parse(Encoding.UTF8.GetString(bytes));
                    return jObj1;
                case 1:
                case 2:
                case 4:
                    return null;
                case 3:
                    {
                        version = bytes.DecompressToString(out var decompressed);
                        var jObj2 = JObject.Parse(decompressed);
                        return jObj2;
                    }
                case 5:
                    {
                        bytes = bytes[Base64SizeV4..];
                        version = bytes.DecompressToString(out var decompressed);
                        var jObj2 = JObject.Parse(decompressed);
                        return jObj2;
                    }
                case 6:
                    {
                        version = bytes.DecompressToString(out var decompressed);
                        var jObj2 = JObject.Parse(decompressed);
                        Debug.Assert(version == 6);
                        return jObj2;
                    }

                default: throw new Exception($"Unknown Version {bytes[0]}.");
            }
        }
    }
    public static class CompressExtensions
    {
        /// <summary> Decompress a byte array into a returned version byte and an array of the remaining bytes. </summary>
        public static byte Decompress(this byte[] compressed, out byte[] decompressed)
        {
            var ret = compressed[0];
            using var compressedStream = new MemoryStream(compressed, 1, compressed.Length - 1);
            using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            zipStream.CopyTo(resultStream);
            decompressed = resultStream.ToArray();
            return ret;
        }

        /// <summary> Decompress a byte array into a returned version byte and a string of the remaining bytes as UTF8. </summary>
        public static byte DecompressToString(this byte[] compressed, out string decompressed)
        {
            var ret = compressed.Decompress(out var bytes);
            decompressed = Encoding.UTF8.GetString(bytes);
            return ret;
        }
    }
}
