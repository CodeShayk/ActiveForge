using FluentAssertions;
using System;
using Turquoise.ORM;
using Xunit;

namespace Turquoise.ORM.Tests.Fields
{
    /// <summary>
    /// Tests for TField.Create() factory methods (both Type-based and string-based).
    /// </summary>
    public class TFieldFactoryTests
    {
        // ── Type-based factory ────────────────────────────────────────────────────────

        [Theory]
        [InlineData(typeof(TString))]
        [InlineData(typeof(TInt))]
        [InlineData(typeof(TDecimal))]
        [InlineData(typeof(TPrimaryKey))]
        [InlineData(typeof(TForeignKey))]
        [InlineData(typeof(TDateTime))]
        [InlineData(typeof(TBool))]
        [InlineData(typeof(TByteArray))]
        [InlineData(typeof(TGuid))]
        [InlineData(typeof(TInt16))]
        [InlineData(typeof(TByte))]
        public void Create_ByType_ReturnsCorrectType(Type type)
        {
            var field = TField.Create(type, null);
            field.Should().NotBeNull();
            field.Should().BeOfType(type);
        }

        [Theory]
        [InlineData(typeof(TString))]
        [InlineData(typeof(TInt))]
        [InlineData(typeof(TDecimal))]
        [InlineData(typeof(TPrimaryKey))]
        [InlineData(typeof(TForeignKey))]
        [InlineData(typeof(TDateTime))]
        [InlineData(typeof(TBool))]
        [InlineData(typeof(TGuid))]
        public void Create_ByType_StartsAsNull(Type type)
        {
            var field = TField.Create(type, null);
            field.IsNull().Should().BeTrue($"{type.Name} should start null");
        }

        [Theory]
        [InlineData(typeof(TString))]
        [InlineData(typeof(TInt))]
        [InlineData(typeof(TDecimal))]
        public void Create_ByType_NotLoaded(Type type)
        {
            var field = TField.Create(type, null);
            field.IsLoaded().Should().BeFalse();
        }

        // ── String-name factory ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("Turquoise.ORM.TString",       typeof(TString))]
        [InlineData("Turquoise.ORM.TInt",           typeof(TInt))]
        [InlineData("Turquoise.ORM.TDecimal",       typeof(TDecimal))]
        [InlineData("Turquoise.ORM.TPrimaryKey",    typeof(TPrimaryKey))]
        [InlineData("Turquoise.ORM.TForeignKey",    typeof(TForeignKey))]
        [InlineData("Turquoise.ORM.TDateTime",      typeof(TDateTime))]
        [InlineData("Turquoise.ORM.TBool",          typeof(TBool))]
        [InlineData("Turquoise.ORM.TByte",          typeof(TByte))]
        [InlineData("Turquoise.ORM.TByteArray",     typeof(TByteArray))]
        [InlineData("Turquoise.ORM.TChar",          typeof(TChar))]
        [InlineData("Turquoise.ORM.TDate",          typeof(TDate))]
        [InlineData("Turquoise.ORM.TDouble",        typeof(TDouble))]
        [InlineData("Turquoise.ORM.TFloat",         typeof(TFloat))]
        [InlineData("Turquoise.ORM.TGuid",          typeof(TGuid))]
        [InlineData("Turquoise.ORM.THtmlString",    typeof(THtmlString))]
        [InlineData("Turquoise.ORM.TInt16",         typeof(TInt16))]
        [InlineData("Turquoise.ORM.TInt64",         typeof(TInt64))]
        [InlineData("Turquoise.ORM.TIpAddress",     typeof(TIpAddress))]
        [InlineData("Turquoise.ORM.TLocalDate",     typeof(TLocalDate))]
        [InlineData("Turquoise.ORM.TLocalDateTime", typeof(TLocalDateTime))]
        [InlineData("Turquoise.ORM.TLong",          typeof(TLong))]
        [InlineData("Turquoise.ORM.TSByte",         typeof(TSByte))]
        [InlineData("Turquoise.ORM.TUInt",          typeof(TUInt))]
        [InlineData("Turquoise.ORM.TUInt16",        typeof(TUInt16))]
        [InlineData("Turquoise.ORM.TUInt64",        typeof(TUInt64))]
        [InlineData("Turquoise.ORM.TUtcDate",       typeof(TUtcDate))]
        [InlineData("Turquoise.ORM.TUtcDateTime",   typeof(TUtcDateTime))]
        [InlineData("Turquoise.ORM.TTime",          typeof(TTime))]
        public void Create_ByName_ReturnsCorrectType(string typeName, Type expected)
        {
            var field = TField.Create(typeName);
            field.Should().NotBeNull();
            field.Should().BeOfType(expected);
        }

        [Fact]
        public void Create_ByName_Unknown_ThrowsPersistenceException()
        {
            Action act = () => TField.Create("Turquoise.ORM.TUnknownType");
            act.Should().Throw<PersistenceException>()
               .WithMessage("*Unknown TField type*");
        }

        [Fact]
        public void Create_ByName_AllStartAsNull()
        {
            TField.Create("Turquoise.ORM.TString").IsNull().Should().BeTrue();
            TField.Create("Turquoise.ORM.TInt").IsNull().Should().BeTrue();
            TField.Create("Turquoise.ORM.TDecimal").IsNull().Should().BeTrue();
            TField.Create("Turquoise.ORM.TPrimaryKey").IsNull().Should().BeTrue();
        }
    }
}
