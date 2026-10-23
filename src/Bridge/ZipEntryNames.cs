using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AQRLiteHelper.Bridge
{
    /// <summary>
    /// Recovers the real entry names of a zip archive. <see cref="System.IO.Compression.ZipArchive"/>
    /// decodes names without the UTF-8 flag using a single fixed encoding and ignores the
    /// Info-ZIP Unicode Path extra field, which garbles GBK names such as "【Artcore】 x.ogg"
    /// produced by Chinese Windows tools. This reads the central directory directly.
    /// </summary>
    internal static class ZipEntryNames
    {
        private const uint EndOfCentralDirectorySignature = 0x06054b50;
        private const uint Zip64LocatorSignature = 0x07064b50;
        private const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
        private const uint CentralDirectoryHeaderSignature = 0x02014b50;
        private const ushort UnicodePathExtraId = 0x7075;
        private const int Utf8NameFlag = 0x800;
        private const long MaxCentralDirectoryBytes = 64L * 1024L * 1024L;

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly uint[] CrcTable = BuildCrcTable();

        /// <summary>
        /// Returns entry names in central-directory order (the order of
        /// <c>ZipArchive.Entries</c>), or null when the directory cannot be parsed.
        /// </summary>
        public static IReadOnlyList<string>? Read(string archivePath)
        {
            try
            {
                var records = ReadCentralDirectory(archivePath);
                return records == null ? null : Decode(records);
            }
            catch (Exception ex)
            {
                Main.Mod?.Logger.Warning($"读取压缩包文件名失败，将使用默认解码：{ex.Message}");
                return null;
            }
        }

        private static List<string> Decode(List<Record> records)
        {
            var legacy = ChooseLegacyEncoding(records);
            return records
                .Select(record => record.Utf8Flag
                    ? Encoding.UTF8.GetString(record.Name)
                    : record.UnicodePath ?? legacy.GetString(record.Name))
                .ToList();
        }

        /// <summary>
        /// Names with neither the UTF-8 flag nor a Unicode Path field are in the
        /// creator's ANSI code page. Many tools write UTF-8 without setting the flag;
        /// otherwise assume GBK, the usual code page in the Chinese AQR community.
        /// Decided per archive, since a single GBK name can also be valid UTF-8.
        /// </summary>
        private static Encoding ChooseLegacyEncoding(List<Record> records)
        {
            var legacyNames = records.Where(record => !record.Utf8Flag && record.UnicodePath == null);
            if (legacyNames.All(record => IsValidUtf8(record.Name)))
            {
                return Encoding.UTF8;
            }

            try
            {
                return Encoding.GetEncoding(936);
            }
            catch (Exception ex)
            {
                Main.Mod?.Logger.Warning($"当前环境不支持 GBK 解码：{ex.Message}");
                return Encoding.UTF8;
            }
        }

        private static List<Record>? ReadCentralDirectory(string archivePath)
        {
            using var stream = File.OpenRead(archivePath);

            // EOCD (22 bytes) + max comment (65535) + room for the Zip64 locator (20).
            var tailLength = (int)Math.Min(stream.Length, 22L + 65535L + 20L);
            var tail = new byte[tailLength];
            stream.Seek(-tailLength, SeekOrigin.End);
            ReadExactly(stream, tail);

            var eocd = LastIndexOfSignature(tail, EndOfCentralDirectorySignature);
            if (eocd < 0)
            {
                return null;
            }

            long count = ReadUInt16(tail, eocd + 10);
            long size = ReadUInt32(tail, eocd + 12);
            long offset = ReadUInt32(tail, eocd + 16);
            if (count == ushort.MaxValue || size == uint.MaxValue || offset == uint.MaxValue)
            {
                var locator = eocd - 20;
                if (locator < 0 || ReadUInt32(tail, locator) != Zip64LocatorSignature)
                {
                    return null;
                }

                var zip64 = new byte[56];
                stream.Seek((long)ReadUInt64(tail, locator + 8), SeekOrigin.Begin);
                ReadExactly(stream, zip64);
                if (ReadUInt32(zip64, 0) != Zip64EndOfCentralDirectorySignature)
                {
                    return null;
                }

                count = (long)ReadUInt64(zip64, 32);
                size = (long)ReadUInt64(zip64, 40);
                offset = (long)ReadUInt64(zip64, 48);
            }

            if (size > MaxCentralDirectoryBytes || offset < 0 || offset + size > stream.Length)
            {
                return null;
            }

            var directory = new byte[size];
            stream.Seek(offset, SeekOrigin.Begin);
            ReadExactly(stream, directory);

            var records = new List<Record>();
            var position = 0;
            for (long i = 0; i < count; i++)
            {
                if (position + 46 > directory.Length
                    || ReadUInt32(directory, position) != CentralDirectoryHeaderSignature)
                {
                    return null;
                }

                var flags = ReadUInt16(directory, position + 8);
                var nameLength = ReadUInt16(directory, position + 28);
                var extraLength = ReadUInt16(directory, position + 30);
                var commentLength = ReadUInt16(directory, position + 32);
                var nameStart = position + 46;
                var extraStart = nameStart + nameLength;
                var next = extraStart + extraLength + commentLength;
                if (next > directory.Length)
                {
                    return null;
                }

                var name = new byte[nameLength];
                Buffer.BlockCopy(directory, nameStart, name, 0, nameLength);
                records.Add(new Record(
                    name,
                    (flags & Utf8NameFlag) != 0,
                    ReadUnicodePath(directory, extraStart, extraLength, name)));
                position = next;
            }

            return records;
        }

        /// <summary>
        /// Info-ZIP Unicode Path field (0x7075): version 1, CRC-32 of the raw header
        /// name, then the UTF-8 name. A CRC mismatch means a tool renamed the entry
        /// without updating the field, so the field is ignored.
        /// </summary>
        private static string? ReadUnicodePath(byte[] data, int start, int length, byte[] rawName)
        {
            var position = start;
            var end = start + length;
            while (position + 4 <= end)
            {
                var id = ReadUInt16(data, position);
                var size = ReadUInt16(data, position + 2);
                var body = position + 4;
                if (body + size > end)
                {
                    return null;
                }

                if (id == UnicodePathExtraId && size >= 5 && data[body] == 1
                    && ReadUInt32(data, body + 1) == Crc32(rawName))
                {
                    try
                    {
                        return StrictUtf8.GetString(data, body + 5, size - 5);
                    }
                    catch (DecoderFallbackException)
                    {
                        return null;
                    }
                }

                position = body + size;
            }

            return null;
        }

        private static bool IsValidUtf8(byte[] bytes)
        {
            try
            {
                StrictUtf8.GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        private static int LastIndexOfSignature(byte[] data, uint signature)
        {
            for (var i = data.Length - 22; i >= 0; i--)
            {
                if (ReadUInt32(data, i) == signature)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void ReadExactly(Stream stream, byte[] buffer)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var count = stream.Read(buffer, read, buffer.Length - read);
                if (count <= 0)
                {
                    throw new EndOfStreamException("压缩包目录不完整。");
                }

                read += count;
            }
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static ulong ReadUInt64(byte[] data, int offset)
        {
            return ReadUInt32(data, offset) | ((ulong)ReadUInt32(data, offset + 4) << 32);
        }

        private static uint Crc32(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var value in data)
            {
                crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            return ~crc;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var value = i;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }

        private sealed class Record
        {
            public Record(byte[] name, bool utf8Flag, string? unicodePath)
            {
                Name = name;
                Utf8Flag = utf8Flag;
                UnicodePath = unicodePath;
            }

            public byte[] Name { get; }
            public bool Utf8Flag { get; }
            public string? UnicodePath { get; }
        }
    }
}
