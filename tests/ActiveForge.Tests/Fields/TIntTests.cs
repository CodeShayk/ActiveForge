using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.Tests.Fields
{
    public class TIntTests
    {
        // ── Default state ─────────────────────────────────────────────────────────────

        [Fact]
        public void Default_IsNull_True()
        {
            var i = new TInt();
            i.IsNull().Should().BeTrue();
        }

        [Fact]
        public void Default_IsLoaded_False()
        {
            new TInt().IsLoaded().Should().BeFalse();
        }

        [Fact]
        public void Default_GetValue_Zero()
        {
            // InnerValue is 0 even when null; GetValue() returns the boxed int
            ((int)new TInt().GetValue()).Should().Be(0);
        }

        // ── SetValue ──────────────────────────────────────────────────────────────────

        [Fact]
        public void SetValue_Int_ClearsNull()
        {
            var i = new TInt();
            i.SetValue(42);
            i.IsNull().Should().BeFalse();
            ((int)i.GetValue()).Should().Be(42);
        }

        [Fact]
        public void SetValue_BoxedInt_Works()
        {
            var i = new TInt();
            i.SetValue((object)99);
            i.IsNull().Should().BeFalse();
            ((int)i.GetValue()).Should().Be(99);
        }

        [Fact]
        public void SetValue_Null_SetsNull()
        {
            var i = new TInt(5);
            i.SetValue((object)null);
            i.IsNull().Should().BeTrue();
        }

        [Fact]
        public void SetValue_DBNull_SetsNull()
        {
            var i = new TInt(5);
            i.SetValue(System.DBNull.Value);
            i.IsNull().Should().BeTrue();
        }

        [Fact]
        public void SetNull_ResetsToZero()
        {
            var i = new TInt(7);
            i.SetNull(true);
            i.IsNull().Should().BeTrue();
            ((int)i.GetValue()).Should().Be(0);
        }

        // ── Implicit conversions ──────────────────────────────────────────────────────

        [Fact]
        public void ImplicitFrom_Int()
        {
            TInt i = 55;
            i.IsNull().Should().BeFalse();
            ((int)i).Should().Be(55);
        }

        [Fact]
        public void ImplicitTo_Int()
        {
            var i = new TInt(33);
            int value = i;
            value.Should().Be(33);
        }

        // ── Comparison operators ──────────────────────────────────────────────────────

        [Fact]
        public void Equality_SameValue_True()
        {
            var i1 = new TInt(10);
            var i2 = new TInt(10);
            (i1 == i2).Should().BeTrue();
        }

        [Fact]
        public void Equality_DifferentValue_False()
        {
            var i1 = new TInt(10);
            var i2 = new TInt(20);
            (i1 == i2).Should().BeFalse();
        }

        [Fact]
        public void Equality_WithInt_Works()
        {
            var i = new TInt(7);
            (i == 7).Should().BeTrue();
            (7 == i).Should().BeTrue();
            (i == 8).Should().BeFalse();
        }

        [Fact]
        public void GreaterThan_Works()
        {
            var i1 = new TInt(10);
            var i2 = new TInt(5);
            (i1 > i2).Should().BeTrue();
            (i2 > i1).Should().BeFalse();
        }

        [Fact]
        public void LessThan_Works()
        {
            var i1 = new TInt(3);
            var i2 = new TInt(9);
            (i1 < i2).Should().BeTrue();
            (i2 < i1).Should().BeFalse();
        }

        [Fact]
        public void GreaterOrEqual_Works()
        {
            var i1 = new TInt(5);
            var i2 = new TInt(5);
            (i1 >= i2).Should().BeTrue();
            (i1 >= new TInt(4)).Should().BeTrue();
            (i1 >= new TInt(6)).Should().BeFalse();
        }

        [Fact]
        public void LessOrEqual_Works()
        {
            var i1 = new TInt(5);
            (i1 <= new TInt(5)).Should().BeTrue();
            (i1 <= new TInt(6)).Should().BeTrue();
            (i1 <= new TInt(4)).Should().BeFalse();
        }

        [Fact]
        public void GreaterThan_WithInt_Works()
        {
            var i = new TInt(10);
            (i > 5).Should().BeTrue();
            (i < 20).Should().BeTrue();
        }

        // ── Null comparison semantics ─────────────────────────────────────────────────

        [Fact]
        public void BothNull_Equality_IsTrue()
        {
            var i1 = new TInt();
            var i2 = new TInt();
            i1.Equals(i2).Should().BeTrue();
        }

        [Fact]
        public void NullGreaterThan_NonNull_IsFalse()
        {
            var nullInt = new TInt();
            var nonNull = new TInt(1);
            (nullInt > nonNull).Should().BeFalse();
        }

        // ── Increment operator ────────────────────────────────────────────────────────

        [Fact]
        public void Increment_Operator_Works()
        {
            var i = new TInt(5);
            var inc = ++i;
            ((int)inc).Should().Be(6);
        }

        [Fact]
        public void Decrement_Operator_Works()
        {
            var i = new TInt(5);
            var dec = --i;
            ((int)dec).Should().Be(4);
        }

        // ── Parse ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Parse_ValidString_Works()
        {
            TInt i = TInt.Parse("123");
            ((int)i).Should().Be(123);
        }

        // ── CopyFrom ──────────────────────────────────────────────────────────────────

        [Fact]
        public void CopyFrom_CopiesValue()
        {
            var src = new TInt(77);
            var dst = new TInt();
            dst.CopyFrom(src);
            dst.IsNull().Should().BeFalse();
            ((int)dst.GetValue()).Should().Be(77);
        }

        [Fact]
        public void CopyFrom_NullSource_SetsNull()
        {
            var src = new TInt();
            var dst = new TInt(99);
            dst.CopyFrom(src);
            dst.IsNull().Should().BeTrue();
        }

        // ── Type info ─────────────────────────────────────────────────────────────────

        [Fact]
        public void GetUnderlyingType_IsInt()
        {
            new TInt().GetUnderlyingType().Should().Be(typeof(int));
        }

        [Fact]
        public void GetTypeDescription_IsInt()
        {
            new TInt().GetTypeDescription().Should().Be("int");
        }

        // ── MinValue / MaxValue ───────────────────────────────────────────────────────

        [Fact]
        public void MinMaxValue_AreSet()
        {
            ((int)TInt.MaxValue).Should().Be(int.MaxValue);
            ((int)TInt.MinValue).Should().Be(int.MinValue);
        }

        // ── GetHashCode ───────────────────────────────────────────────────────────────

        [Fact]
        public void GetHashCode_Consistent()
        {
            var i1 = new TInt(42);
            var i2 = new TInt(42);
            i1.GetHashCode().Should().Be(i2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_Null_IsZero()
        {
            new TInt().GetHashCode().Should().Be(0);
        }
    }
}
