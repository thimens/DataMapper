//------------------------------------------------------------------------------
// This class is part of Microsoft Enterprise Library Data Block v6.0.1034 
// Some changes could have been made to run on netstandard2.0
//------------------------------------------------------------------------------

//===============================================================================
// Microsoft patterns & practices Enterprise Library
// Data Access Application Block
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Data.Common;

namespace Thimens.DataMapper
{
    /// <summary>
    /// <para>Represents a factory for creating named instances of <see cref="Database"/> objects.</para>
    /// </summary>
    public class DatabaseProviderFactory
    {
        private static readonly ConcurrentDictionary<string, DbProviderFactory> DbFactories = new ConcurrentDictionary<string, DbProviderFactory>();

        /// <summary>
        /// <para>Initializes a new instance of the <see cref="DatabaseProviderFactory"/> class 
        /// with the default configuration source.</para>
        /// </summary>
        public DatabaseProviderFactory()
        { }

        /// <summary>
        /// Register a new <see cref="DbProviderFactory"/>. If no alias for the factory is informed, the TypeName will be used.
        /// </summary>
        /// <param name="factory">The factory that will be used to create the Database object. </param>
        public static void RegisterFactory(DbProviderFactory factory)
        {
            RegisterFactory(factory, factory.GetType().Name);
        }

        /// <summary>
        /// Register a new <see cref="DbProviderFactory"/>.
        /// </summary>
        /// <param name="factory">The factory that will be used to create the Database object</param>
        /// <param name="alias">The factory name (alias)</param>
        public static void RegisterFactory(DbProviderFactory factory, string alias)
        {
            DbFactories.AddOrUpdate(alias, factory, (k, a) => { return a; });
        }

        /// <summary>
        /// Returns a new <see cref="Database"/> instance based on the <paramref name="connectionString"/> and <paramref name="factoryType"/>.
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <param name="factoryType">The type of the factory registred using the RegisterFactory method.</param>
        /// <returns></returns>
        public static Database Create(string connectionString, Type factoryType)
        {
            return Create(connectionString, factoryType.Name);
        }

        /// <summary>
        /// Returns a new <see cref="Database"/> instance based on the <paramref name="connectionString"/> and <paramref name="factoryName"/>.
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <param name="factoryName">The name of the factory registred using the RegisterFactory method.</param>
        /// <returns>
        /// A new Database instance.
        /// </returns>
        public static Database Create(string connectionString, string factoryName)
        {
            Guard.ArgumentNotNullOrEmpty(connectionString, "connectionString");

            try
            {
                if (DbFactories.Keys.Contains(factoryName))
                    return new GenericDatabase(connectionString, DbFactories[factoryName]);
                else
                    throw new ArgumentOutOfRangeException("Factory name not found. Please either use a registered factory alias or a type name of a registered factory (if no alias was informed), or register the factory using the RegisterFactory method.");
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture, Resources.ExceptionDatabaseInvalid, factoryName),
                    e);
            }
                    
        }
        
    }
}
