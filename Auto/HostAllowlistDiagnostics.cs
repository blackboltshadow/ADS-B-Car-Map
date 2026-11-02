using System.Globalization;
using System.Security.Cryptography;
using Android.Content;
using Android.Content.PM;

namespace AdsbCarMap.Auto
{
    /// <summary>
    /// Checks the allowlist of the hosts against the hosts that are installed on the phone and writes the result to the log, so that a wrong entry can be found without a car. The check follows the Car App library: a host is accepted when the SHA-256 digest of any certificate from its signing history is on the list.
    /// </summary>
    internal static class HostAllowlistDiagnostics
    {
        /// <summary>
        /// Tag used in the Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Writes to the log, for every entry of the allowlist, whether the host is installed and whether its certificate matches the digest.
        /// </summary>
        /// <param name="context">Context used to reach the package manager.</param>
        /// <param name="entries">Entries of the allowlist, each "digest,package-name".</param>
        public static void Log(Context context, IReadOnlyList<string> entries)
        {
            foreach (string entry in entries)
            {
                string[] parts = entry.Split(',', 2);

                if (parts.Length != 2)
                {
                    Android.Util.Log.Error(LogTag, "Host allowlist: malformed entry '" + entry + "'");
                    continue;
                }

                string digest = parts[0].Trim().ToLowerInvariant();
                string package = parts[1].Trim();
                List<string> installedDigests = GetInstalledDigests(context, package);

                if (installedDigests.Count == 0)
                {
                    Android.Util.Log.Info(LogTag, "Host allowlist: " + package + " is not installed on this phone, so it cannot be checked");
                }
                else if (installedDigests.Contains(digest))
                {
                    Android.Util.Log.Info(LogTag, "Host allowlist: " + package + " is installed and its certificate matches the list");
                }
                else
                {
                    Android.Util.Log.Warn(LogTag, "Host allowlist: " + package + " is installed but its certificate does NOT match the list. Digests of the installed host: " + string.Join(", ", installedDigests));
                }
            }
        }

        /// <summary>
        /// Returns the SHA-256 digests, in the form used by the Car App library, of the certificates of an installed package.
        /// </summary>
        /// <param name="context">Context used to reach the package manager.</param>
        /// <param name="package">Package name.</param>
        /// <returns>The digests, empty when the package is not installed or has no readable certificate.</returns>
        private static List<string> GetInstalledDigests(Context context, string package)
        {
            List<string> digests = new List<string>();

            try
            {
                PackageManager? manager = context.PackageManager;

                if (manager == null)
                {
                    return digests;
                }

                Android.Content.PM.Signature[]? signatures = null;

                if (OperatingSystem.IsAndroidVersionAtLeast(28))
                {
                    PackageInfo? info = manager.GetPackageInfo(package, PackageInfoFlags.SigningCertificates);
                    signatures = info?.SigningInfo?.GetSigningCertificateHistory();
                }

                foreach (Android.Content.PM.Signature signature in signatures ?? Array.Empty<Android.Content.PM.Signature>())
                {
                    digests.Add(Convert.ToHexString(SHA256.HashData(signature.ToByteArray()!)).ToLower(CultureInfo.InvariantCulture));
                }
            }
            catch (PackageManager.NameNotFoundException)
            {
            }

            return digests;
        }
    }
}
