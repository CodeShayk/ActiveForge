using FluentAssertions;
using System;
using ActiveForge;
using Xunit;

namespace ActiveForge.Tests
{
    public class PersistenceExceptionTests
    {
        [Fact]
        public void CanThrowAndCatch()
        {
            Action act = () => throw new PersistenceException("test error");
            act.Should().Throw<PersistenceException>().WithMessage("test error");
        }

        [Fact]
        public void Message_IsAccessible()
        {
            var ex = new PersistenceException("hello");
            ex.Message.Should().Be("hello");
        }

        [Fact]
        public void InnerException_IsPreserved()
        {
            var inner = new InvalidOperationException("root cause");
            var ex = new PersistenceException("wrapper", inner);
            ex.InnerException.Should().BeSameAs(inner);
            ex.Message.Should().Be("wrapper");
        }

        [Fact]
        public void IsException()
        {
            var ex = new PersistenceException("test");
            ex.Should().BeAssignableTo<Exception>();
        }
    }
}
