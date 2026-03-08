using System;
using System.Collections.Generic;
using System.Reflection;

namespace Turquoise.ORM
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
                "Turquoise.ORM.TBool"         => new TBool(),
                "Turquoise.ORM.TByte"         => new TByte(),
                "Turquoise.ORM.TByteArray"    => new TByteArray(),
                "Turquoise.ORM.TChar"         => new TChar(),
                "Turquoise.ORM.TDate"         => new TDate(),
                "Turquoise.ORM.TDateTime"     => new TDateTime(),
                "Turquoise.ORM.TDecimal"      => new TDecimal(),
                "Turquoise.ORM.TDouble"       => new TDouble(),
                "Turquoise.ORM.TFloat"        => new TFloat(),
                "Turquoise.ORM.TForeignKey"   => new TForeignKey(),
                "Turquoise.ORM.TGuid"         => new TGuid(),
                "Turquoise.ORM.THtmlString"   => new THtmlString(),
                "Turquoise.ORM.TInt"          => new TInt(),
                "Turquoise.ORM.TInt16"        => new TInt16(),
                "Turquoise.ORM.TInt64"        => new TInt64(),
                "Turquoise.ORM.TIpAddress"    => new TIpAddress(),
                "Turquoise.ORM.TLocalDate"    => new TLocalDate(),
                "Turquoise.ORM.TLocalDateTime"=> new TLocalDateTime(),
                "Turquoise.ORM.TLong"         => new TLong(),
                "Turquoise.ORM.TPrimaryKey"   => new TPrimaryKey(),
                "Turquoise.ORM.TSByte"        => new TSByte(),
                "Turquoise.ORM.TString"       => new TString(),
                "Turquoise.ORM.TUInt"         => new TUInt(),
                "Turquoise.ORM.TUInt16"       => new TUInt16(),
                "Turquoise.ORM.TUInt64"       => new TUInt64(),
                "Turquoise.ORM.TUtcDate"      => new TUtcDate(),
                "Turquoise.ORM.TUtcDateTime"  => new TUtcDateTime(),
                "Turquoise.ORM.TTime"         => new TTime(),
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
