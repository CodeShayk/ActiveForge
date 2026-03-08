namespace Turquoise.ORM
{
    /// <summary>
    /// Interface for a custom field mapper that translates values between the CLR field
    /// and the database column representation. Implement to handle non-standard mappings.
    /// </summary>
    public interface IDBFieldMapper
    {
        /// <summary>Provides the DataObject that contains this field, for context-dependent mapping.</summary>
        void SetContainingDataObject(DataObject obj);

        /// <summary>Converts the in-memory CLR value to the value written to the database.</summary>
        object ConvertToDBValue(object value);

        /// <summary>Converts the raw database value back to the in-memory CLR representation.</summary>
        object ConvertFromDBValue(object value);
    }
}
