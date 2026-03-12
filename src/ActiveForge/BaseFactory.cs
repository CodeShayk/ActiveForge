using System;
using System.Collections.Generic;

namespace ActiveForge
{
    /// <summary>
    /// Provides polymorphic type-mapping: given an abstract base type, return the
    /// concrete type to instantiate.  Override <see cref="CreateTypeMap"/> to add entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>BaseFactory</c> is used by <c>DataConnection</c> when it needs to instantiate a
    /// <c>DataObject</c> subclass from a <c>Type</c> token.  By registering a mapping from an
    /// abstract base type to a concrete subclass you can make the ORM return specialised objects
    /// without changing the query call site.
    /// </para>
    /// <para>
    /// Subclass <c>BaseFactory</c>, override <see cref="CreateTypeMap"/>, and call
    /// <see cref="AddTypeMapping"/> for each abstract→concrete pair.  Pass the factory instance
    /// to the <c>DataConnection</c> constructor or set it on <c>DataConnection.Factory</c>.
    /// </para>
    /// </remarks>
    public class BaseFactory
    {
        private readonly Dictionary<Type, Type> _typeMap = new Dictionary<Type, Type>();

        /// <summary>
        /// Initialises a new factory and invokes <see cref="CreateTypeMap"/> so that subclasses
        /// can register their type mappings during construction.
        /// </summary>
        public BaseFactory() { CreateTypeMap(); }

        /// <summary>
        /// Override this method and call <see cref="AddTypeMapping"/> for each abstract→concrete
        /// type pair you need the ORM to resolve.  Called once from the constructor.
        /// </summary>
        protected virtual void CreateTypeMap() { }

        /// <summary>
        /// Returns the concrete type registered for <paramref name="sourceType"/>, or
        /// <paramref name="sourceType"/> itself when no mapping has been registered.
        /// </summary>
        /// <param name="sourceType">
        /// The type token to look up — typically an abstract <c>DataObject</c> base class.
        /// </param>
        /// <returns>
        /// The mapped concrete type, or <paramref name="sourceType"/> if no mapping exists.
        /// </returns>
        public Type MapType(Type sourceType)
        {
            if (_typeMap.TryGetValue(sourceType, out var mapped)) return mapped;
            return sourceType;
        }

        /// <summary>
        /// Registers a mapping so that requests for <paramref name="baseType"/> are fulfilled by
        /// instantiating <paramref name="concreteType"/> instead.
        /// </summary>
        /// <param name="baseType">
        /// The abstract or base type that the ORM will encounter at query time.
        /// </param>
        /// <param name="concreteType">
        /// The concrete subclass to instantiate.  Must be assignable to <paramref name="baseType"/>.
        /// </param>
        public void AddTypeMapping(Type baseType, Type concreteType)
        {
            _typeMap[baseType] = concreteType;
        }
    }
}
