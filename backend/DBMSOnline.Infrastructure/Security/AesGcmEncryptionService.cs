using System;
using System.Security.Cryptography;
using System.Text;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Infrastructure.Security;

public class AesGcmEncryptionService : IEncryptionService
{
    private readonly byte[] _masterKey;

    public AesGcmEncryptionService(string masterSecret = "DBMSOnline_Default_Master_Key_2026_Secure!")
    {
        var salt = Encoding.UTF8.GetBytes("DBMSOnline_Salt_2026_Fixed_App_Salt");
        _masterKey = Rfc2898DeriveBytes.Pbkdf2(masterSecret, salt, 100000, HashAlgorithmName.SHA256, 32);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] nonce = RandomNumberGenerator.GetBytes(12); // 96-bit nonce
        byte[] tag = new byte[16]; // 128-bit tag
        byte[] cipherText = new byte[plainBytes.Length];

        using (var aesGcm = new AesGcm(_masterKey, 16))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);
        }

        // Output format: Nonce (12B) + Tag (16B) + Ciphertext
        byte[] resultBytes = new byte[nonce.Length + tag.Length + cipherText.Length];
        Buffer.BlockCopy(nonce, 0, resultBytes, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, resultBytes, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherText, 0, resultBytes, nonce.Length + tag.Length, cipherText.Length);

        // Clear plainBytes buffer from memory
        CryptographicOperations.ZeroMemory(plainBytes);

        return Convert.ToBase64String(resultBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        byte[] fullBytes = Convert.FromBase64String(cipherText);
        if (fullBytes.Length < 28) // 12 (nonce) + 16 (tag)
            throw new ArgumentException("Invalid ciphertext payload length.");

        byte[] nonce = new byte[12];
        byte[] tag = new byte[16];
        byte[] cipherBytes = new byte[fullBytes.Length - 28];

        Buffer.BlockCopy(fullBytes, 0, nonce, 0, 12);
        Buffer.BlockCopy(fullBytes, 12, tag, 0, 16);
        Buffer.BlockCopy(fullBytes, 28, cipherBytes, 0, cipherBytes.Length);

        byte[] plainBytes = new byte[cipherBytes.Length];

        using (var aesGcm = new AesGcm(_masterKey, 16))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }

        string result = Encoding.UTF8.GetString(plainBytes);
        CryptographicOperations.ZeroMemory(plainBytes);
        return result;
    }

    public byte[] HashMasterKey(string masterKey, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(masterKey, salt, 100000, HashAlgorithmName.SHA256, 32);
    }
}
