using System.Linq;
using DataLayer.MyEntityDb.EfCompareDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TestSupport.DesignTimeServices;
using TestSupport.EfHelpers;
using TestSupport.EfSchemeCompare;
using TestSupport.EfSchemeCompare.Internal;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.AssertExtensions;

namespace Test.UnitTests.EfSchemaCompare
{
    public class ComparerMyEntityDiff
    {
        private readonly ITestOutputHelper _output;
        private readonly string _connectionString;
        private readonly DbContextOptions<MyEntityDbContext> _options;
        private readonly DatabaseModel _databaseModel;
        public ComparerMyEntityDiff(ITestOutputHelper output)
        {
            _output = output;
            _options = this
                .CreateUniqueClassOptions<MyEntityDbContext>();
            var serviceProvider = DatabaseProviders.SqlServer.GetDesignTimeProvider();
            var factory = serviceProvider.GetService<IDatabaseModelFactory>();

            using (var context = new MyEntityDbContext(_options))
            {
                _connectionString = context.Database.GetDbConnection().ConnectionString;
                context.Database.EnsureCreated();
                _databaseModel = factory.Create(_connectionString, new string[] { }, new string[] { });
            }
        }

        [Fact]
        public void CompareDefaultConfigNoErrors()
        {
            //SETUP
            using (var context = new MyEntityDbContext(_options))
            {
                var model = context.Model;
                var handler = new DbContextComparer(model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeFalse();
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new StringEnumConverter());
                var json = JsonConvert.SerializeObject(handler.Logs, settings);
            }
        }

        [Fact]
        public void CompareSchemaConfig()
        {
            //SETUP
            var optionsBuilder = new DbContextOptionsBuilder<MyEntitySetSchemaDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            using (var context = new MyEntitySetSchemaDbContext(optionsBuilder.Options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                CompareLog.ListAllErrors(handler.Logs).Single().ShouldEqual(
                    "NOT IN DATABASE: Entity 'MyEntity', table name. Expected = MySchema.MyEntities");
            }
        }

        [Fact]
        public void CompareExtraProperty()
        {
            //SETUP
            var optionsBuilder = new DbContextOptionsBuilder<MyEntityExtraPropDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            using (var context = new MyEntityExtraPropDbContext(optionsBuilder.Options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                CompareLog.ListAllErrors(handler.Logs).Single().ShouldEqual(
                    "NOT IN DATABASE: MyEntity->Property 'ShadowProp', column name. Expected = ShadowProp");
            }
        }

        [Fact]
        public void ComparePropertyDiffColName()
        {
            //SETUP
            var optionsBuilder = new DbContextOptionsBuilder<MyEntityPropertyDiffColDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            using (var context = new MyEntityPropertyDiffColDbContext(optionsBuilder.Options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                CompareLog.ListAllErrors(handler.Logs).Single().ShouldEqual(
                    "NOT IN DATABASE: MyEntity->Property 'MyInt', column name. Expected = OtherColName");
            }
        }

        [Fact]
        public void ComparePropertyDiffNullName()
        {
            //SETUP
            var optionsBuilder = new DbContextOptionsBuilder<MyEntityPropertyDiffNullDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            using (var context = new MyEntityPropertyDiffNullDbContext(optionsBuilder.Options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                CompareLog.ListAllErrors(handler.Logs).Single().ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyString', nullability. Expected = NOT NULL, found = NULL");
            }
        }

        [Fact]
        public void ComparePropertyDiffTypeName()
        {
            //SETUP
            var optionsBuilder = new DbContextOptionsBuilder<MyEntityPropertyDiffTypeDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            using (var context = new MyEntityPropertyDiffTypeDbContext(optionsBuilder.Options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                CompareLog.ListAllErrors(handler.Logs).Single().ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyString', column type. Expected = varchar(max), found = nvarchar(max)");
            }
        }

        [Fact]
        public void CompareDiffPrimaryKey()
        {
            //SETUP
            var optionsBuilder = new DbContextOptionsBuilder<MyEntityDiffPKeyDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            using (var context = new MyEntityDiffPKeyDbContext(optionsBuilder.Options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                var errors = CompareLog.ListAllErrors(handler.Logs).ToList();
                errors.Count.ShouldEqual(3);
                errors[0].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyInt', value generated. Expected = OnAdd, found = Never");
                errors[1].ShouldEqual(
                    "NOT IN DATABASE: MyEntity->PrimaryKey 'PK_MyEntites', column name. Expected = MyInt");
                errors[2].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyEntityId', value generated. Expected = Never, found = OnAdd");
            }
        }
        [Fact]
        public void ComparePropertyComputedColName()
        {
            //SETUP
            var optionsBuilder = new DbContextOptionsBuilder<MyEntityComputedColDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            using (var context = new MyEntityComputedColDbContext(optionsBuilder.Options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                var errors = CompareLog.ListAllErrors(handler.Logs).ToList();
                errors.Count.ShouldEqual(2);
                errors[0].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyDateTime', computed column sql. Expected = getutcdate(), found = <null>");
                errors[1].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyDateTime', value generated. Expected = OnAddOrUpdate, found = Never");
            }
        }

        private DbContextOptions<MyEntityComputedColDbContext> GetComputedColDbOptions()
        {
            var options = this.CreateUniqueMethodOptions<MyEntityComputedColDbContext>();
            return options;
        }

        [Fact]
        public void ComparePropertyComputedColSelf()
        {
            //SETUP
            var serviceProvider = DatabaseProviders.SqlServer.GetDesignTimeProvider();
            var factory = serviceProvider.GetService<IDatabaseModelFactory>();
            var options = GetComputedColDbOptions();
            using (var context = new MyEntityComputedColDbContext(options))
            {
                var connectionString = context.Database.GetDbConnection().ConnectionString;
                context.Database.EnsureCreated();
                var localDatabaseModel = factory.Create(connectionString, new string[] { }, new string[] { });

                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(localDatabaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                //The setting of a computed col changed the column type
                CompareLog.ListAllErrors(handler.Logs).Single().ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyDateTime', column type. Expected = datetime2, found = datetime");
            }
        }

        [Fact]
        public void ComparePropertyComputedColNameReversed()
        {
            //SETUP
            var serviceProvider = DatabaseProviders.SqlServer.GetDesignTimeProvider();
            var factory = serviceProvider.GetService<IDatabaseModelFactory>();
            var options = GetComputedColDbOptions();
            DatabaseModel localDatabaseModel;
            using (var context = new MyEntityComputedColDbContext(options))
            {
                var connectionString = context.Database.GetDbConnection().ConnectionString;
                context.Database.EnsureCreated();
                localDatabaseModel = factory.Create(connectionString, new string[] { }, new string[] { });
            }

            using (var context = new MyEntityDbContext(_options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(localDatabaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                var errors = CompareLog.ListAllErrors(handler.Logs).ToList();
                errors.Count.ShouldEqual(3);
                errors[0].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyDateTime', column type. Expected = datetime2, found = datetime");
                errors[1].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyDateTime', computed column sql. Expected = <null>, found = getutcdate()");
                errors[2].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyDateTime', value generated. Expected = Never, found = OnAddOrUpdate");
            }
        }

        [Fact]
        public void ComparePropertySqlDefaultName()
        {
            //SETUP
            var optionsBuilder = new DbContextOptionsBuilder<MyEntitySqlDefaultDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            using (var context = new MyEntitySqlDefaultDbContext(optionsBuilder.Options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(_databaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                var errors = CompareLog.ListAllErrors(handler.Logs).ToList();
                errors.Count.ShouldEqual(2);
                errors[0].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyInt', default value sql. Expected = 123, found = <null>");
                errors[1].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyInt', value generated. Expected = OnAdd, found = Never");
            }
        }


        private DbContextOptions<MyEntitySqlDefaultDbContext> GetDefaultSqlDbOptions()
        {
            var options = this.CreateUniqueMethodOptions<MyEntitySqlDefaultDbContext>();
            return options;
        }

        [Fact]
        public void ComparePropertySqlDefaultSelf()
        {
            //SETUP
            var serviceProvider = DatabaseProviders.SqlServer.GetDesignTimeProvider();
            var factory = serviceProvider.GetService<IDatabaseModelFactory>();
            var options = GetDefaultSqlDbOptions();
            using (var context = new MyEntitySqlDefaultDbContext(options))
            {
                var connectionString = context.Database.GetDbConnection().ConnectionString;
                context.Database.EnsureCreated();
                var localDatabaseModel = factory.Create(connectionString, new string[] { }, new string[] { });

                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(localDatabaseModel);

                //VERIFY
                hasErrors.ShouldBeFalse();
                //CompareLog.ListAllErrors(handler.Logs).Single().ShouldEqual(
                //    "DIFFERENT: Property 'MyInt', value generated. Expected = OnAdd, found = Never");
            }
        }

        [Fact]
        public void ComparePropertySqlDefaultReversed()
        {
            //SETUP
            var serviceProvider = DatabaseProviders.SqlServer.GetDesignTimeProvider();
            var factory = serviceProvider.GetService<IDatabaseModelFactory>();
            var options = GetDefaultSqlDbOptions();
            DatabaseModel localDatabaseModel;
            using (var context = new MyEntitySqlDefaultDbContext(options))
            {
                var connectionString = context.Database.GetDbConnection().ConnectionString;
                context.Database.EnsureCreated();
                localDatabaseModel = factory.Create(connectionString, new string[] { }, new string[] { });
            }

            using (var context = new MyEntityDbContext(_options))
            {
                var handler = new DbContextComparer(context.Model, context.GetType().Name);

                //ATTEMPT
                var hasErrors = handler.CompareModelToDatabase(localDatabaseModel);

                //VERIFY
                hasErrors.ShouldBeTrue();
                var errors = CompareLog.ListAllErrors(handler.Logs).ToList();
                errors.Count.ShouldEqual(2);
                errors[0].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyInt', default value sql. Expected = <null>, found = 123");
                errors[1].ShouldEqual(
                    "DIFFERENT: MyEntity->Property 'MyInt', value generated. Expected = Never, found = OnAdd");
            }
        }

    }
}
