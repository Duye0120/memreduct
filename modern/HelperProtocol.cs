using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MemReduct.Fluent
{
    // Shared with the protected .NET Framework helper; no paths or executable commands cross this boundary.
    internal static class HelperProtocol
    {
        internal const int Magic = 0x4d524831;
        internal const int MaximumResponse = 65536;
        internal static byte[] Request(bool clean, CleanAreas areas, bool automatic, bool chinese)
        {
            if (clean) MemoryEngine.ValidateAreas(areas);
            using (var data = new MemoryStream())
            using (var writer = new BinaryWriter(data))
            {
                writer.Write(Magic); writer.Write((byte)1); writer.Write((byte)(clean ? 2 : 1));
                writer.Write((int)areas); writer.Write((byte)(automatic ? 1 : 0)); writer.Write((byte)(chinese ? 1 : 0));
                return data.ToArray();
            }
        }
        internal static void ParseRequest(byte[] data, out bool clean, out CleanAreas areas, out bool automatic, out bool chinese)
        {
            if (data.Length != 12) throw new InvalidDataException("Invalid helper request size.");
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                if (reader.ReadInt32() != Magic || reader.ReadByte() != 1) throw new InvalidDataException("Unsupported helper protocol.");
                var operation = reader.ReadByte();
                areas = (CleanAreas)reader.ReadInt32();
                var auto = reader.ReadByte(); var language = reader.ReadByte();
                if ((operation != 1 && operation != 2) || auto > 1 || language > 1
                    || (operation == 1 && (areas != CleanAreas.None || auto != 0))) throw new InvalidDataException("Invalid helper operation.");
                clean = operation == 2; automatic = auto == 1; chinese = language == 1;
                if (clean) MemoryEngine.ValidateAreas(areas);
                if (automatic) areas = MemoryEngine.ForAutomaticCleanup(areas);
            }
        }
        internal static byte[] Response(CleanupResult result)
        {
            using (var data = new MemoryStream())
            using (var writer = new BinaryWriter(data))
            {
                writer.Write(Magic); writer.Write(result.FreedBytes); writer.Write((int)result.CompletedAreas);
                writer.Write(Math.Min(result.Errors.Count, 16));
                for (var i = 0; i < Math.Min(result.Errors.Count, 16); i++)
                {
                    var message = result.Errors[i];
                    var bytes = Encoding.UTF8.GetBytes(message.Length > 1000 ? message.Substring(0, 1000) : message);
                    writer.Write(bytes.Length); writer.Write(bytes);
                }
                if (data.Length > MaximumResponse) throw new InvalidDataException("Helper response is too large.");
                return data.ToArray();
            }
        }
        internal static CleanupResult ParseResponse(byte[] data)
        {
            if (data.Length < 20 || data.Length > MaximumResponse) throw new InvalidDataException("Invalid helper response size.");
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                if (reader.ReadInt32() != Magic) throw new InvalidDataException("Invalid helper response.");
                var bytes = reader.ReadUInt64(); var completed = (CleanAreas)reader.ReadInt32(); var count = reader.ReadInt32();
                if (((int)completed & ~255) != 0 || count < 0 || count > 16) throw new InvalidDataException("Invalid helper result.");
                var errors = new List<string>();
                for (var i = 0; i < count; i++)
                {
                    var length = reader.ReadInt32();
                    if (length < 0 || length > 4000 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                        throw new InvalidDataException("Invalid helper error length.");
                    errors.Add(new UTF8Encoding(false, true).GetString(reader.ReadBytes(length)));
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Unexpected helper response data.");
                return new CleanupResult(bytes, errors, completed);
            }
        }
        internal static async Task<byte[]> ReadAsync(Stream stream, int count, CancellationToken cancellation)
        {
            if (count < 0 || count > MaximumResponse) throw new InvalidDataException("Invalid helper frame length.");
            var buffer = new byte[count]; var position = 0;
            while (position < count)
            {
                var received = await stream.ReadAsync(buffer, position, count - position, cancellation).ConfigureAwait(false);
                if (received == 0) throw new EndOfStreamException();
                position += received;
            }
            return buffer;
        }
    }
}
