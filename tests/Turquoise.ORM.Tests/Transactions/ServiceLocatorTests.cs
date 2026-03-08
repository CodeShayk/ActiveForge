using System;
using FluentAssertions;
using Moq;
using Turquoise.ORM.Transactions;
using Xunit;

namespace Turquoise.ORM.Tests.Transactions
{
    /// <summary>Tests for TurquoiseServiceLocator.</summary>
    public class ServiceLocatorTests : IDisposable
    {
        public ServiceLocatorTests() => TurquoiseServiceLocator.Reset();
        public void Dispose()        => TurquoiseServiceLocator.Reset();

        [Fact]
        public void GetUnitOfWork_WithFactory_ReturnsInstance()
        {
            var mock = new Mock<IUnitOfWork>();
            TurquoiseServiceLocator.SetUnitOfWorkFactory(() => mock.Object);

            IUnitOfWork uow = TurquoiseServiceLocator.GetUnitOfWork();

            uow.Should().BeSameAs(mock.Object);
        }

        [Fact]
        public void GetUnitOfWork_WithServiceProvider_ReturnsInstance()
        {
            var mock     = new Mock<IUnitOfWork>();
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(mock.Object);

            TurquoiseServiceLocator.SetProvider(provider.Object);
            IUnitOfWork uow = TurquoiseServiceLocator.GetUnitOfWork();

            uow.Should().BeSameAs(mock.Object);
        }

        [Fact]
        public void Resolve_UnregisteredType_Throws()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(It.IsAny<Type>())).Returns(null);
            TurquoiseServiceLocator.SetProvider(provider.Object);

            Action act = () => TurquoiseServiceLocator.Resolve<IUnitOfWork>();
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*no service registered*");
        }

        [Fact]
        public void Resolve_WithoutInit_Throws()
        {
            Action act = () => TurquoiseServiceLocator.GetUnitOfWork();
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*not been initialised*");
        }

        [Fact]
        public void SetProvider_NullArg_Throws()
        {
            Action act = () => TurquoiseServiceLocator.SetProvider(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void SetUnitOfWorkFactory_NullArg_Throws()
        {
            Action act = () => TurquoiseServiceLocator.SetUnitOfWorkFactory(null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
