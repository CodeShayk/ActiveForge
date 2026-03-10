using FluentAssertions;
using System;
using ActiveForge;
using Xunit;

namespace ActiveForge.Tests
{
    // Test helpers
    internal abstract class AnimalBase : Record { }
    internal class Dog : AnimalBase { }
    internal class Cat : AnimalBase { }

    internal class AnimalFactory : FactoryBase
    {
        protected override void CreateTypeMap()
        {
            AddTypeMapping(typeof(AnimalBase), typeof(Dog));
        }
    }

    public class FactoryBaseTests
    {
        // ── Default behaviour ─────────────────────────────────────────────────────────

        [Fact]
        public void MapType_UnmappedType_ReturnsSameType()
        {
            var factory = new FactoryBase();
            factory.MapType(typeof(Dog)).Should().Be(typeof(Dog));
        }

        [Fact]
        public void MapType_UnknownType_ReturnsSameType()
        {
            var factory = new FactoryBase();
            factory.MapType(typeof(string)).Should().Be(typeof(string));
        }

        // ── AddTypeMapping ────────────────────────────────────────────────────────────

        [Fact]
        public void AddTypeMapping_MappedType_ReturnsConcreteType()
        {
            var factory = new FactoryBase();
            factory.AddTypeMapping(typeof(AnimalBase), typeof(Dog));
            factory.MapType(typeof(AnimalBase)).Should().Be(typeof(Dog));
        }

        [Fact]
        public void AddTypeMapping_OverridesPreviousMapping()
        {
            var factory = new FactoryBase();
            factory.AddTypeMapping(typeof(AnimalBase), typeof(Dog));
            factory.AddTypeMapping(typeof(AnimalBase), typeof(Cat));
            factory.MapType(typeof(AnimalBase)).Should().Be(typeof(Cat));
        }

        [Fact]
        public void AddTypeMapping_DoesNotAffectUnrelatedTypes()
        {
            var factory = new FactoryBase();
            factory.AddTypeMapping(typeof(AnimalBase), typeof(Dog));
            factory.MapType(typeof(Cat)).Should().Be(typeof(Cat));
        }

        // ── Subclass CreateTypeMap ────────────────────────────────────────────────────

        [Fact]
        public void SubclassFactory_CreateTypeMap_RegistersOnConstruction()
        {
            var factory = new AnimalFactory();
            factory.MapType(typeof(AnimalBase)).Should().Be(typeof(Dog));
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
            var factory = new FactoryBase();
            factory.AddTypeMapping(typeof(AnimalBase), typeof(Dog));
            factory.AddTypeMapping(typeof(Dog), typeof(Cat));

            factory.MapType(typeof(AnimalBase)).Should().Be(typeof(Dog));
            factory.MapType(typeof(Dog)).Should().Be(typeof(Cat));
            factory.MapType(typeof(Cat)).Should().Be(typeof(Cat));
        }
    }
}
