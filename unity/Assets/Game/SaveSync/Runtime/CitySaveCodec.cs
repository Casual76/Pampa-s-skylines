namespace PampaSkylines.SaveSync
{
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PampaSkylines.Core;

public static class CitySaveCodec
{
    public static byte[] Encode(CitySnapshot snapshot)
    {
        snapshot = CitySnapshotMigrator.MigrateToCurrent(snapshot);
        snapshot.ContentHash = SnapshotHashing.ComputeContentHash(snapshot);
        var json = PampaSkylinesJson.Serialize(snapshot);
        var payload = Encoding.UTF8.GetBytes(json);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(payload, 0, payload.Length);
        }

        return output.ToArray();
    }

    public static CitySnapshot Decode(byte[] compressedPayload)
    {
        using var input = new MemoryStream(compressedPayload);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var json = reader.ReadToEnd();
        var snapshot = PampaSkylinesJson.Deserialize<CitySnapshot>(json) ?? new CitySnapshot();
        snapshot = CitySnapshotMigrator.MigrateToCurrent(snapshot);

        var expectedHash = snapshot.ContentHash;
        var computedHash = SnapshotHashing.ComputeContentHash(snapshot);
        if (!string.IsNullOrWhiteSpace(expectedHash) && !string.Equals(expectedHash, computedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Snapshot checksum mismatch. Expected '{expectedHash}' but got '{computedHash}'.");
        }

        snapshot.ContentHash = computedHash;
        return snapshot;
    }

    public static async Task WriteToFileAsync(string path, CitySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var bytes = Encode(snapshot);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
    }

    public static async Task<CitySnapshot> ReadFromFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return Decode(bytes);
    }
}
}
