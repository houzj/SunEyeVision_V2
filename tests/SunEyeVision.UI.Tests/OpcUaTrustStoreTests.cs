using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class OpcUaTrustStoreTests
{
    [Fact]
    public void Explicitly_trusted_certificate_is_listed_and_can_be_revoked()
    {
        var root = Path.Combine(Path.GetTempPath(), "SunEyeVision-OpcUa-Trust", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "server.cer");
        Directory.CreateDirectory(root);
        try
        {
            using var certificate = CreateCertificate(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
            File.WriteAllBytes(source, certificate.Export(X509ContentType.Cert));
            var store = new OpcUaTrustStore(Path.Combine(root, "pki"));

            var trusted = store.TrustFile(source);

            var listed = Assert.Single(store.List());
            Assert.Equal(trusted.Thumbprint, listed.Thumbprint);
            Assert.True(store.Remove(trusted.Thumbprint));
            Assert.Empty(store.List());
            Assert.False(store.Remove(trusted.Thumbprint));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void Expired_certificate_is_rejected_without_being_trusted()
    {
        var root = Path.Combine(Path.GetTempPath(), "SunEyeVision-OpcUa-Trust", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "expired.cer");
        Directory.CreateDirectory(root);
        try
        {
            using var certificate = CreateCertificate(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1));
            File.WriteAllBytes(source, certificate.Export(X509ContentType.Cert));
            var store = new OpcUaTrustStore(Path.Combine(root, "pki"));

            Assert.Throws<InvalidDataException>(() => store.TrustFile(source));
            Assert.Empty(store.List());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static X509Certificate2 CreateCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=OPC UA Trust Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(notBefore, notAfter);
    }
}
