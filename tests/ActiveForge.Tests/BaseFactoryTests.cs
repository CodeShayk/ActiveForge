using FluentAssertions;
using System;
using ActiveForge;
using Xunit;

namespace ActiveForge.Tests
{
    // Test helpers
    internal abstract class BaseAnimal : Record { }
    internal class Dog : BaseAnimal { }
    internal class Cat : BaseAnimal { }

    internal class AnimalFactory : BaseFactory
    {
        protected override void CreateTypeMap()
        {
            AddTypeMapping(typeof(BaseAnimal), typeof(Dog));
        }
    }

    public class BaseFactoryTests
    {
        // ── Default behaviour ─────────────────────────────────────────────────────────

        [Fact]
        public void MapType_UnmappedType_ReturnsSameType()
        {
            var factory = new BaseFactory();
            factory.MapType(typeof(Dog)).Should().Be(typeof(Dog));
        }

        [Fact]
        public void MapType_UnknownType_ReturnsSameType()
        {
            var factory = new BaseFactory();
            factory.MapType(typeof(string)).Should().Be(typeof(string));
        }

        // ── AddTypeMapping ────────────────────────────────────────────────────────────

        [Fact]
        public void AddTypeMapping_MappedType_ReturnsConcreteType()
        {
            var factory = new BaseFactory();
            factory.AddTypeMapping(typeof(BaseAnimal), typeof(Dog));
            factory.MapType(typeof(BaseAnimal)).Should().Be(typeof(Dog));
        }

        [Fact]
        public void AddTypeMapping_OverridesPreviousMapping()
        {
            var factory = new BaseFactory();
            factory.AddTypeMapping(typeof(BaseAnimal), typeof(Dog));
            factory.AddTypeMapping(typeof(BaseAnimal), typeof(Cat));
            factory.MapType(typeof(BaseAnimal)).Should().Be(typeof(Cat));
        }

        [Fact]
        public void AddTypeMapping_DoesNotAffectUnrelatedTypes()
        {
            var factory = new BaseFactory();
            factory.AddTypeMapping(typeof(BaseAnimal), typeof(Dog));
            factory.MapType(typeof(Cat)).Should().Be(typeof(Cat));
        }

        // ── Subclass CreateTypeMap ────────────────────────────────────────────────────

        [Fact]
        public void SubclassFactory_CreateTypeMap_RegistersOnConstruction()
        {
            var factory = new AnimalFactory();
            factory.MapType(typeof(BaseAnimal)).Should().Be(typeof(Dog));
        }

        [Fact]
        public void SubclassFactory_UnmappedType_StillReturnsSelf()
        {
            var factory = new AnimalFactory();
            factory.MapType(typeof(Cat)).Should().Be(typeof(Cat));
        }

        // ── Multiple mappings ─────────────────────────────────────────────────────────

        [Fact]
        public void MultipleTypeMappings_AllWork()
        {
            var factory = new BaseFactory();
            factory.AddTypeMapping(typeof(BaseAnimal), typeof(Dog));
            factory.AddTypeMapping(typeof(Dog), typeof(Cat));

            factory.MapType(typeof(BaseAnimal)).Should().Be(typeof(Dog));
            factory.MapType(typeof(Dog)).Should().Be(typeof(Cat));
            factory.MapType(typeof(Cat)).Should().Be(typeof(Cat));
        }
    }
}
