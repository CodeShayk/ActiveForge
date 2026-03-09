using System;
using FluentAssertions;
using Turquoise.ORM;
using Xunit;

namespace Turquoise.ORM.Tests
{
    /// <summary>
    /// Unit tests for the <see cref="JoinOverride"/> struct.
    /// </summary>
    public class JoinOverrideTests
    {
        [Fact]
        public void Constructor_SetsTargetType()
        {
            var ov = new JoinOverride(typeof(object), JoinSpecification.JoinTypeEnum.InnerJoin);
            ov.TargetType.Should().Be(typeof(object));
        }

        [Fact]
        public void Constructor_SetsJoinType_InnerJoin()
        {
            var ov = new JoinOverride(typeof(object), JoinSpecification.JoinTypeEnum.InnerJoin);
            ov.JoinType.Should().Be(JoinSpecification.JoinTypeEnum.InnerJoin);
        }

        [Fact]
        public void Constructor_SetsJoinType_LeftOuterJoin()
        {
            var ov = new JoinOverride(typeof(object), JoinSpecification.JoinTypeEnum.LeftOuterJoin);
            ov.JoinType.Should().Be(JoinSpecification.JoinTypeEnum.LeftOuterJoin);
        }

        [Fact]
        public void Constructor_NullTargetType_ThrowsArgumentNullException()
        {
            Action act = () => _ = new JoinOverride(null, JoinSpecification.JoinTypeEnum.InnerJoin);
            act.Should().Throw<ArgumentNullException>().WithParameterName("targetType");
        }

        [Fact]
        public void InnerJoin_And_LeftOuterJoin_AreDistinct()
        {
            JoinSpecification.JoinTypeEnum.InnerJoin
                .Should().NotBe(JoinSpecification.JoinTypeEnum.LeftOuterJoin);
        }

        [Fact]
        public void TwoOverrides_WithSameType_CanHaveDifferentJoinTypes()
        {
            var inner = new JoinOverride(typeof(string), JoinSpecification.JoinTypeEnum.InnerJoin);
            var outer = new JoinOverride(typeof(string), JoinSpecification.JoinTypeEnum.LeftOuterJoin);

            inner.TargetType.Should().Be(outer.TargetType);
            inner.JoinType.Should().NotBe(outer.JoinType);
        }
    }
}
