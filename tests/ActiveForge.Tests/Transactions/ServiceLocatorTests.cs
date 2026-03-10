using System;
using FluentAssertions;
using Moq;
using ActiveForge.Transactions;
using Xunit;

namespace ActiveForge.Tests.Transactions
{
    /// <summary>Tests for ActiveForgeServiceLocator.</summary>
    public class ServiceLocatorTests : IDisposable
    {
        public ServiceLocatorTests() => ActiveForgeServiceLocator.Reset();
        public void Dispose()        => ActiveForgeServiceLocator.Reset();

        [Fact]
        public void GetUnitOfWork_WithFactory_ReturnsInstance()
        {
            var mock = new Mock<IUnitOfWork>();
            ActiveForgeServiceLocator.SetUnitOfWorkFactory(() => mock.Object);

            IUnitOfWork uow = ActiveForgeServiceLocator.GetUnitOfWork();

            uow.Should().BeSameAs(mock.Object);
        }

        [Fact]
        public void GetUnitOfWork_WithServiceProvider_ReturnsInstance()
        {
            var mock     = new Mock<IUnitOfWork>();
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(mock.Object);

            ActiveForgeServiceLocator.SetProvider(provider.Object);
            IUnitOfWork uow = ActiveForgeServiceLocator.GetUnitOfWork();

            uow.Should().BeSameAs(mock.Object);
        }

        [Fact]
        public void Resolve_UnregisteredType_Throws()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(It.IsAny<Type>())).Returns(null);
            ActiveForgeServiceLocator.SetProvider(provider.Object);

            Action act = () => ActiveForgeServiceLocator.Resolve<IUnitOfWork>();
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*no service registered*");
        }

        [Fact]
        public void Resolve_WithoutInit_Throws()
        {
            Action act = () => ActiveForgeServiceLocator.GetUnitOfWork();
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*not been initialised*");
        }

        [Fact]
        public void SetProvider_NullArg_Throws()
        {
            Action act = () => ActiveForgeServiceLocator.SetProvider(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void SetUnitOfWorkFactory_NullArg_Throws()
        {
            Action act = () => ActiveForgeServiceLocator.SetUnitOfWorkFactory(null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
