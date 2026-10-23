#if !HELPER
#nullable disable // Shared with the protected Windows .NET Framework helper.
#endif
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MemReduct.Fluent
{
    internal sealed class AppRelease
    {
        public Version Version { get; private set; }
        public Uri Download { get; private set; }
        public string Sha256 { get; private set; }
        public long Size { get; private set; }
        internal AppRelease(Version version, Uri download, string sha256, long size)
        { Version = version; Download = download; Sha256 = sha256; Size = size; }
    }

    // Both processes validate the same fixed release contract; the helper never trusts UI metadata.
    internal static class UpdatePackage
    {
        internal const string Repository = "https://github.com/Duye0120/memreduct-winui";
        internal const string LatestApi = "https://api.github.com/repos/Duye0120/memreduct-winui/releases/latest";
        internal const int MaximumMetadata = 1024 * 1024;
        internal const long MaximumDownload = 512L * 1024 * 1024;

        internal static AppRelease ParseRelease(string json, Version current)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > MaximumMetadata) throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
            ReleaseMetadata release;
            using (var stream = new MemoryStream(bytes))
                release = (ReleaseMetadata)new DataContractJsonSerializer(typeof(ReleaseMetadata)).ReadObject(stream);
            if (release == null) throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
            if (release.Draft || release.Prerelease) return null;
            var tag = release.Tag ?? "";
            if (tag == "fluent-v1.0.0" || tag == "fluent-v1.0.1" || tag == "fluent-v1.0.2") return null;
            var match = Regex.Match(tag, @"^fluent-v(\d+\.\d+\.\d+)$", RegexOptions.CultureInvariant);
            Version version;
            if (!match.Success || !Version.TryParse(match.Groups[1].Value, out version)) return null;
            if (new Version(version.Major, version.Minor, version.Build, 0) <= current) return null;
            var name = "MemReductFluent-" + version + "-win-x64-setup.exe";
            var assets = (release.Assets ?? new AssetMetadata[0]).Where(candidate => candidate != null && candidate.Name == name).ToArray();
            if (assets.Length != 1) throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
            var asset = assets[0];
            var expected = Repository + "/releases/download/" + tag + "/" + name;
            if (asset.Download != expected || asset.Size <= 0 || asset.Size > MaximumDownload || asset.Digest == null
                || !Regex.IsMatch(asset.Digest, @"^sha256:[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant))
                throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
            return new AppRelease(version, new Uri(expected), asset.Digest.Substring(7), asset.Size);
        }

        internal static async Task CopyVerifiedAsync(Stream source, string destinationPath, AppRelease release,
            CancellationToken token, IProgress<long> progress = null)
        {
            var created = false;
            try
            {
                using (var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                using (var hash = SHA256.Create())
                {
                    created = true;
                    var buffer = new byte[81920];
                    long total = 0;
                    var lastPercent = 0;
                    if (progress != null) progress.Report(0);
                    int count;
                    while ((count = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                    {
                        total += count;
                        if (total > release.Size) throw new InvalidDataException(UiText.Get("UpdateHashFailed"));
                        hash.TransformBlock(buffer, 0, count, buffer, 0);
                        await destination.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
                        var percent = (int)(total * 100 / release.Size);
                        if (percent > lastPercent && total < release.Size)
                        {
                            lastPercent = percent;
                            if (progress != null) progress.Report(total);
                        }
                    }
                    token.ThrowIfCancellationRequested();
                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    if (total != release.Size || !DigestMatches(hash.Hash, release.Sha256))
                        throw new InvalidDataException(UiText.Get("UpdateHashFailed"));
                    if (progress != null) progress.Report(total);
                }
            }
            catch { if (created && File.Exists(destinationPath)) File.Delete(destinationPath); throw; }
        }

        internal static bool DigestMatches(byte[] hash, string expected)
        {
            var actual = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            if (expected == null || actual.Length != expected.Length) return false;
            var difference = 0;
            for (var index = 0; index < actual.Length; index++) difference |= actual[index] ^ char.ToLowerInvariant(expected[index]);
            return difference == 0;
        }

        [DataContract]
        private sealed class ReleaseMetadata
        {
            [DataMember(Name = "draft", IsRequired = true)] public bool Draft { get; set; }
            [DataMember(Name = "prerelease", IsRequired = true)] public bool Prerelease { get; set; }
            [DataMember(Name = "tag_name", IsRequired = true)] public string Tag { get; set; }
            [DataMember(Name = "assets")] public AssetMetadata[] Assets { get; set; }
        }
        [DataContract]
        private sealed class AssetMetadata
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "browser_download_url")] public string Download { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
            [DataMember(Name = "size")] public long Size { get; set; }
        }
    }
}
