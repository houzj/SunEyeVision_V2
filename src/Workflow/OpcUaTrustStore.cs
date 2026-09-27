using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SunEyeVision.Workflow;

/// <summary>Explicit, leaf-certificate trust management for OPC UA server peers.</summary>
public sealed class OpcUaTrustStore
{
    private readonly string _certificatesDirectory;

    public OpcUaTrustStore(string pkiRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pkiRoot);
        _certificatesDirectory = Path.Combine(Path.GetFullPath(pkiRoot), "UA Applications", "certs");
    }

    public IReadOnlyList<TrustedOpcUaCertificate> List()
    {
        if (!Directory.Exists(_certificatesDirectory)) return Array.Empty<TrustedOpcUaCertificate>();

        return Directory.EnumerateFiles(_certificatesDirectory, "*.der")
            .Select(path => TryRead(path))
            .Where(certificate => certificate is not null)
            .Select(certificate => certificate!)
            .OrderBy(certificate => certificate.Subject, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public TrustedOpcUaCertificate TrustFile(string certificatePath)
    {
        var inspected = InspectFile(certificatePath);
        using var certificate = LoadCertificate(Path.GetFullPath(certificatePath));

        Directory.CreateDirectory(_certificatesDirectory);
        var target = GetCertificatePath(inspected.Thumbprint);
        if (!File.Exists(target))
            File.WriteAllBytes(target, certificate.Export(X509ContentType.Cert));

        return inspected;
    }

    public static TrustedOpcUaCertificate InspectFile(string certificatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        var source = Path.GetFullPath(certificatePath);
        if (!File.Exists(source)) throw new FileNotFoundException("找不到 OPC UA 服务器证书文件。", source);

        using var certificate = LoadCertificate(source);
        var now = DateTime.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
            throw new InvalidDataException("证书尚未生效或已经过期，不能加入信任列表。");
        if (string.IsNullOrWhiteSpace(certificate.Thumbprint))
            throw new InvalidDataException("证书指纹无效，不能加入信任列表。");
        return ToInfo(certificate);
    }

    public bool Remove(string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        var normalized = new string(thumbprint.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length is not (40 or 64)) throw new ArgumentException("证书指纹格式无效。", nameof(thumbprint));
        var path = GetCertificatePath(normalized);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private string GetCertificatePath(string thumbprint) => Path.Combine(_certificatesDirectory, $"{thumbprint.ToUpperInvariant()}.der");

    private static X509Certificate2 LoadCertificate(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try
        {
            if (bytes.AsSpan().StartsWith("-----BEGIN CERTIFICATE-----"u8))
                return X509Certificate2.CreateFromPem(File.ReadAllText(path));
            return new X509Certificate2(bytes);
        }
        finally { Array.Clear(bytes); }
    }

    private static TrustedOpcUaCertificate? TryRead(string path)
    {
        try
        {
            using var certificate = new X509Certificate2(File.ReadAllBytes(path));
            return ToInfo(certificate);
        }
        catch (CryptographicException) { return null; }
        catch (IOException) { return null; }
    }

    private static TrustedOpcUaCertificate ToInfo(X509Certificate2 certificate) =>
        new(certificate.Subject, certificate.Thumbprint ?? string.Empty, certificate.NotAfter.ToUniversalTime());
}

public sealed record TrustedOpcUaCertificate(string Subject, string Thumbprint, DateTime ExpiresUtc);
