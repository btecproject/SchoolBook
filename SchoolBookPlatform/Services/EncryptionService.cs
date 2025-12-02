using System.Security.Cryptography;
using System.Text;

namespace SchoolBookPlatform.Services
{
    public class EncryptionService
    {
        public string Encrypt(string plainText, byte[] key)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[16];

            using var aes = new AesGcm(key);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
            
            byte[] encrypted = new byte[nonce.Length + ciphertext.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, encrypted, 0, nonce.Length);
            Buffer.BlockCopy(ciphertext, 0, encrypted, nonce.Length, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, encrypted, nonce.Length + ciphertext.Length, tag.Length);

            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string encryptedText, byte[] key)
        {
            byte[] encrypted = Convert.FromBase64String(encryptedText);
            byte[] nonce = new byte[12];
            byte[] ciphertext = new byte[encrypted.Length - 28];  // Total - nonce - tag
            byte[] tag = new byte[16];

            Buffer.BlockCopy(encrypted, 0, nonce, 0, 12);
            Buffer.BlockCopy(encrypted, 12, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(encrypted, 12 + ciphertext.Length, tag, 0, 16);

            byte[] plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(key);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
    }
}