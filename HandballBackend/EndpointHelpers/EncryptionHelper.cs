using System.Security.Cryptography;
using System.Text;

namespace HandballBackend.EndpointHelpers;

public static class EncryptionHelper {
    private static byte[] Key() {
        return Convert.FromBase64String(
            File.ReadAllText(Config.SECRETS_FOLDER + "/PhoneNumberKey.txt")
        );
    }

    public static string Encrypt(string plaintext) {
        using var aesAlg = Aes.Create();
        aesAlg.Key = Key();
        var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using var msEncrypt = new MemoryStream();
        msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)) {
            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            csEncrypt.Write(plainBytes, 0, plainBytes.Length);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public static string Decrypt(string ciphertext) {
        var cipherBytes = Convert.FromBase64String(ciphertext);
        using var aesAlg = Aes.Create();
        aesAlg.Key = Key();
        aesAlg.IV = cipherBytes.Take(aesAlg.IV.Length).ToArray();
        using var msDecrypt = new MemoryStream(cipherBytes.Skip(aesAlg.IV.Length).ToArray());
        var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
        byte[] decryptedBytes;
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using (var msPlain = new MemoryStream()) {
            csDecrypt.CopyTo(msPlain);
            decryptedBytes = msPlain.ToArray();
        }

        return Encoding.UTF8.GetString(decryptedBytes);
    }
}