using System;
using System.Collections.Generic;
using System.Reflection;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base class for all ActiveForge typed database fields.
    /// <para>
    /// Every concrete subclass (e.g. <see cref="TInt"/>, <see cref="TString"/>, <see cref="TBool"/>)
    /// wraps a single CLR value together with null-tracking, load-state, conversion-error state, and
    /// database metadata.  The value is never accessed directly from outside the class hierarchy;
    /// callers use <see cref="SetValue(object)"/>, <see cref="GetValue()"/>, or the implicit
    /// cast operators defined on each concrete type.
    /// </para>
    /// <para>
    /// A field starts in the null state (<c>IsNull() == true</c>).  Assigning any non-null,
    /// non-empty value transitions it to the non-null state.  Passing <c>null</c>,
    /// <see cref="DBNull.Value"/>, or an empty string (when
    /// <c>ConvertEmptyStringsToNull</c> is <c>true</c>) transitions it back to null.
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class TField
    {
        #region State

        /// <summary>
        /// <c>true</c> when the field holds no value (SQL NULL semantics).
        /// Initialised to <c>true</c> so every new field starts null.
        /// </summary>
        protected bool Null = true;

        /// <summary>
        /// <c>true</c> after the field value has been populated from the database.
        /// Set explicitly via <see cref="SetLoaded"/>.
        /// </summary>
        protected bool Loaded;

        /// <summary>
        /// Backing store for the <see cref="ConversionError"/> property.
        /// Set to <c>true</c> by the framework when a value could not be converted
        /// to the field's underlying type.
        /// </summary>
        protected bool _ConversionError;

        /// <summary>
        /// When <c>true</c> (the default), an empty string passed to
        /// <see cref="SetValue(object)"/> is treated as SQL NULL rather than
        /// stored as an empty string.
        /// </summary>
        protected bool ConvertEmptyStringsToNull = true;

        /// <summary>
        /// The raw, unconverted value that was last passed to <see cref="SetValue(object)"/>.
        /// Preserved so the original input can be retrieved even after a conversion error.
        /// </summary>
        protected object RawValue;

        /// <summary>
        /// <c>true</c> when a validation rule has been marked as failed via
        /// <see cref="SetValidationFailure"/>.
        /// </summary>
        protected bool ValidationFailed;

        /// <summary>Cached <see cref="Type"/> reference for <see cref="TField"/>.</summary>
        protected static readonly Type TFieldType        = typeof(TField);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TPrimaryKey"/>.</summary>
        protected static readonly Type TPrimaryKeyType   = typeof(TPrimaryKey);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TForeignKey"/>.</summary>
        protected static readonly Type TForeignKeyType   = typeof(TForeignKey);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TString"/>.</summary>
        protected static readonly Type TStringType       = typeof(TString);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TDateTime"/>.</summary>
        protected static readonly Type TDateTimeType     = typeof(TDateTime);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TBool"/>.</summary>
        protected static readonly Type TBoolType         = typeof(TBool);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TGuid"/>.</summary>
        protected static readonly Type TGuidType         = typeof(TGuid);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TInt"/>.</summary>
        protected static readonly Type TIntType          = typeof(TInt);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TDecimal"/>.</summary>
        protected static readonly Type TDecimalType      = typeof(TDecimal);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TByte"/>.</summary>
        protected static readonly Type TByteType         = typeof(TByte);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TInt16"/>.</summary>
        protected static readonly Type TInt16Type        = typeof(TInt16);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="TByteArray"/>.</summary>
        protected static readonly Type TByteArrayType    = typeof(TByteArray);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="string"/>.</summary>
        protected static readonly Type StringType        = typeof(string);
        /// <summary>Cached <see cref="Type"/> reference for <see cref="DBNull"/>.</summary>
        protected static readonly Type DBNullType        = typeof(DBNull);

        #endregion

        #region Factory

        /// <summary>
        /// Creates a new, empty <see cref="TField"/> instance whose runtime type matches
        /// <paramref name="type"/>.  Common field types are resolved via a fast switch to avoid
        /// reflection overhead; unknown subclasses fall back to their public parameterless
        /// constructor via reflection.
        /// </summary>
        /// <param name="type">
        /// The concrete <see cref="TField"/> subclass to instantiate
        /// (e.g. <c>typeof(TInt)</c>).
        /// </param>
        /// <param name="container">
        /// The <see cref="Record"/> that will own the new field.
        /// Currently unused by the base implementation but available to overrides.
        /// </param>
        /// <returns>
        /// A new, null-state instance of the requested type, or <c>null</c> if the type
        /// has no public parameterless constructor.
        /// </returns>
        public static TField Create(Type type, Record container)
        {
            if (type == TForeignKeyType)  return new TForeignKey();
            if (type == TPrimaryKeyType)  return new TPrimaryKey();
            if (type == TStringType)      return new TString();
            if (type == TIntType)         return new TInt();
            if (type == TDateTimeType)    return new TDateTime();
            if (type == TBoolType)        return new TBool();
            if (type == TByteType)        return new TByte();
            if (type == TDecimalType)     return new TDecimal();
            if (type == TInt16Type)       return new TInt16();
            if (type == TByteArrayType)   return new TByteArray();
            if (type == TGuidType)        return new TGuid();

            // Fall back to reflection for subclasses
            ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor != null) return (TField)ctor.Invoke(null);
            return null;
        }

        /// <summary>
        /// Creates a new, empty <see cref="TField"/> instance identified by its
        /// fully-qualified type name (e.g. <c>"ActiveForge.TInt"</c>).
        /// </summary>
        /// <param name="typeName">
        /// The assembly-qualified or fully-qualified name of the concrete
        /// <see cref="TField"/> subclass.
        /// </param>
        /// <returns>A new, null-state instance of the identified type.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <paramref name="typeName"/> does not correspond to any
        /// known <see cref="TField"/> subclass.
        /// </exception>
        public static TField Create(string typeName)
        {
            return typeName switch
            {
                "ActiveForge.TBool"         => new TBool(),
                "ActiveForge.TByte"         => new TByte(),
                "ActiveForge.TByteArray"    => new TByteArray(),
                "ActiveForge.TChar"         => new TChar(),
                "ActiveForge.TDate"         => new TDate(),
                "ActiveForge.TDateTime"     => new TDateTime(),
                "ActiveForge.TDecimal"      => new TDecimal(),
                "ActiveForge.TDouble"       => new TDouble(),
                "ActiveForge.TFloat"        => new TFloat(),
                "ActiveForge.TForeignKey"   => new TForeignKey(),
                "ActiveForge.TGuid"         => new TGuid(),
                "ActiveForge.THtmlString"   => new THtmlString(),
                "ActiveForge.TInt"          => new TInt(),
                "ActiveForge.TInt16"        => new TInt16(),
                "ActiveForge.TInt64"        => new TInt64(),
                "ActiveForge.TIpAddress"    => new TIpAddress(),
                "ActiveForge.TLocalDate"    => new TLocalDate(),
                "ActiveForge.TLocalDateTime"=> new TLocalDateTime(),
                "ActiveForge.TLong"         => new TLong(),
                "ActiveForge.TPrimaryKey"   => new TPrimaryKey(),
                "ActiveForge.TSByte"        => new TSByte(),
                "ActiveForge.TString"       => new TString(),
                "ActiveForge.TUInt"         => new TUInt(),
                "ActiveForge.TUInt16"       => new TUInt16(),
                "ActiveForge.TUInt64"       => new TUInt64(),
                "ActiveForge.TUtcDate"      => new TUtcDate(),
                "ActiveForge.TUtcDateTime"  => new TUtcDateTime(),
                "ActiveForge.TTime"         => new TTime(),
                _ => throw new PersistenceException("Unknown TField type: " + typeName)
            };
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initialises a new field in the null state.
        /// Subclasses must call this base constructor (implicitly or explicitly) to ensure
        /// <see cref="Null"/> starts as <c>true</c>.
        /// </summary>
        protected TField()
        {
            Null = true;
        }

        #endregion

        #region Abstract interface

        /// <summary>
        /// Returns the CLR type that this field wraps (e.g. <c>typeof(int)</c> for
        /// <see cref="TInt"/>).  Used by the framework for schema discovery and
        /// parameter binding.
        /// </summary>
        /// <returns>The underlying CLR <see cref="Type"/>.</returns>
        public abstract Type GetUnderlyingType();

        /// <summary>
        /// Returns the current value of the field as a plain <see cref="object"/>.
        /// The caller is responsible for null-checking (<see cref="IsNull()"/>) before
        /// consuming the result, because a null field still returns the type's default
        /// (e.g. <c>0</c>, <c>false</c>, <c>null</c>) rather than throwing.
        /// </summary>
        /// <returns>The wrapped CLR value.</returns>
        public abstract object GetValue();

        /// <summary>
        /// Sets the inner value from an already-unboxed or already-typed object.
        /// Called by <see cref="SetValue(object)"/> after null/empty-string handling;
        /// implementations should cast or convert <paramref name="value"/> to the
        /// field's specific CLR type and store it in <c>InnerValue</c>.
        /// </summary>
        /// <param name="value">
        /// The non-null value to store.  May be another <see cref="TField"/> of the
        /// same subtype, the raw CLR type, or any type that <see cref="Convert"/> can
        /// coerce to the target type.
        /// </param>
        public abstract void SetDerivedValue(object value);

        /// <summary>
        /// Returns a short, lowercase string that identifies the field's database type
        /// (e.g. <c>"int"</c>, <c>"string"</c>, <c>"bool"</c>).  Used when generating
        /// schema descriptions and diagnostic messages.
        /// </summary>
        /// <returns>A non-null, lowercase type identifier string.</returns>
        public abstract string GetTypeDescription();

        #endregion

        #region Null / Loaded state

        /// <summary>
        /// Returns <c>true</c> when the field currently holds SQL NULL (no value has
        /// been assigned, or the last assignment was <c>null</c> / <see cref="DBNull.Value"/>
        /// / an empty string with <c>ConvertEmptyStringsToNull</c> enabled).
        /// </summary>
        /// <returns><c>true</c> if the field is null; otherwise <c>false</c>.</returns>
        public bool IsNull()    => Null;

        /// <summary>
        /// Returns <c>true</c> when the field's value has been populated from the
        /// database (i.e. <see cref="SetLoaded"/> was called with <c>true</c>).
        /// </summary>
        /// <returns><c>true</c> if the field has been loaded; otherwise <c>false</c>.</returns>
        public bool IsLoaded()  => Loaded;

        /// <summary>
        /// Explicitly sets the null state of the field.
        /// When setting to <c>true</c>, also clears <see cref="RawValue"/>.
        /// Subclasses override this to additionally reset their <c>InnerValue</c>
        /// to the type's default (e.g. <c>0</c>, <c>false</c>, <c>'\0'</c>).
        /// </summary>
        /// <param name="isNull">
        /// <c>true</c> to mark the field as null; <c>false</c> to mark it as
        /// containing a value.
        /// </param>
        public virtual void SetNull(bool isNull)
        {
            Null = isNull;
            if (isNull) RawValue = null;
        }

        /// <summary>
        /// Sets the load state of the field, indicating whether its value has been
        /// populated from a database row.
        /// </summary>
        /// <param name="loaded">
        /// <c>true</c> to mark the field as loaded from the database;
        /// <c>false</c> to mark it as not yet loaded.
        /// </param>
        public void SetLoaded(bool loaded) => Loaded = loaded;

        #endregion

        #region Value setting

        /// <summary>
        /// Sets the field's value from an arbitrary <see cref="object"/>.
        /// <para>
        /// Handles the following special inputs before delegating to
        /// <see cref="SetDerivedValue"/>:
        /// <list type="bullet">
        ///   <item><c>null</c> or <see cref="DBNull.Value"/> → field becomes null.</item>
        ///   <item>
        ///     An empty <see cref="string"/> when <c>ConvertEmptyStringsToNull</c>
        ///     is <c>true</c> → field becomes null.
        ///   </item>
        /// </list>
        /// </para>
        /// The raw, unconverted value is always stored in <see cref="RawValue"/> before
        /// conversion so it can be retrieved via <see cref="GetRawValue()"/> even if
        /// a conversion error occurs.
        /// </summary>
        /// <param name="value">
        /// The value to store.  May be the field's native CLR type, another
        /// <see cref="TField"/> of the same subtype, a string representation, or
        /// <c>null</c> / <see cref="DBNull.Value"/>.
        /// </param>
        public virtual void SetValue(object value)
        {
            if (value == null || value is DBNull)
            {
                SetNull(true);
                return;
            }
            RawValue = value;
            if ((value is string s) && ConvertEmptyStringsToNull && s.Length == 0)
            {
                SetNull(true);
                return;
            }
            SetNull(false);
            SetDerivedValue(value);
        }

        /// <summary>
        /// Returns the raw, unconverted value that was last passed to
        /// <see cref="SetValue(object)"/>, or <c>null</c> if the field is in the null
        /// state.  Useful for surfacing the original input when
        /// <see cref="ConversionError"/> is <c>true</c>.
        /// </summary>
        /// <returns>The raw input value, or <c>null</c>.</returns>
        public object GetRawValue() => RawValue;

        #endregion

        #region Validation

        /// <summary>
        /// Gets or sets a value indicating whether the last value assignment caused a
        /// type-conversion error.  Set to <c>true</c> by the framework when a value
        /// could not be converted to the field's underlying type; set to <c>false</c>
        /// by typed <c>SetValue</c> overloads in concrete subclasses after a successful
        /// assignment.
        /// </summary>
        public bool ConversionError { get => _ConversionError; set => _ConversionError = value; }

        /// <summary>
        /// Returns <c>true</c> when the last value assignment caused a type-conversion
        /// error.  Equivalent to reading <see cref="ConversionError"/>.
        /// </summary>
        /// <returns><c>true</c> if a conversion error has been recorded.</returns>
        public bool ConversionErrorOccurred() => _ConversionError;

        /// <summary>
        /// Records the result of an external validation check.
        /// Pass <c>true</c> to mark the field as invalid; <c>false</c> to clear a
        /// previous failure.  The result is retrievable via <see cref="IsValid()"/>.
        /// </summary>
        /// <param name="failure">
        /// <c>true</c> to indicate validation failure; <c>false</c> to indicate success.
        /// </param>
        public void SetValidationFailure(bool failure)
        {
            ValidationFailed = failure;
        }

        /// <summary>
        /// Returns <c>true</c> when no validation failure has been recorded for this
        /// field (i.e. <see cref="SetValidationFailure"/> was never called with
        /// <c>true</c>, or was last called with <c>false</c>).
        /// </summary>
        /// <returns><c>true</c> if the field is valid; otherwise <c>false</c>.</returns>
        public bool IsValid() => !ValidationFailed;

        #endregion

        #region Copy

        /// <summary>
        /// Copies the value and load state from <paramref name="source"/> into this
        /// field.  If <paramref name="source"/> is null or null-state, this field is
        /// set to null.  Otherwise the value is transferred via
        /// <see cref="SetValue(object)"/> and <see cref="Loaded"/> is mirrored.
        /// </summary>
        /// <param name="source">
        /// The <see cref="TField"/> to copy from, or <c>null</c> (treated as null-state).
        /// </param>
        public virtual void CopyFrom(TField source)
        {
            if (source == null) return;
            if (source.IsNull()) { SetNull(true); return; }
            SetValue(source.GetValue());
            Loaded = source.Loaded;
        }

        #endregion

        #region Validity check helper

        /// <summary>
        /// Throws a <see cref="PersistenceException"/> when the field is in the null
        /// state.  Called by the protected <c>Value</c> property getter in each
        /// concrete subclass to guard against reading an unset field.
        /// </summary>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="IsNull()"/> returns <c>true</c>.
        /// </exception>
        protected void CheckValidity()
        {
            if (Null)
                throw new PersistenceException($"Attempted to read a null {GetType().Name} value.");
        }

        #endregion

        #region Comparison helpers for subclasses

        /// <summary>
        /// Returns <c>true</c> when both <paramref name="o1"/> and <paramref name="o2"/>
        /// are <c>null</c> references (not merely null-valued fields).
        /// Used by equality operator implementations to replicate reference-null semantics.
        /// </summary>
        protected static bool BothObjectsNull<T>(T o1, T o2) where T : TField
            => (object)o1 == null && (object)o2 == null;

        /// <summary>
        /// Returns <c>true</c> when at least one of <paramref name="o1"/> or
        /// <paramref name="o2"/> is a <c>null</c> reference.
        /// Used by equality operator implementations to short-circuit comparison.
        /// </summary>
        protected static bool EitherObjectNull<T>(T o1, T o2) where T : TField
            => (object)o1 == null || (object)o2 == null;

        /// <summary>
        /// Shared implementation for the <c>==</c> operator on concrete subclasses.
        /// Two null references are equal; a null reference and a non-null reference are not;
        /// two non-null references are compared via <see cref="Equals(object)"/>.
        /// </summary>
        protected static bool EqualityOperatorHelper<T>(T o1, T o2) where T : TField
        {
            if (BothObjectsNull(o1, o2)) return true;
            if (EitherObjectNull(o1, o2)) return false;
            return o1.Equals(o2);
        }

        /// <summary>
        /// Shared implementation for the <c>&gt;</c> operator on concrete subclasses.
        /// Null references and null-state fields sort below all non-null values;
        /// two non-null values are compared via <see cref="IComparable.CompareTo"/>.
        /// </summary>
        protected static bool GTHelper<T>(T o1, T o2) where T : TField, IComparable
        {
            if (BothObjectsNull(o1, o2)) return false;
            if ((object)o1 == null) return false;
            if ((object)o2 == null) return true;
            if (o1.IsNull() && o2.IsNull()) return false;
            if (o1.IsNull()) return false;
            if (o2.IsNull()) return true;
            return o1.CompareTo(o2) > 0;
        }

        /// <summary>
        /// Shared implementation for the <c>&lt;</c> operator on concrete subclasses.
        /// Null references and null-state fields sort below all non-null values;
        /// two non-null values are compared via <see cref="IComparable.CompareTo"/>.
        /// </summary>
        protected static bool LTHelper<T>(T o1, T o2) where T : TField, IComparable
        {
            if (BothObjectsNull(o1, o2)) return false;
            if ((object)o1 == null) return true;
            if ((object)o2 == null) return false;
            if (o1.IsNull() && o2.IsNull()) return false;
            if (o1.IsNull()) return true;
            if (o2.IsNull()) return false;
            return o1.CompareTo(o2) < 0;
        }

        /// <summary>
        /// Strongly-typed equality helper used by concrete subclass
        /// <see cref="object.Equals(object)"/> overrides.  Two null-state fields of
        /// the same type are considered equal; a null-state and a non-null-state field
        /// are not; two non-null fields are compared by their underlying values via
        /// <see cref="GetValue()"/>.
        /// </summary>
        /// <typeparam name="T">The concrete <see cref="TField"/> subtype.</typeparam>
        /// <typeparam name="TBase">The underlying CLR value type (unused at runtime but
        /// required to constrain the generic signature).</typeparam>
        /// <param name="other">The object to compare against.</param>
        /// <returns>
        /// <c>true</c> when <paramref name="other"/> is the same subtype and its value
        /// equals this field's value (with consistent null semantics).
        /// </returns>
        protected bool EqualsHelper<T, TBase>(object other) where T : TField
        {
            if (other == null) return IsNull();
            if (other is T t)
            {
                if (IsNull() && t.IsNull()) return true;
                if (IsNull() || t.IsNull()) return false;
                return GetValue().Equals(t.GetValue());
            }
            return false;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="other"/> can be safely cast to the
        /// concrete field subtype <typeparamref name="T"/> or to the underlying CLR base
        /// type <typeparamref name="TBase"/>.
        /// </summary>
        protected bool IsCastable<T, TBase>(object other) where T : TField
            => other is T || other is TBase;

        #endregion

        #region Operator overloads

        /// <summary>
        /// Determines whether two <see cref="TField"/> instances are equal.
        /// Two <c>null</c> references are considered equal; a <c>null</c> reference and
        /// a non-<c>null</c> reference are not; two non-<c>null</c> instances are
        /// compared via <see cref="Equals(object)"/>.
        /// </summary>
        /// <param name="f1">The left-hand field.</param>
        /// <param name="f2">The right-hand field.</param>
        /// <returns><c>true</c> if <paramref name="f1"/> equals <paramref name="f2"/>.</returns>
        public static bool operator ==(TField f1, TField f2)
        {
            if ((object)f1 == null && (object)f2 == null) return true;
            if ((object)f1 == null || (object)f2 == null) return false;
            return f1.Equals(f2);
        }

        /// <summary>
        /// Determines whether two <see cref="TField"/> instances are not equal.
        /// </summary>
        /// <param name="f1">The left-hand field.</param>
        /// <param name="f2">The right-hand field.</param>
        /// <returns><c>true</c> if <paramref name="f1"/> does not equal <paramref name="f2"/>.</returns>
        public static bool operator !=(TField f1, TField f2) => !(f1 == f2);

        /// <inheritdoc/>
        public override bool Equals(object obj) => base.Equals(obj);

        /// <summary>
        /// Returns a hash code for this field.  Null-state fields always return the hash
        /// code of <see cref="int.MinValue"/>; non-null fields return the hash code of
        /// their underlying value via <see cref="GetValue()"/>.
        /// </summary>
        /// <returns>A hash code suitable for use in hash tables.</returns>
        public override int GetHashCode()
        {
            if (IsNull()) return int.MinValue.GetHashCode();
            return GetValue().GetHashCode();
        }

        #endregion

        #region FieldInfo lookup helper

        /// <summary>
        /// Returns the <see cref="FieldInfo"/> that declares this <see cref="TField"/>
        /// instance on the given <paramref name="container"/> object.  Delegates to
        /// <see cref="Record.GetFieldInfo(TField)"/> on the container.
        /// </summary>
        /// <param name="container">
        /// The <see cref="Record"/> that owns this field.
        /// </param>
        /// <returns>
        /// The <see cref="FieldInfo"/> for the field declaration, or <c>null</c> if not
        /// found.
        /// </returns>
        public FieldInfo GetFieldInfo(Record container)
            => container.GetFieldInfo(this);

        #endregion
    }
}
