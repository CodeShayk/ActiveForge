using System;
using System.Collections.Generic;
using System.Reflection;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base class for all Turquoise typed database fields.
    /// Wraps a CLR value with null-tracking, validation state, and DB metadata.
    /// </summary>
    [Serializable]
    public abstract class TField
    {
        #region State
        protected bool Null = true;
        protected bool Loaded;
        protected bool _ConversionError;
        protected bool ConvertEmptyStringsToNull = true;
        protected object RawValue;
        protected bool ValidationFailed;

        protected static readonly Type TFieldType        = typeof(TField);
        protected static readonly Type TPrimaryKeyType   = typeof(TPrimaryKey);
        protected static readonly Type TForeignKeyType   = typeof(TForeignKey);
        protected static readonly Type TStringType       = typeof(TString);
        protected static readonly Type TDateTimeType     = typeof(TDateTime);
        protected static readonly Type TBoolType         = typeof(TBool);
        protected static readonly Type TGuidType         = typeof(TGuid);
        protected static readonly Type TIntType          = typeof(TInt);
        protected static readonly Type TDecimalType      = typeof(TDecimal);
        protected static readonly Type TByteType         = typeof(TByte);
        protected static readonly Type TInt16Type        = typeof(TInt16);
        protected static readonly Type TByteArrayType    = typeof(TByteArray);
        protected static readonly Type StringType        = typeof(string);
        protected static readonly Type DBNullType        = typeof(DBNull);
        #endregion

        #region Factory
        /// <summary>Optimised factory that avoids reflection for common types.</summary>
        public static TField Create(Type type, DataObject container)
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

        /// <summary>Create a TField instance from its fully-qualified type name.</summary>
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
        protected TField()
        {
            Null = true;
        }
        #endregion

        #region Abstract interface
        public abstract Type   GetUnderlyingType();
        public abstract object GetValue();
        public abstract void   SetDerivedValue(object value);
        public abstract string GetTypeDescription();
        #endregion

        #region Null / Loaded state
        public bool IsNull()    => Null;
        public bool IsLoaded()  => Loaded;

        public virtual void SetNull(bool isNull)
        {
            Null = isNull;
            if (isNull) RawValue = null;
        }

        public void SetLoaded(bool loaded) => Loaded = loaded;
        #endregion

        #region Value setting
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

        public object GetRawValue() => RawValue;
        #endregion

        #region Validation
        public bool ConversionError { get => _ConversionError; set => _ConversionError = value; }
        public bool ConversionErrorOccurred() => _ConversionError;

        public void SetValidationFailure(bool failure)
        {
            ValidationFailed = failure;
        }

        public bool IsValid() => !ValidationFailed;
        #endregion

        #region Copy
        public virtual void CopyFrom(TField source)
        {
            if (source == null) return;
            if (source.IsNull()) { SetNull(true); return; }
            SetValue(source.GetValue());
            Loaded = source.Loaded;
        }
        #endregion

        #region Validity check helper
        protected void CheckValidity()
        {
            if (Null)
                throw new PersistenceException($"Attempted to read a null {GetType().Name} value.");
        }
        #endregion

        #region Comparison helpers for subclasses
        protected static bool BothObjectsNull<T>(T o1, T o2) where T : TField
            => (object)o1 == null && (object)o2 == null;

        protected static bool EitherObjectNull<T>(T o1, T o2) where T : TField
            => (object)o1 == null || (object)o2 == null;

        protected static bool EqualityOperatorHelper<T>(T o1, T o2) where T : TField
        {
            if (BothObjectsNull(o1, o2)) return true;
            if (EitherObjectNull(o1, o2)) return false;
            return o1.Equals(o2);
        }

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

        protected bool IsCastable<T, TBase>(object other) where T : TField
            => other is T || other is TBase;
        #endregion

        #region Operator overloads
        public static bool operator ==(TField f1, TField f2)
        {
            if ((object)f1 == null && (object)f2 == null) return true;
            if ((object)f1 == null || (object)f2 == null) return false;
            return f1.Equals(f2);
        }

        public static bool operator !=(TField f1, TField f2) => !(f1 == f2);

        public override bool Equals(object obj) => base.Equals(obj);

        public override int GetHashCode()
        {
            if (IsNull()) return int.MinValue.GetHashCode();
            return GetValue().GetHashCode();
        }
        #endregion

        #region FieldInfo lookup helper
        public FieldInfo GetFieldInfo(DataObject container)
            => container.GetFieldInfo(this);
        #endregion
    }
}
