using ActiveForge.Attributes;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base for field-level encryption algorithms.
    /// Implement to encrypt/decrypt values as they flow between the ORM and the database.
    /// </summary>
    public abstract class EncryptionAlgorithm
    {
        /// <summary>Returns the maximum byte length that the encrypted form of a field can occupy.</summary>
        public abstract int GetMaxFieldLength();

        /// <summary>Encrypts a plaintext value for storage in the database.</summary>
        public abstract object Encrypt(object plaintext);

        /// <summary>Decrypts a value retrieved from the database.</summary>
        public abstract object Decrypt(object ciphertext);

        /// <summary>Returns the encryption method type (e.g. AllDataEncrypted or PartialEncryption).</summary>
        public abstract EncryptionMethodType GetEncryptionMethodType();

        /// <summary>
        /// Creates an instance of the encryption algorithm specified by the attribute.
        /// </summary>
        public static EncryptionAlgorithm CreateAlgorithm(
            Attributes.EncryptedAttribute encAttr, string sourceName, string targetName)
        {
            if (encAttr?.AlgorithmType == null)
                throw new PersistenceException(
                    $"EncryptedAttribute on {sourceName}.{targetName} has no AlgorithmType");
            return (EncryptionAlgorithm)System.Activator.CreateInstance(encAttr.AlgorithmType);
        }
    }
}
