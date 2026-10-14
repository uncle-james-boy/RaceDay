using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceDay.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialRaceDaySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbo",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ProfilePictureUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                    table.CheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "dbo",
                columns: table => new
                {
                    EventID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganiserID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "varchar(max)", nullable: true),
                    EventDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    Location = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Distance = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EventType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    EventBannerUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventID);
                    table.CheckConstraint("CK_Events_Distance", "[Distance] > 0");
                    table.CheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')");
                    table.ForeignKey(
                        name: "FK_Events_Users",
                        column: x => x.OrganiserID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "dbo",
                columns: table => new
                {
                    CategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventID = table.Column<int>(type: "int", nullable: false),
                    CategoryName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    Distance = table.Column<decimal>(type: "decimal(5,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryID);
                    table.UniqueConstraint("UQ_Categories_CategoryID_EventID", x => new { x.CategoryID, x.EventID });
                    table.CheckConstraint("CK_Categories_AgeRange", "[MinAge] IS NULL OR [MaxAge] IS NULL OR [MinAge] <= [MaxAge]");
                    table.CheckConstraint("CK_Categories_Distance", "[Distance] IS NULL OR [Distance] > 0");
                    table.CheckConstraint("CK_Categories_MaxAge", "[MaxAge] IS NULL OR [MaxAge] >= 0");
                    table.CheckConstraint("CK_Categories_MinAge", "[MinAge] IS NULL OR [MinAge] >= 0");
                    table.ForeignKey(
                        name: "FK_Categories_Events",
                        column: x => x.EventID,
                        principalSchema: "dbo",
                        principalTable: "Events",
                        principalColumn: "EventID");
                });

            migrationBuilder.CreateTable(
                name: "EventRoutes",
                schema: "dbo",
                columns: table => new
                {
                    RouteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventID = table.Column<int>(type: "int", nullable: false),
                    RouteName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Distance = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    RouteDescription = table.Column<string>(type: "varchar(max)", nullable: true),
                    RouteMapUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRoutes", x => x.RouteID);
                    table.CheckConstraint("CK_EventRoutes_Distance", "[Distance] > 0");
                    table.ForeignKey(
                        name: "FK_EventRoutes_Events",
                        column: x => x.EventID,
                        principalSchema: "dbo",
                        principalTable: "Events",
                        principalColumn: "EventID");
                });

            migrationBuilder.CreateTable(
                name: "Enrolments",
                schema: "dbo",
                columns: table => new
                {
                    EnrolmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventID = table.Column<int>(type: "int", nullable: false),
                    ParticipantID = table.Column<int>(type: "int", nullable: false),
                    CategoryID = table.Column<int>(type: "int", nullable: false),
                    EnrolmentDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrolments", x => x.EnrolmentID);
                    table.CheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_Enrolments_CategoryEvent",
                        columns: x => new { x.CategoryID, x.EventID },
                        principalSchema: "dbo",
                        principalTable: "Categories",
                        principalColumns: new[] { "CategoryID", "EventID" });
                    table.ForeignKey(
                        name: "FK_Enrolments_Events",
                        column: x => x.EventID,
                        principalSchema: "dbo",
                        principalTable: "Events",
                        principalColumn: "EventID");
                    table.ForeignKey(
                        name: "FK_Enrolments_Users",
                        column: x => x.ParticipantID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Results",
                schema: "dbo",
                columns: table => new
                {
                    ResultID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrolmentID = table.Column<int>(type: "int", nullable: false),
                    FinishTime = table.Column<TimeSpan>(type: "time(0)", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.ResultID);
                    table.CheckConstraint("CK_Results_Position", "[Position] > 0");
                    table.ForeignKey(
                        name: "FK_Results_Enrolments",
                        column: x => x.EnrolmentID,
                        principalSchema: "dbo",
                        principalTable: "Enrolments",
                        principalColumn: "EnrolmentID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_EventID",
                schema: "dbo",
                table: "Categories",
                column: "EventID");

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_CategoryID_EventID",
                schema: "dbo",
                table: "Enrolments",
                columns: new[] { "CategoryID", "EventID" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_EventID",
                schema: "dbo",
                table: "Enrolments",
                column: "EventID");

            migrationBuilder.CreateIndex(
                name: "UQ_Enrolments_Participant_Event_Category",
                schema: "dbo",
                table: "Enrolments",
                columns: new[] { "ParticipantID", "EventID", "CategoryID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EventRoutes_EventID_RouteName",
                schema: "dbo",
                table: "EventRoutes",
                columns: new[] { "EventID", "RouteName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganiserID",
                schema: "dbo",
                table: "Events",
                column: "OrganiserID");

            migrationBuilder.CreateIndex(
                name: "UQ_Results_EnrolmentID",
                schema: "dbo",
                table: "Results",
                column: "EnrolmentID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Users_Email",
                schema: "dbo",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventRoutes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Results",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Enrolments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbo");
        }
    }
}
