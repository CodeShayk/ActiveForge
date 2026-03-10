using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.Tests.Fields
{
    public class TStringTests
    {
        // ── Construction & default state ─────────────────────────────────────────────

        [Fact]
        public void Default_IsNull_True()
        {
            var s = new TString();
            s.IsNull().Should().BeTrue();
        }

        [Fact]
        public void Default_IsLoaded_False()
        {
            var s = new TString();
            s.IsLoaded().Should().BeFalse();
        }

        [Fact]
        public void Default_Length_IsZero()
        {
            var s = new TString();
            s.Length.Should().Be(0);
        }

        [Fact]
        public void Default_ToString_IsEmpty()
        {
            var s = new TString();
            s.ToString().Should().Be("");
        }

        [Fact]
        public void ConstructWithString_SetsValue()
        {
            var s = new TString("hello");
            s.IsNull().Should().BeFalse();
            s.ToString().Should().Be("hello");
        }

        // ── TString does NOT convert empty string to null ─────────────────────────────

        [Fact]
        public void EmptyString_DoesNotSetNull()
        {
            var s = new TString();
            s.SetValue("");
            s.IsNull().Should().BeFalse();
            s.Length.Should().Be(0);
        }

        // ── SetValue / null handling ──────────────────────────────────────────────────

        [Fact]
        public void SetValue_String_ClearsNull()
        {
            var s = new TString();
            s.SetValue("world");
            s.IsNull().Should().BeFalse();
            s.ToString().Should().Be("world");
        }

        [Fact]
        public void SetValue_Null_SetsNull()
        {
            var s = new TString("abc");
            s.SetValue((object)null);
            s.IsNull().Should().BeTrue();
        }

        [Fact]
        public void SetValue_DBNull_SetsNull()
        {
            var s = new TString("abc");
            s.SetValue(System.DBNull.Value);
            s.IsNull().Should().BeTrue();
        }

        [Fact]
        public void SetNull_True_ResetsToEmpty()
        {
            var s = new TString("hello");
            s.SetNull(true);
            s.IsNull().Should().BeTrue();
            s.Length.Should().Be(0);
        }

        [Fact]
        public void SetNull_False_DoesNotClearValue()
        {
            var s = new TString("hello");
            s.SetNull(false);
            s.IsNull().Should().BeFalse();
        }

        // ── Implicit conversions ──────────────────────────────────────────────────────

        [Fact]
        public void ImplicitFrom_String()
        {
            TString s = "ActiveForge";
            s.IsNull().Should().BeFalse();
            s.ToString().Should().Be("ActiveForge");
        }

        [Fact]
        public void ImplicitTo_String()
        {
            var s = new TString("orm");
            string result = s;
            result.Should().Be("orm");
        }

        // ── Equality operators ────────────────────────────────────────────────────────

        [Fact]
        public void Equality_SameValue_IsTrue()
        {
            var s1 = new TString("abc");
            var s2 = new TString("abc");
            (s1 == s2).Should().BeTrue();
        }

        [Fact]
        public void Equality_DifferentValue_IsFalse()
        {
            var s1 = new TString("abc");
            var s2 = new TString("xyz");
            (s1 == s2).Should().BeFalse();
        }

        [Fact]
        public void Equality_BothNull_IsTrue()
        {
            var s1 = new TString();
            var s2 = new TString();
            s1.IsNull().Should().BeTrue();
            s2.IsNull().Should().BeTrue();
            s1.Equals(s2).Should().BeTrue();
        }

        [Fact]
        public void Equality_OneNull_IsFalse()
        {
            var s1 = new TString("abc");
            var s2 = new TString();
            (s1 == s2).Should().BeFalse();
        }

        [Fact]
        public void Equality_WithRawString_Works()
        {
            var s = new TString("hello");
            (s == "hello").Should().BeTrue();
            ("hello" == s).Should().BeTrue();
            (s == "world").Should().BeFalse();
        }

        [Fact]
        public void Inequality_Operator_Works()
        {
            var s1 = new TString("a");
            var s2 = new TString("b");
            (s1 != s2).Should().BeTrue();
        }

        // ── GetValue ──────────────────────────────────────────────────────────────────

        [Fact]
        public void GetValue_ReturnsString()
        {
            var s = new TString("value");
            s.GetValue().Should().Be("value");
        }

        [Fact]
        public void GetUnderlyingType_IsString()
        {
            new TString().GetUnderlyingType().Should().Be(typeof(string));
        }

        [Fact]
        public void GetTypeDescription_IsString()
        {
            new TString().GetTypeDescription().Should().Be("string");
        }

        // ── CopyFrom ──────────────────────────────────────────────────────────────────

        [Fact]
        public void CopyFrom_CopiesValue()
        {
            var src = new TString("source");
            var dst = new TString();
            dst.CopyFrom(src);
            dst.IsNull().Should().BeFalse();
            dst.ToString().Should().Be("source");
        }

        [Fact]
        public void CopyFrom_NullSource_SetsNull()
        {
            var src = new TString();
            var dst = new TString("existing");
            dst.CopyFrom(src);
            dst.IsNull().Should().BeTrue();
        }

        [Fact]
        public void CopyFrom_Null_IsNoOp()
        {
            var dst = new TString("safe");
            dst.CopyFrom(null);
            dst.ToString().Should().Be("safe");
        }

        // ── String helpers ────────────────────────────────────────────────────────────

        [Fact]
        public void Contains_ReturnsTrue_WhenSubstringPresent()
        {
            var s = new TString("hello world");
            s.Contains("world").Should().BeTrue();
            s.Contains("xyz").Should().BeFalse();
        }

        [Fact]
        public void ToUpper_UpperCases()
        {
            var s = new TString("hello");
            s.ToUpper().Should().Be("HELLO");
        }

        [Fact]
        public void ToLower_LowerCases()
        {
            var s = new TString("HELLO");
            s.ToLower().Should().Be("hello");
        }

        [Fact]
        public void Trim_RemovesWhitespace()
        {
            var s = new TString("  hello  ");
            s.Trim().Should().Be("hello");
        }

        // ── Clone ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Clone_ProducesIndependentCopy()
        {
            var s = new TString("original");
            var c = (TString)s.Clone();
            c.ToString().Should().Be("original");
            s.SetValue("changed");
            c.ToString().Should().Be("original");
        }

        // ── GetHashCode ───────────────────────────────────────────────────────────────

        [Fact]
        public void GetHashCode_SameValue_SameHash()
        {
            var s1 = new TString("x");
            var s2 = new TString("x");
            s1.GetHashCode().Should().Be(s2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_Null_IsZero()
        {
            new TString().GetHashCode().Should().Be(0);
        }

        // ── CompareTo ─────────────────────────────────────────────────────────────────

        [Fact]
        public void CompareTo_Equal_IsZero()
        {
            var s1 = new TString("abc");
            var s2 = new TString("abc");
            s1.CompareTo(s2).Should().Be(0);
        }

        [Fact]
        public void CompareTo_LessThan_IsNegative()
        {
            var s1 = new TString("abc");
            var s2 = new TString("xyz");
            s1.CompareTo(s2).Should().BeLessThan(0);
        }

        // ── SetLoaded ─────────────────────────────────────────────────────────────────

        [Fact]
        public void SetLoaded_CanBeSetAndRead()
        {
            var s = new TString("x");
            s.SetLoaded(true);
            s.IsLoaded().Should().BeTrue();
            s.SetLoaded(false);
            s.IsLoaded().Should().BeFalse();
        }
    }
}
