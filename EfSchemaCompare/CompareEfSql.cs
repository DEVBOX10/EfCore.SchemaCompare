﻿// Copyright (c) 2020 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using EfSchemaCompare.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.Configuration;

namespace EfSchemaCompare
{
    /// <summary>
    /// This is the main class for Comparing EF Core DbContexts against a database to see if they differ
    /// </summary>
    public class CompareEfSql
    {
        private readonly Assembly _callingAssembly;
        private readonly CompareEfSqlConfig _config;

        private readonly List<CompareLog> _logs = new List<CompareLog>();

        /// <summary>
        /// This creates the comparer class that you use for comparing EF Core DbContexts to a database
        /// </summary>
        /// <param name="config"></param>
        public CompareEfSql(CompareEfSqlConfig config = null)
        {
            _callingAssembly = Assembly.GetCallingAssembly();
            _config = config ?? new CompareEfSqlConfig();
        }

        /// <summary>
        /// This returns a single string containing all the errors found
        /// Each error is on a separate line
        /// </summary>
        public string GetAllErrors => string.Join(Environment.NewLine, CompareLog.ListAllErrors(Logs));

        /// <summary>
        /// This gives you access to the full log. but its not an easy thing to parse
        /// Look at the CompareLog class for various static methods that will output the log in a human-readable format
        /// </summary>
        public IReadOnlyList<CompareLog> Logs => _logs.ToImmutableList();

        /// <summary>
        /// This will compare one or more DbContext against database pointed to the first DbContext.
        /// </summary>
        /// <param name="dbContexts">One or more dbContext instances to be compared with the database</param>
        /// <returns>true if any errors found, otherwise false</returns>
        public bool CompareEfWithDb(params DbContext[] dbContexts)
        {
            if (dbContexts == null) throw new ArgumentNullException(nameof(dbContexts));
            if (dbContexts.Length == 0)
                throw new ArgumentException("You must provide at least one DbContext instance.", nameof(dbContexts));
            return CompareEfWithDb(dbContexts[0].Database.GetDbConnection().ConnectionString, dbContexts);
        }

        /// <summary>
        /// This will compare one or more DbContext against database pointed to by the connectionString.
        /// </summary>
        /// <param name="configOrConnectionString">This should either be a 
        /// connection string or the name of a connection string in the appsetting.json file.
        /// </param>
        /// <param name="dbContexts">One or more dbContext instances to be compared with the database</param>
        /// <returns>true if any errors found, otherwise false</returns>
        public bool CompareEfWithDb(string configOrConnectionString, params DbContext[] dbContexts )
        {
            if (dbContexts == null) throw new ArgumentNullException(nameof(dbContexts));
            if (dbContexts.Length == 0)
                throw new ArgumentException("You must provide at least one DbContext instance.", nameof(dbContexts));

            return FinishRestOfCompare(GetConfigurationOrActualString(configOrConnectionString), dbContexts);
        }

        //------------------------------------------------------
        //private methods

        private bool FinishRestOfCompare(string connectionString, DbContext[] dbContexts)
        {
            var databaseModel = GetDatabaseModelViaScaffolder(dbContexts, connectionString);
            bool hasErrors = false;
            foreach (var context in dbContexts)
            {
                var stage1Comparer = new Stage1Comparer(context, _config, _logs);
                hasErrors |= stage1Comparer.CompareModelToDatabase(databaseModel);
            }

            if (hasErrors) return true;

            //No errors, so its worth running the second phase
            var stage2Comparer = new Stage2Comparer(databaseModel, _config);
            hasErrors = stage2Comparer.CompareLogsToDatabase(_logs);
            _logs.AddRange(stage2Comparer.Logs);
            return hasErrors;
        }

        private  DatabaseModel GetDatabaseModelViaScaffolder(DbContext[] contexts, string connectionString)
        {
            var databaseFactory = contexts[0].GetDatabaseModelFactory();

            var databaseModel = databaseFactory.Create(connectionString,
                new DatabaseModelFactoryOptions(new string[] { }, new string[] { }));
            RemoveAnyTableToIgnore(databaseModel, contexts);
            return databaseModel;
        }

        private void RemoveAnyTableToIgnore(DatabaseModel databaseModel, DbContext[] contexts)
        {
            var tablesToRemove = new List<DatabaseTable>();
            if (_config.TablesToIgnoreCommaDelimited == null)
            {
                //We remove all tables not mapped by the contexts
                var tablesInContext = contexts.SelectMany(x => x.Model.GetEntityTypes())
                    .Select(x => x.FormSchemaTableFromModel()).ToList();

                tablesToRemove = databaseModel.Tables
                    .Where(x => !tablesInContext.Contains(x.FormSchemaTableFromDatabase(databaseModel.DefaultSchema), StringComparer.InvariantCultureIgnoreCase)).ToList();
            }
            else
            {
                
                foreach (var tableToIgnore in _config.TablesToIgnoreCommaDelimited.Split(',')
                    .Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
                {
                    var split = tableToIgnore.Split('.').Select(x => x.Trim()).ToArray();
                    var schema = split.Length == 1 ? databaseModel.DefaultSchema : split[0];
                    var tableName = split.Length == 1 ? split[0] : split[1];
                    var tableToRemove = databaseModel.Tables
                        .SingleOrDefault(x => x.Schema.Equals(schema, StringComparison.InvariantCultureIgnoreCase)
                                           && x.Name.Equals(tableName, StringComparison.InvariantCultureIgnoreCase));
                    if (tableToRemove == null)
                        throw new InvalidOperationException(
                            $"The TablesToIgnoreCommaDelimited config property contains a table name of '{tableToIgnore}', which was not found in the database");
                    tablesToRemove.Add(tableToRemove);
                }
            }
            foreach (var tableToRemove in tablesToRemove)
            {
                databaseModel.Tables.Remove(tableToRemove);
            }
        }

        private string GetConfigurationOrActualString(string configOrConnectionString)
        {
            var config = AppSettings.GetConfiguration(_callingAssembly);
            var connectionFromConfigFile = config?.GetConnectionString(configOrConnectionString);
            return connectionFromConfigFile ?? configOrConnectionString;
        }
    }
}