using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data.Entities;

namespace RaceDay.Api.Data;

public sealed class RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RaceEvent> Events => Set<RaceEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventRoute> EventRoutes => Set<EventRoute>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<RaceResult> Results => Set<RaceResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUsers(modelBuilder);
        ConfigureEvents(modelBuilder);
        ConfigureCategories(modelBuilder);
        ConfigureEventRoutes(modelBuilder);
        ConfigureEnrolments(modelBuilder);
        ConfigureResults(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("Users", "dbo", table => table.HasCheckConstraint(
            "CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
        user.HasKey(item => item.UserID).HasName("PK_Users");
        user.Property(item => item.UserID).UseIdentityColumn();
        user.Property(item => item.FullName).HasMaxLength(100).IsUnicode(false).IsRequired();
        user.Property(item => item.Email).HasMaxLength(150).IsUnicode(false).IsRequired();
        user.Property(item => item.PasswordHash).HasMaxLength(255).IsUnicode(false).IsRequired();
        user.Property(item => item.Role).HasMaxLength(20).IsUnicode(false).IsRequired();
        user.Property(item => item.ProfilePictureUrl).HasMaxLength(500).IsUnicode(false);
        user.Property(item => item.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        user.HasIndex(item => item.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
    }

    private static void ConfigureEvents(ModelBuilder modelBuilder)
    {
        var raceEvent = modelBuilder.Entity<RaceEvent>();
        raceEvent.ToTable("Events", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')");
            table.HasCheckConstraint("CK_Events_Distance", "[Distance] > 0");
        });
        raceEvent.HasKey(item => item.EventID).HasName("PK_Events");
        raceEvent.Property(item => item.EventID).UseIdentityColumn();
        raceEvent.Property(item => item.Name).HasMaxLength(150).IsUnicode(false).IsRequired();
        raceEvent.Property(item => item.Description).HasColumnType("varchar(max)");
        raceEvent.Property(item => item.EventDate).HasColumnType("datetime");
        raceEvent.Property(item => item.Location).HasMaxLength(200).IsUnicode(false).IsRequired();
        raceEvent.Property(item => item.Distance).HasColumnType("decimal(5,2)");
        raceEvent.Property(item => item.EventType).HasMaxLength(20).IsUnicode(false).IsRequired();
        raceEvent.Property(item => item.EventBannerUrl).HasMaxLength(500).IsUnicode(false);
        raceEvent.Property(item => item.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        raceEvent.HasOne(item => item.Organiser).WithMany(user => user.OrganisedEvents)
            .HasForeignKey(item => item.OrganiserID).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Events_Users");
    }

    private static void ConfigureCategories(ModelBuilder modelBuilder)
    {
        var category = modelBuilder.Entity<Category>();
        category.ToTable("Categories", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Categories_MinAge", "[MinAge] IS NULL OR [MinAge] >= 0");
            table.HasCheckConstraint("CK_Categories_MaxAge", "[MaxAge] IS NULL OR [MaxAge] >= 0");
            table.HasCheckConstraint("CK_Categories_AgeRange", "[MinAge] IS NULL OR [MaxAge] IS NULL OR [MinAge] <= [MaxAge]");
            table.HasCheckConstraint("CK_Categories_Distance", "[Distance] IS NULL OR [Distance] > 0");
        });
        category.HasKey(item => item.CategoryID).HasName("PK_Categories");
        category.Property(item => item.CategoryID).UseIdentityColumn();
        category.Property(item => item.CategoryName).HasMaxLength(100).IsUnicode(false).IsRequired();
        category.Property(item => item.Distance).HasColumnType("decimal(5,2)");
        category.HasAlternateKey(item => new { item.CategoryID, item.EventID })
            .HasName("UQ_Categories_CategoryID_EventID");
        category.HasOne(item => item.Event).WithMany(raceEvent => raceEvent.Categories)
            .HasForeignKey(item => item.EventID).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Categories_Events");
    }

    private static void ConfigureEventRoutes(ModelBuilder modelBuilder)
    {
        var route = modelBuilder.Entity<EventRoute>();
        route.ToTable("EventRoutes", "dbo", table =>
            table.HasCheckConstraint("CK_EventRoutes_Distance", "[Distance] > 0"));
        route.HasKey(item => item.RouteID).HasName("PK_EventRoutes");
        route.Property(item => item.RouteID).UseIdentityColumn();
        route.Property(item => item.RouteName).HasMaxLength(100).IsUnicode(false).IsRequired();
        route.Property(item => item.Distance).HasColumnType("decimal(5,2)");
        route.Property(item => item.RouteDescription).HasColumnType("varchar(max)");
        route.Property(item => item.RouteMapUrl).HasMaxLength(500).IsUnicode(false);
        route.HasIndex(item => new { item.EventID, item.RouteName }).IsUnique()
            .HasDatabaseName("UQ_EventRoutes_EventID_RouteName");
        route.HasOne(item => item.Event).WithMany(raceEvent => raceEvent.Routes)
            .HasForeignKey(item => item.EventID).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_EventRoutes_Events");
    }

    private static void ConfigureEnrolments(ModelBuilder modelBuilder)
    {
        var enrolment = modelBuilder.Entity<Enrolment>();
        enrolment.ToTable("Enrolments", "dbo", table =>
            table.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')"));
        enrolment.HasKey(item => item.EnrolmentID).HasName("PK_Enrolments");
        enrolment.Property(item => item.EnrolmentID).UseIdentityColumn();
        enrolment.Property(item => item.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        enrolment.Property(item => item.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Pending");
        enrolment.HasIndex(item => new { item.ParticipantID, item.EventID, item.CategoryID }).IsUnique()
            .HasDatabaseName("UQ_Enrolments_Participant_Event_Category");
        enrolment.HasOne(item => item.Event).WithMany(raceEvent => raceEvent.Enrolments)
            .HasForeignKey(item => item.EventID).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Enrolments_Events");
        enrolment.HasOne(item => item.Participant).WithMany(user => user.ParticipantEnrolments)
            .HasForeignKey(item => item.ParticipantID).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Enrolments_Users");
        enrolment.HasOne(item => item.Category).WithMany(category => category.Enrolments)
            .HasForeignKey(item => new { item.CategoryID, item.EventID })
            .HasPrincipalKey(category => new { category.CategoryID, category.EventID })
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Enrolments_CategoryEvent");
    }

    private static void ConfigureResults(ModelBuilder modelBuilder)
    {
        var result = modelBuilder.Entity<RaceResult>();
        result.ToTable("Results", "dbo", table =>
            table.HasCheckConstraint("CK_Results_Position", "[Position] > 0"));
        result.HasKey(item => item.ResultID).HasName("PK_Results");
        result.Property(item => item.ResultID).UseIdentityColumn();
        result.Property(item => item.FinishTime).HasColumnType("time(0)");
        result.Property(item => item.CapturedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        result.HasIndex(item => item.EnrolmentID).IsUnique().HasDatabaseName("UQ_Results_EnrolmentID");
        result.HasOne(item => item.Enrolment).WithOne(enrolment => enrolment.Result)
            .HasForeignKey<RaceResult>(item => item.EnrolmentID).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Results_Enrolments");
    }
}
