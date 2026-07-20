using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static readonly byte[] Pkcs7SignedDataOid = { 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x02 };
        private static readonly byte[] Pkcs7DataOid = { 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x01 };

        private static string SecurityContainerTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".p7x" && StartsWith(header, Encoding.ASCII.GetBytes("PKCX")) && IndexOfBytes(header, Pkcs7SignedDataOid) >= 0)
                return "Windows AppX PKCX signature container";
            if ((ext == ".p7b" || ext == ".p7c") && LooksLikeDerSequence(header) && IndexOfBytes(header, Pkcs7SignedDataOid) >= 0)
                return "PKCS #7 certificate bundle";
            if ((ext == ".cer" || ext == ".crt" || ext == ".der") && LooksLikeDerCertificate(header))
                return "X.509 certificate";
            if ((ext == ".pfx" || ext == ".p12") && LooksLikePkcs12(header))
                return "PKCS #12 personal information exchange";
            if (ext == ".pem" && PemBlockKinds(header).Length > 0)
                return "PEM certificate or key bundle";
            return null;
        }

        private static void AddSecurityContainerInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = SecurityContainerTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Certificate / signature data");
            Add(section, "Format", type);
            Add(section, "File size", FormatBytes(fileLength));
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".p7x")
            {
                Add(section, "Header marker", "PKCX");
                Add(section, "Role", "Signed-data container used for Windows AppX/MSIX package signatures.");
            }
            else if (ext == ".p7b" || ext == ".p7c")
            {
                Add(section, "Container", "ASN.1 DER PKCS #7 SignedData");
                Add(section, "Common contents", "One or more public certificates and certificate-chain records.");
            }
            else if (ext == ".cer" || ext == ".crt" || ext == ".der")
            {
                AddCertificateMetadata(section, path);
            }
            else if (ext == ".pfx" || ext == ".p12")
            {
                Add(section, "Container", "ASN.1 DER PKCS #12/PFX");
                Add(section, "Sensitive content", "May contain private keys and certificates protected by a password.");
                Add(section, "Safety note", "FileDentify does not request a password, import the bundle, or inspect private keys.");
            }
            else if (ext == ".pem")
            {
                var kinds = PemBlockKinds(header);
                Add(section, "PEM block types in sample", string.Join(Environment.NewLine, kinds));
                Add(section, "PEM blocks in sample", CountPemBlocks(header).ToString(CultureInfo.InvariantCulture));
                if (kinds.Any(kind => kind.IndexOf("PRIVATE KEY", StringComparison.OrdinalIgnoreCase) >= 0))
                    Add(section, "Sensitive content", "Private-key block marker found. Treat this file as confidential.");
            }
            Add(section, "Notes", "Certificate and signature containers are reported from standard wrapper markers and safe public metadata only. FileDentify does not validate trust, install certificates, or expose private-key payloads.");
        }

        private static string SecurityContainerDetectionBasis(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".p7x") return ".p7x extension plus PKCX and PKCS #7 SignedData markers";
            if (ext == ".p7b" || ext == ".p7c") return ext + " extension plus ASN.1 DER and PKCS #7 SignedData markers";
            if (ext == ".cer" || ext == ".crt" || ext == ".der") return ext + " extension plus nested ASN.1 DER certificate structure";
            if (ext == ".pfx" || ext == ".p12") return ext + " extension plus PKCS #12 version and data-object markers";
            if (ext == ".pem") return ".pem extension plus BEGIN/END encapsulation markers";
            return null;
        }

        private static bool LooksLikeDerSequence(byte[] header)
        {
            return header != null && header.Length >= 8 && header[0] == 0x30 && (header[1] & 0x80) != 0;
        }

        private static bool LooksLikeDerCertificate(byte[] header)
        {
            if (!LooksLikeDerSequence(header))
                return false;
            var outerHeader = 2 + (header[1] & 0x7F);
            return outerHeader > 2 && outerHeader < header.Length && header[outerHeader] == 0x30;
        }

        private static bool LooksLikePkcs12(byte[] header)
        {
            if (!LooksLikeDerSequence(header) || IndexOfBytes(header, Pkcs7DataOid) < 0)
                return false;
            return IndexOfBytes(header.Take(Math.Min(header.Length, 32)).ToArray(), new byte[] { 0x02, 0x01, 0x03 }) >= 0;
        }

        private static string[] PemBlockKinds(byte[] header)
        {
            var text = Encoding.ASCII.GetString(header ?? new byte[0]);
            return Regex.Matches(text, @"-----BEGIN ([A-Z0-9 ]+)-----", RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
        }

        private static int CountPemBlocks(byte[] header)
        {
            return Regex.Matches(Encoding.ASCII.GetString(header ?? new byte[0]), @"-----BEGIN [A-Z0-9 ]+-----", RegexOptions.CultureInvariant).Count;
        }

        private static void AddCertificateMetadata(ReportSection section, string path)
        {
            try
            {
                using (var certificate = new X509Certificate2(path))
                {
                    Add(section, "Subject", certificate.Subject);
                    Add(section, "Issuer", certificate.Issuer);
                    Add(section, "Valid from", certificate.NotBefore.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    Add(section, "Valid until", certificate.NotAfter.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    Add(section, "Public-key algorithm", certificate.PublicKey.Oid.FriendlyName ?? certificate.PublicKey.Oid.Value);
                    Add(section, "Signature algorithm", certificate.SignatureAlgorithm.FriendlyName ?? certificate.SignatureAlgorithm.Value);
                }
            }
            catch
            {
                Add(section, "Container", "ASN.1 DER certificate data");
            }
        }

    }
}
