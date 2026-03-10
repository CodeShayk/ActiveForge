using FluentAssertions;
using System;
using ActiveForge;
using Xunit;

namespace ActiveForge.Tests.Fields
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
        [InlineData("ActiveForge.TString",       typeof(TString))]
        [InlineData("ActiveForge.TInt",           typeof(TInt))]
        [InlineData("ActiveForge.TDecimal",       typeof(TDecimal))]
        [InlineData("ActiveForge.TPrimaryKey",    typeof(TPrimaryKey))]
        [InlineData("ActiveForge.TForeignKey",    typeof(TForeignKey))]
        [InlineData("ActiveForge.TDateTime",      typeof(TDateTime))]
        [InlineData("ActiveForge.TBool",          typeof(TBool))]
        [InlineData("ActiveForge.TByte",          typeof(TByte))]
        [InlineData("ActiveForge.TByteArray",     typeof(TByteArray))]
        [InlineData("ActiveForge.TChar",          typeof(TChar))]
        [InlineData("ActiveForge.TDate",          typeof(TDate))]
        [InlineData("ActiveForge.TDouble",        typeof(TDouble))]
        [InlineData("ActiveForge.TFloat",         typeof(TFloat))]
        [InlineData("ActiveForge.TGuid",          typeof(TGuid))]
        [InlineData("ActiveForge.THtmlString",    typeof(THtmlString))]
        [InlineData("ActiveForge.TInt16",         typeof(TInt16))]
        [InlineData("ActiveForge.TInt64",         typeof(TInt64))]
        [InlineData("ActiveForge.TIpAddress",     typeof(TIpAddress))]
        [InlineData("ActiveForge.TLocalDate",     typeof(TLocalDate))]
        [InlineData("ActiveForge.TLocalDateTime", typeof(TLocalDateTime))]
        [InlineData("ActiveForge.TLong",          typeof(TLong))]
        [InlineData("ActiveForge.TSByte",         typeof(TSByte))]
        [InlineData("ActiveForge.TUInt",          typeof(TUInt))]
        [InlineData("ActiveForge.TUInt16",        typeof(TUInt16))]
        [InlineData("ActiveForge.TUInt64",        typeof(TUInt64))]
        [InlineData("ActiveForge.TUtcDate",       typeof(TUtcDate))]
        [InlineData("ActiveForge.TUtcDateTime",   typeof(TUtcDateTime))]
        [InlineData("ActiveForge.TTime",          typeof(TTime))]
        public void Create_ByName_ReturnsCorrectType(string typeName, Type expected)
        {
            var field = TField.Create(typeName);
            field.Should().NotBeNull();
            field.Should().BeOfType(expected);
        }

        [Fact]
        public void Create_ByName_Unknown_ThrowsPersistenceException()
        {
            Action act = () => TField.Create("ActiveForge.TUnknownType");
            act.Should().Throw<PersistenceException>()
               .WithMessage("*Unknown TField type*");
        }

        [Fact]
        public void Create_ByName_AllStartAsNull()
        {
            TField.Create("ActiveForge.TString").IsNull().Should().BeTrue();
            TField.Create("ActiveForge.TInt").IsNull().Should().BeTrue();
            TField.Create("ActiveForge.TDecimal").IsNull().Should().BeTrue();
            TField.Create("ActiveForge.TPrimaryKey").IsNull().Should().BeTrue();
        }
    }
}
