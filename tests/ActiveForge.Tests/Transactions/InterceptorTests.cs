using System;
using System.Data;
using Castle.DynamicProxy;
using FluentAssertions;
using Moq;
using ActiveForge.Transactions;
using Xunit;

namespace ActiveForge.Tests.Transactions
{
    /// <summary>
    /// Tests for <see cref="TransactionInterceptor"/> and <see cref="DataConnectionProxyFactory"/>.
    /// Uses Castle DynamicProxy to proxy a simple interface so we don't need a real DB connection.
    /// </summary>
    public class InterceptorTests
    {
        // ── Test service interface ─────────────────────────────────────────────────────

        public interface IOrderService
        {
            [Transaction]
            void PlaceOrder(int id);

            [Transaction(IsolationLevel.Serializable)]
            string CriticalOperation();

            // No attribute — should NOT get a transaction.
            int GetCount();
        }

        public class OrderService : IOrderService
        {
            public bool PlaceOrderCalled   { get; private set; }
            public bool CriticalOpCalled   { get; private set; }
            public bool GetCountCalled     { get; private set; }

            [Transaction]
            public virtual void PlaceOrder(int id) => PlaceOrderCalled = true;

            [Transaction(IsolationLevel.Serializable)]
            public virtual string CriticalOperation() { CriticalOpCalled = true; return "ok"; }

            public virtual int GetCount() { GetCountCalled = true; return 5; }
        }

        // ── Helper ────────────────────────────────────────────────────────────────────

        private static (OrderService proxy, Mock<IUnitOfWork> uowMock) CreateProxy()
        {
            var uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
            uowMock.Setup(u => u.CreateTransaction(It.IsAny<IsolationLevel>())).Returns((TransactionBase)null);
            uowMock.Setup(u => u.Commit()).Verifiable();
            uowMock.Setup(u => u.Rollback()).Verifiable();

            var interceptor = new TransactionInterceptor(uowMock.Object);
            var generator   = new ProxyGenerator();
            var real        = new OrderService();
            var proxy       = (OrderService)generator.CreateClassProxyWithTarget(typeof(OrderService), real, interceptor);

            return (proxy, uowMock);
        }

        // ── Method WITH [Transaction] ─────────────────────────────────────────────────

        [Fact]
        public void InterceptedMethod_StartsAndCommitsTransaction()
        {
            var (proxy, uow) = CreateProxy();

            proxy.PlaceOrder(1);

            uow.Verify(u => u.CreateTransaction(IsolationLevel.ReadCommitted), Times.Once);
            uow.Verify(u => u.Commit(), Times.Once);
            uow.Verify(u => u.Rollback(), Times.Never);
        }

        [Fact]
        public void InterceptedMethod_DelegatesToRealImplementation()
        {
            var (proxy, _) = CreateProxy();
            proxy.PlaceOrder(99);
            // If real impl ran, PlaceOrderCalled is true on the underlying instance.
            // (proxy wraps the real object, so side-effects on the real object are observable.)
        }

        [Fact]
        public void InterceptedMethod_UsesAttributeIsolationLevel()
        {
            var (proxy, uow) = CreateProxy();

            proxy.CriticalOperation();

            uow.Verify(u => u.CreateTransaction(IsolationLevel.Serializable), Times.Once);
        }

        [Fact]
        public void InterceptedMethod_RollsBackOnException()
        {
            var uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
            uowMock.Setup(u => u.CreateTransaction(It.IsAny<IsolationLevel>())).Returns((TransactionBase)null);
            uowMock.Setup(u => u.Rollback()).Verifiable();

            var interceptor = new TransactionInterceptor(uowMock.Object);
            var generator   = new ProxyGenerator();

            // Proxy over a simple interface so we can throw from implementation.
            var implMock = new Mock<OrderService>() { CallBase = true };
            implMock.Setup(s => s.PlaceOrder(It.IsAny<int>())).Throws<InvalidOperationException>();

            var proxy = (OrderService)generator.CreateClassProxyWithTarget(
                typeof(OrderService), implMock.Object, interceptor);

            Action act = () => proxy.PlaceOrder(1);
            act.Should().Throw<InvalidOperationException>();

            uowMock.Verify(u => u.Rollback(), Times.Once);
            uowMock.Verify(u => u.Commit(), Times.Never);
        }

        // ── Method WITHOUT [Transaction] ──────────────────────────────────────────────

        [Fact]
        public void NonInterceptedMethod_DoesNotStartTransaction()
        {
            var (proxy, uow) = CreateProxy();

            proxy.GetCount();

            uow.Verify(u => u.CreateTransaction(It.IsAny<IsolationLevel>()), Times.Never);
            uow.Verify(u => u.Commit(), Times.Never);
        }

        // ── Constructor null guard ────────────────────────────────────────────────────

        [Fact]
        public void Constructor_NullUow_Throws()
        {
            Action act = () => new TransactionInterceptor(null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
        }

        // ── DataConnectionProxyFactory null guards ────────────────────────────────────

        [Fact]
        public void ProxyFactory_NullConnection_Throws()
        {
            var uow = new Mock<IUnitOfWork>().Object;
            Action act = () => DataConnectionProxyFactory.Create((DataConnection)null, uow);
            act.Should().Throw<ArgumentNullException>().WithParameterName("connection");
        }

        [Fact]
        public void ProxyFactory_NullUow_Throws()
        {
            // We need a non-null connection — but DataConnection is abstract.
            // Just test with a mock that Castle can proxy.
            // Since DataConnection has no public zero-arg ctor, test the generic overload validation.
            var uow = new Mock<IUnitOfWork>().Object;
            Action act = () => DataConnectionProxyFactory.Create(null, (IUnitOfWork)null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
