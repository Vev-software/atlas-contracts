using System.Security.Cryptography;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// RSA helpers for the landscape share digest detached signature. The conformance fixtures use a
/// fixed, documented 2048-bit RSA key so the signature bytes are stable test vectors (RSA PKCS#1
/// v1.5 signatures are deterministic). The key is a test vector, not a secret: it is committed on
/// purpose so the fixtures can be regenerated and verified from public feeds only. Production key
/// distribution is an enrollment concern and out of scope for this repository.
/// </summary>
internal static class DigestSignature
{
    /// <summary>The key id carried by the conformance fixtures.</summary>
    public const string TestKeyId = "atlas-conformance-test-key";

    /// <summary>The signature algorithm identifier carried by the conformance fixtures.</summary>
    public const string Algorithm = "rsa-pkcs1-v1_5-sha256";

    /// <summary>
    /// The fixed 2048-bit RSA test key (PKCS#8, base64url). A documented test vector, not a secret.
    /// </summary>
    public const string TestPrivateKeyPkcs8Base64Url =
        "MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDIQr-XecBeLaGeNmkL9zPOcQ4meum6UE3EY_4oedWH_mxxo06gx-qugB9IxjNXuCyQoxfyoMeL1EAi52qeQEoUphS2DkZnhWGaLK2pA0S3VHFxPi4cCOkfzLDEF_g2GDZQqWV4T0modXc-9t8DGtcpHH04TJ-75UM5_yZVzDiPVbB9EY3p_LXyULhHuaQ-u8xWX8KD-vyN7JefJCQTcYmaZXHudx-eNtXqZuDn1Ad3SGnDaEFkGN89rVyPB-XOEKHF33V41n84UERPuANQUcsZZHa42ZTOVAQqCoZ56iXj0Q9CPSm3SrFU6_wruYkfQsr8EAi3MoWjyHGMpTattFzpAgMBAAECggEAPMcVQrGu0mZi8JNu2jTSQktJkiAno2YP1BTM5Bfl2Ho9C1gW2vERppg35mxQ1MSOse7tq7NkHGn0dSNq1lCIqy-khYRblbuDXblFk0_soP891rKaZ-PEbORAmaL0r-Y1RdHDe_oETt4nrLJcCKMyvcVps5Z9vBbeilGHnu52E_nRVmkcW6gSwihnJTk9lOkTKXRxYzRBo5CSLoAEfxfE8ohUptG863AZjsDhzMpi_YtRoM2uueKAxNpQMr185rUINeeho_4zrpJqNWiKVyVyQbCU_gRhgx4RT31dB1-ER4UOh5nmcvtxPHw9eY0jJelUMLUsv-k2JnfJesf85L7yaQKBgQDJwUugu4u8aC4SZyfinMQJTUF11duR7Xk2-pIjTM8U_P7LHvKUf9RsezOAKNiQWZ5SSmj1EtOnpB5e7J9pEaWcMFfNbNgXPfxY2sEwN9corb8vCOrew3L51CnwOCmy--L2wHxALJrfOX_Ysh4RUsspZOl6ZcdamG9G2lg2EH8l5wKBgQD-GplvZe9B8pr_scQl5OnIgwKDdnlYw7AusL5Xyz0pcg-7mFKiRV6qoHbkFXIjBN8NglMXz2n-VUcogZ4PYh4RhCFxRgsGEpnS5tXTvkHu9hlJ3hhd9hXUshnHLp5lr-zIQwqisIc_yvyZz87W0rdkTJx1l5c2ENuZnBEhtVRsrwKBgCXb-d8dkmk2e37EkL17gWXlc2UzTlKh518jwgyRu0JK_10KX6byHeCFdfKUt57O0mDKyctDTyhkKbbSXE-RGiym_bL0u9AQt6vM0PfFYsClafzfhYNr1cV_xKbpQxntHngDQs2gESfWWGFHe1Dw4mLQJufq9W_QrwDytB7hFZr9AoGBAMWwlgzH5lONVq91ct_0bzmzS0JR6uh3HlvE3-FX2lajScAuKqPaimL_AmBCmxDJmmtdKB5PHOxlFj5aUUkwkaoY_ReuYacw04H0WwkobQ1nY5dHdfesG6Hlig6fo1YDf5jyJ5UY97BW0R0tQoN5Xnpm7BbEgDzs8pxYgi-SboM7AoGAYGcKrGQlYvMISHFZkGKG_dF9cDieWCMmqfxXs3zSdvFEo88_5yqnjZb2kUv3qItFt4-SGdJqXrKRQbctiuxmFMhwIOvNKFXLSv2Pf4RwweCz5w6KkcxZDXwktFKSU2sL6j2GfWLv5TEgtsyzfEtdSE_rgESz7XXTe0myf7uS2UA";

    /// <summary>
    /// The fixed test key's SubjectPublicKeyInfo (base64url) as carried by the conformance fixtures.
    /// </summary>
    public const string TestPublicKeySpkiBase64Url =
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAyEK_l3nAXi2hnjZpC_czznEOJnrpulBNxGP-KHnVh_5scaNOoMfqroAfSMYzV7gskKMX8qDHi9RAIudqnkBKFKYUtg5GZ4VhmiytqQNEt1RxcT4uHAjpH8ywxBf4Nhg2UKlleE9JqHV3PvbfAxrXKRx9OEyfu-VDOf8mVcw4j1WwfRGN6fy18lC4R7mkPrvMVl_Cg_r8jeyXnyQkE3GJmmVx7ncfnjbV6mbg59QHd0hpw2hBZBjfPa1cjwflzhChxd91eNZ_OFBET7gDUFHLGWR2uNmUzlQEKgqGeeol49EPQj0pt0qxVOv8K7mJH0LK_BAItzKFo8hxjKU2rbRc6QIDAQAB";

    /// <summary>Signs a message with the conformance test key (SHA-256, PKCS#1 v1.5).</summary>
    public static byte[] Sign(byte[] message)
    {
        using var rsa = CreateTestKey();
        return rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>Verifies a signature over a message with the given SubjectPublicKeyInfo public key.</summary>
    public static bool Verify(byte[] publicKeySpki, byte[] signature, byte[] message)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(publicKeySpki, out _);
        return rsa.VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static RSA CreateTestKey()
    {
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(FromBase64Url(TestPrivateKeyPkcs8Base64Url), out _);
        return rsa;
    }

    public static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string text) =>
        Convert.FromBase64String(text.PadRight(text.Length + ((4 - text.Length % 4) % 4), '=').Replace('-', '+').Replace('_', '/'));
}
