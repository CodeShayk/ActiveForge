using System;

namespace Turquoise.ORM.Attributes
{
    public enum EncryptionMethodType { AllDataEncrypted, PartialEncryption }

    /// <summary>Marks a field as encrypted at the ORM layer.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class EncryptedAttribute : Attribute
    {
        public EncryptedAttribute(Type algorithmType, EncryptionMethodType method = EncryptionMethodType.AllDataEncrypted)
        {
            AlgorithmType = algorithmType;
            Method        = method;
        }
        public Type                AlgorithmType { get; }
        public EncryptionMethodType Method        { get; }
        public EncryptionMethodType GetEncryptionMethodType() => Method;
    }
}
