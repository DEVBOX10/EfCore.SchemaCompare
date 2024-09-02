// Copyright (c) 2020 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using EfSchemaCompare;
using EfSchemaCompare.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.AssertExtensions;

namespace Test.UnitTests
{
    public class CompareLoggerTests
    {
        private readonly ITestOutputHelper _output;

        public CompareLoggerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void LogOK()
        {
            //SETUP
            bool errorLogged = false;
            var logs = new List<CompareLog>();
            var logger = new CompareLogger2(CompareType.Entity, "Test", logs, null,
                () => { errorLogged = true; });

            //ATTEMPT
            logger.MarkAsOk("MyValue");

            //VERIFY
            errorLogged.ShouldBeFalse();
            logs.Count.ShouldEqual(1);
            logs[0].ToString().ShouldEqual("OK: Entity 'Test'");
        }

        [Fact]
        public void LogNotInDatabase()
        {
            //SETUP
            bool errorLogged = false;
            var logs = new List<CompareLog>();
            var logger = new CompareLogger2(CompareType.Entity, "Test", logs, null,
                () => { errorLogged = true; });

            //ATTEMPT
            logger.NotInDatabase("MyValue");

            //VERIFY
            errorLogged.ShouldBeTrue();
            logs.Count.ShouldEqual(1);
            logs[0].ToString().ShouldEqual("NOT IN DATABASE: Entity 'Test'. Expected = MyValue");
        }

        [Fact]
        public void LogExtraEntityInDatabase()
        {
            //SETUP
            bool errorLogged = false;
            var logs = new List<CompareLog>();
            var logger = new CompareLogger2(CompareType.Entity, "Test", logs, null,
                () => { errorLogged = true; });

            //ATTEMPT
            logger.ExtraInDatabase("MyValue", CompareAttributes.ColumnName);

            //VERIFY
            errorLogged.ShouldBeTrue();
            logs.Count.ShouldEqual(1);
            logs[0].ToString().ShouldEqual("EXTRA IN DATABASE: Entity 'Test', column name. Found = MyValue");
        }

        [Fact]
        public void LogExtraDatabaseInDatabase()
        {
            //SETUP
            bool errorLogged = false;
            var logs = new List<CompareLog>();
            var logger = new CompareLogger2(CompareType.Database, "MyDatabase", logs, null,
                () => { errorLogged = true; });

            //ATTEMPT
            logger.ExtraInDatabase(null, CompareAttributes.NotSet, "MyDatabase");

            //VERIFY
            errorLogged.ShouldBeTrue();
            logs.Count.ShouldEqual(1);
            logs[0].ToString().ShouldEqual("EXTRA IN DATABASE: Database 'MyDatabase'");
        }


        [Fact]
        public void LogNotInDatabaseIgnore()
        {
            //SETUP
            bool errorLogged = false;
            var logs = new List<CompareLog>();
            var config = new CompareEfSqlConfig();
            config.AddIgnoreCompareLog(new CompareLog(CompareType.Entity, CompareState.NotInDatabase, null));

            //ATTEMPT
            var logger = new CompareLogger2(CompareType.Entity, "Test", logs, config.LogsToIgnore,
                () => { errorLogged = true; });
            logger.NotInDatabase("MyValue");

            //VERIFY
            errorLogged.ShouldBeFalse();
            logs.Count.ShouldEqual(0);
        }
    }
}
