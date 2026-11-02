using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the allowlists of the Android Auto hosts in Resources/values/allowed_hosts.xml. A malformed entry is rejected by the Car App library only when the car connects, so the format is checked here.
    /// </summary>
    public class AllowedHostsTests
    {
        /// <summary>
        /// Format of an entry: 64 lowercase hexadecimal digits without colons, a comma and a package name.
        /// </summary>
        private static readonly Regex EntryFormat = new Regex("^[0-9a-f]{64},[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Reads the entries of one list.
        /// </summary>
        /// <param name="name">Name of the string-array.</param>
        /// <returns>The entries as written in the file.</returns>
        private static List<string> Read(string name)
        {
            XDocument document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "allowed_hosts.xml"));
            XElement? array = document.Root!.Elements("string-array").FirstOrDefault(element => (string?)element.Attribute("name") == name);

            Assert.NotNull(array);

            return array.Elements("item").Select(item => item.Value).ToList();
        }

        /// <summary>
        /// Both lists exist and every entry has the format that the library expects.
        /// </summary>
        [Theory]
        [InlineData("hosts_allowlist_release")]
        [InlineData("hosts_allowlist_dev")]
        public void Lists_HaveWellFormedEntriesWithoutDuplicates(string name)
        {
            List<string> entries = Read(name);

            Assert.NotEmpty(entries);
            Assert.All(entries, entry => Assert.Matches(EntryFormat, entry));
            Assert.Equal(entries.Count, entries.Distinct().Count());
        }

        /// <summary>
        /// The release list holds Android Auto and the development list holds everything from the release list.
        /// </summary>
        [Fact]
        public void ReleaseList_IsPartOfTheDevelopmentList_AndHoldsAndroidAuto()
        {
            List<string> release = Read("hosts_allowlist_release");
            List<string> development = Read("hosts_allowlist_dev");

            Assert.Contains(release, entry => entry.EndsWith(",com.google.android.projection.gearhead", StringComparison.Ordinal));
            Assert.All(release, entry => Assert.Contains(entry, development));
        }

        /// <summary>
        /// The release list never allows everybody: it has no entry for an unknown package and no empty digest.
        /// </summary>
        [Fact]
        public void ReleaseList_HasOnlyKnownGoogleHosts()
        {
            string[] knownPackages = { "com.google.android.projection.gearhead", "com.google.android.apps.automotive.templates.host" };

            Assert.All(Read("hosts_allowlist_release"), entry => Assert.Contains(entry.Split(',')[1], knownPackages));
        }
    }
}
