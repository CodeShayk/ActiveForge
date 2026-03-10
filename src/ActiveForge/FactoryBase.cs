using System;
using System.Collections.Generic;

namespace ActiveForge
{
    /// <summary>
    /// Provides polymorphic type-mapping: given an abstract base type, return the
    /// concrete type to instantiate.  Override <see cref="CreateTypeMap"/> to add entries.
    /// </summary>
    public class FactoryBase
    {
        private readonly Dictionary<Type, Type> _typeMap = new Dictionary<Type, Type>();

        public FactoryBase() { CreateTypeMap(); }

        protected virtual void CreateTypeMap() { }

        public Type MapType(Type sourceType)
        {
            if (_typeMap.TryGetValue(sourceType, out var mapped)) return mapped;
            return sourceType;
        }

        public void AddTypeMapping(Type baseType, Type concreteType)
        {
            _typeMap[baseType] = concreteType;
        }
    }
}
