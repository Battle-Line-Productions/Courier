namespace Courier.Domain.Encryption;

public interface ICredentialEncryptor
{
    byte[] Encrypt(string plaintext);
    string Decrypt(byte[] ciphertext);
}
