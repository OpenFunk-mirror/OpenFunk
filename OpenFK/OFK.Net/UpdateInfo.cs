#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenFK.OFK.Common;

namespace OpenFK.OFK.Net
{
    using VariantProperties = Dictionary<string, string>;
    readonly struct Variant
    {
        /// <summary>
        /// If you write a mod over 18446 PiB, I will personally come to your house, replace all your batteries with dead ones,
        /// and switch all your light switches around to make you pay for your crimes against humanity
        /// </summary>
        public readonly ulong size;
        public readonly string link;
        public readonly VariantProperties properties;

        public Variant(string link, ulong size, VariantProperties? properties)
        {
            this.link = link ?? throw new ArgumentNullException(nameof(link));
            this.size = size;
            this.properties = properties ?? new VariantProperties();
        }

        public Variant(string link, ulong size) : this(link, size, null)
        { }
    }

    class UpdateInfo
    {
        public static readonly string[] SupportedUpdateXmlSchemaVersions = new[] { "1.0", "2.0" };
        private readonly XElement XmlRoot;

        public readonly string SchemaVersion;
        /// <summary>
        /// In the case of a permanent redirect being encountered, this is set to the new url,
        /// so you can update any links accordingly.
        /// </summary>
        public readonly string? NewUrl;
        public readonly string? Id;
        public readonly string? Name;
        public readonly string Version;
        public readonly string VersionName;

        public UpdateInfo(string xml) : this(XElement.Parse(xml))
        { }

        public UpdateInfo(XElement root)
        {
            XmlRoot = root;

        Parse:
            SchemaVersion = GetAttributeValue("schema_version", "1.0");
            if (!SupportedUpdateXmlSchemaVersions.Contains(SchemaVersion))
                LogManager.LogNetwork($"[Update] Update.xml has an unknown schema version ({SchemaVersion}), so we might be missing out on some details.", "NetCommand");

            if (SchemaVersion.StartsWith("1."))
            {
                // The original update.xml "spec" does not have redirects, so assume there are none
                Version = GetNonNullableAttributeValue("version");
                VersionName = GetNonNullableAttributeValue("name");
                return;
            }

            if (SchemaVersion.StartsWith("2."))
            {
                string? redirectType = GetAttributeValue("redirect_type");
                if (redirectType == "temporary" || redirectType == "permanent")
                {
                    string redirectLink = GetNonNullableAttributeValue("redirect_link");
                    if (redirectType == "permanent") NewUrl = redirectLink;

                    XmlRoot = XElement.Parse(HttpManager.HTTPGet(redirectLink));
                    goto Parse;
                }

                Id = GetAttributeValue("id");
                Name = GetAttributeValue("name");
                Version = GetNonNullableAttributeValue("version");
                VersionName = GetNonNullableAttributeValue("version_name");

                return;
            }
            
            throw new NotSupportedException($"Encountered unsupported Update.xml version ({SchemaVersion}). Supported versions are: {string.Join(", ", SupportedUpdateXmlSchemaVersions)}");
        }

        private bool? Is64BitVariant(Variant variant)
        {
            return Is64BitString(variant.properties?["Architecture"]);
        }

        private bool? Is64BitString(string? str)
        {
            if (str == null) return null;
            // x64, x86_64, 64-bit, amd64
            if (str.Contains("64")) return true;
            // x86, 32-bit, x32, i386
            if (str.Contains("32") || str.Contains("86")) return false;
            return null;
        }

        private IEnumerable<Variant> GetUnsortedVariants()
        {
            if (SchemaVersion.StartsWith("1.")) {
                List<Variant> variants = new();

                ulong size = (ulong)XmlRoot.Attribute("size");
                XAttribute url = XmlRoot.Attribute("url");
                XAttribute url32 = XmlRoot.Attribute("url32");
                XAttribute url64 = XmlRoot.Attribute("url64");

                if (url is not null)
                {
                    variants.Add(new Variant(
                        (string)url,
                        size,
                        new VariantProperties()
                    ));
                }

                if (url32 is not null)
                {
                    variants.Add(new Variant(
                        (string)url32,
                        size,
                        // we don't specify a build architecture because we can't know it for sure,
                        // even though it's usually Release
                        new VariantProperties { { "Architecture", "x86" } }
                    ));
                }

                if (url64 is not null)
                {
                    variants.Add(new Variant(
                        (string)url64,
                        size,
                        new VariantProperties { { "Architecture", "x64" } }
                    ));
                }

                return variants;
            }

            if (SchemaVersion.StartsWith("2."))
            {
                return XmlRoot
                    .Descendants("variant")
                    .Select(variant => new Variant(
                        (string)variant.Attribute("link"),
                        (ulong)variant.Attribute("size"),
                        variant.Descendants("Property")
                         .ToDictionary(
                            property => (string)property.Attribute("key"),
                            property => (string)property.Attribute("value")
                    )));
            }

            throw new NotSupportedException($"Encountered unsupported Update.xml version ({SchemaVersion}). Supported versions are: {string.Join(", ", SupportedUpdateXmlSchemaVersions)}");
        }

        /// <summary>
        /// Returns a list of variants offered, sorted in the order by which you should prefer them.
        /// </summary>
        public Variant[] GetVariants()
        {
            return GetUnsortedVariants()
                // Respect the user's architecture choice (eg wine users using 32-bit builds but running on a 64-bit system),
                // so don't check for 64-bit OS
                .Where(variant => Environment.Is64BitProcess || Is64BitVariant(variant) != true)
                .OrderBy(variant => variant.properties?["BuildConfiguration"] switch
                {
                    "Release" => 0,
                    "Debug" => 1,
                    _ => 2
                })
                .ThenBy(variant => Is64BitVariant(variant) switch
                {
                    // order: unknown (assume it's any), x64, x86
                    true => 1,
                    false => 2,
                    _ => 0
                })
                .ToArray();
        }

        private string? GetAttributeValue(XElement element, string attribute)
        {
            return element.Attribute(attribute)?.Value;
        }

        private string GetAttributeValue(XElement element, string attribute, string defaultValue)
        {
            return GetAttributeValue(element, attribute) ?? defaultValue;
        }

        private string GetNonNullableAttributeValue(XElement element, string attribute)
        {
            return GetAttributeValue(element, attribute) ?? throw new ArgumentNullException($"Update.xml parsing error: {attribute} may not be null!");
        }

        private string? GetAttributeValue(string attribute) => GetAttributeValue(XmlRoot, attribute);
        private string GetAttributeValue(string attribute, string defaultValue) => GetAttributeValue(XmlRoot, attribute, defaultValue);
        private string GetNonNullableAttributeValue(string attribute) => GetNonNullableAttributeValue(XmlRoot, attribute);

        public static UpdateInfo Fetch(string uri)
        {
            LogManager.LogNetwork($"[Update] Downloading {uri}", "NetCommand");
            string updateXml = HttpManager.HTTPGet(uri);
            LogManager.LogNetwork("[Update] Update.xml was downloaded", "NetCommand");
            return new UpdateInfo(updateXml);
        }
    }
}
