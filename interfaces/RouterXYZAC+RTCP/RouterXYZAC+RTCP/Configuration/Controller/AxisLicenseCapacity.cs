using System.Buffers.Binary;
using System.Security.Cryptography;

namespace RouterXYZACRTCP;

/// <summary>Offline admission only. Core still authenticates the adapter and board before motion.</summary>
internal static class AxisLicenseCapacity
{
    // Same approved PUBLIC point as NT-Security/Native/RadonixTrust.h.
    internal const string PublicPointHex =
        "61F817B9E5A2203ADDB71FACAB80973C9C2816036426187B8FBDCF33B8DBD7AF" +
        "E569C095D58485E082C07269090E22ACE993A37224C5AF34ADED0B7A471707A2";

    internal static int? Read(string text)
        => Read(text, Convert.FromHexString(PublicPointHex));

    internal static int? Read(string text, ReadOnlySpan<byte> publicPoint)
    {
        if (text.Length > 4096 || publicPoint.Length != 64) return null;
        string compact = string.Concat(text.Where(c => c is not (' ' or '\t' or '\r' or '\n')));
        if (!compact.StartsWith("RNT3.", StringComparison.Ordinal)) return null;
        string encoded = compact[5..];
        if (encoded.Length is not (166 or 182 or 198)
            || encoded.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) return null;
        try
        {
            byte[] envelope = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/')
                + new string('=', (4 - encoded.Length % 4) % 4));
            if (Convert.ToBase64String(envelope).TrimEnd('=').Replace('+', '-').Replace('/', '_') != encoded
                || envelope.Length < 124 || !envelope.AsSpan(0, 4).SequenceEqual("RNTL"u8)
                || envelope[4] != 3 || envelope[5] > 16 || envelope[7] != 0
                || envelope[6] is not (0 or 12 or 24) || envelope.Length != 124 + envelope[6]
                || BinaryPrimitives.ReadUInt16BigEndian(envelope.AsSpan(58, 2)) is < 1000 or > 9999
                || BinaryPrimitives.ReadInt64BigEndian(envelope.AsSpan(24, 8)) < 0
                || new Guid(envelope.AsSpan(8, 16)) == Guid.Empty) return null;
            using ECDsa key = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = publicPoint[..32].ToArray(), Y = publicPoint[32..].ToArray() }
            });
            byte[] fingerprint = SHA256.HashData(key.ExportSubjectPublicKeyInfo());
            if (!envelope.AsSpan(42, 16).SequenceEqual(fingerprint.AsSpan(0, 16))) return null;
            int payloadLength = envelope.Length - 64;
            return key.VerifyData(envelope.AsSpan(0, payloadLength), envelope.AsSpan(payloadLength),
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
                ? envelope[5] : null;
        }
        catch (Exception error) when (error is FormatException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }
}
