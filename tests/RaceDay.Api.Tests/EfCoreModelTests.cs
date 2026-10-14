using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class EfCoreModelTests
{
    [Fact]
    public void ModelMapsAllSixEntitiesToThePartOneTables()
    {
        using var db = CreateSqlServerModelContext();

        var expectedTables = new Dictionary<Type, string>
        {
            [typeof(User)] = "Users",
            [typeof(RaceEvent)] = "Events",
            [typeof(Category)] = "Categories",
            [typeof(EventRoute)] = "EventRoutes",
            [typeof(Enrolment)] = "Enrolments",
            [typeof(RaceResult)] = "Results"
        };

        foreach (var (entityType, tableName) in expectedTables)
        {
            Assert.Equal(tableName, db.Model.FindEntityType(entityType)?.GetTableName());
        }
    }

    [Fact]
    public void EnrolmentCategoryForeignKeyUsesBothCategoryIdAndEventId()
    {
        using var db = CreateSqlServerModelContext();
        var enrolment = db.Model.FindEntityType(typeof(Enrolment))!;
        var categoryForeignKey = Assert.Single(enrolment.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Enrolment.CategoryID), nameof(Enrolment.EventID)]));

        Assert.Equal(typeof(Category), categoryForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(new[] { nameof(Category.CategoryID), nameof(Category.EventID) },
            categoryForeignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.False(categoryForeignKey.PrincipalKey.IsPrimaryKey());
        Assert.Equal(DeleteBehavior.NoAction, categoryForeignKey.DeleteBehavior);
    }

    [Fact]
    public void EnrolmentHasThePlannedDuplicatePreventionIndex()
    {
        using var db = CreateSqlServerModelContext();
        var enrolment = db.Model.FindEntityType(typeof(Enrolment))!;
        var uniqueIndex = Assert.Single(enrolment.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Enrolment.ParticipantID), nameof(Enrolment.EventID), nameof(Enrolment.CategoryID)]));

        Assert.Equal("UQ_Enrolments_Participant_Event_Category", uniqueIndex.GetDatabaseName());
    }

    [Fact]
    public void CodeFirstCreateScriptContainsTheSixTablesAndCompositeForeignKey()
    {
        using var db = CreateSqlServerModelContext();
        var createScript = db.Database.GenerateCreateScript();

        foreach (var table in new[] { "Users", "Events", "Categories", "EventRoutes", "Enrolments", "Results" })
        {
            Assert.Contains($"CREATE TABLE [dbo].[{table}]", createScript);
        }

        Assert.Contains("FK_Enrolments_CategoryEvent", createScript);
        Assert.Contains("FOREIGN KEY ([CategoryID], [EventID])", createScript);
        Assert.Contains("REFERENCES [dbo].[Categories] ([CategoryID], [EventID])", createScript);
        Assert.Single(db.Database.GetMigrations());
    }

    private static RaceDayDbContext CreateSqlServerModelContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new RaceDayDbContext(options);
    }
}
