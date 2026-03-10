using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>Integer foreign key field (references another table's primary key).</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TForeignKey : TKey
    {
        public TForeignKey()            : base() { }
        public TForeignKey(int value)   : base(value) { }
        public TForeignKey(long value)  : base(checked((int)value)) { }
        public TForeignKey(object v)    : base(v) { }
        public TForeignKey(TInt value)  { CopyFrom(value); }

        public static implicit operator TForeignKey(int v)          => new TForeignKey(v);
        public static implicit operator TForeignKey(long v)         => new TForeignKey(v);
        public static implicit operator TPrimaryKey(TForeignKey fk) => new TPrimaryKey((TInt)fk);

        public static bool operator ==(TForeignKey o1, TForeignKey o2) => EqualityOperatorHelper<TForeignKey>(o1, o2);
        public static bool operator !=(TForeignKey o1, TForeignKey o2) => !(o1 == o2);
        public static bool operator ==(TForeignKey o1, int o2)         => o1 == (TForeignKey)o2;
        public static bool operator !=(TForeignKey o1, int o2)         => o1 != (TForeignKey)o2;
        public static bool operator ==(int o1, TForeignKey o2)         => (TForeignKey)o1 == o2;
        public static bool operator !=(int o1, TForeignKey o2)         => (TForeignKey)o1 != o2;
        public static bool operator ==(TPrimaryKey o1, TForeignKey o2) => (TForeignKey)(TInt)o1 == o2;
        public static bool operator !=(TPrimaryKey o1, TForeignKey o2) => !((TForeignKey)(TInt)o1 == o2);
        public static bool operator ==(TForeignKey o1, TPrimaryKey o2) => o1 == (TForeignKey)(TInt)o2;
        public static bool operator !=(TForeignKey o1, TPrimaryKey o2) => !(o1 == (TForeignKey)(TInt)o2);
        public static bool operator >(TForeignKey  o1, TForeignKey o2) => GTHelper<TForeignKey>(o1, o2);
        public static bool operator <(TForeignKey  o1, TForeignKey o2) => LTHelper<TForeignKey>(o1, o2);
        public static bool operator >=(TForeignKey o1, TForeignKey o2) => o1 > o2 || o1 == o2;
        public static bool operator <=(TForeignKey o1, TForeignKey o2) => o1 < o2 || o1 == o2;

        public override string GetTypeDescription() => "foreignkey";
        public override bool   Equals(object obj)   => base.Equals(obj);
        public override int    GetHashCode()         => base.GetHashCode();
    }
}
