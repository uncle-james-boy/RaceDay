# RaceDay Event Management Platform

![CI](https://github.com/uncle-james-boy/RaceDay/actions/workflows/build.yml/badge.svg?branch=part2)

RaceDay helps Organisers manage road events and Participants browse events, enrol in a category,
view route details, and track results. The repository contains the six-entity Part 1 design and a
Part 2 ASP.NET Core Web API.

## Roles and API

- **Organiser:** manages only their own events, categories, and routes; views enrolments and records
  results for their events.
- **Participant:** browses public events, creates an account, enrols in a category, and views only
  their own enrolments and results.

The API uses EF Core with SQL Server and Code-First migrations. Authentication uses a server-side
session cookie that stores the user's ID and role; passwords are hashed. The API plan contains 23
operations across authentication, profiles, events, categories, routes, enrolments, and results.
The enrolment model preserves the composite `(CategoryID, EventID)` foreign key, so a participant
cannot select a category from a different event.

The six entities are Users, Events, Categories, EventRoutes, Enrolments, and Results. EventRoutes
stores named course details for an event: distance, optional description, and optional map URL.
The full plan and schema files are listed below.

## Project files

- `schema.dbml` — schema source.
- `docs/Part1_ERD.png` — the six entities, keys, constraints, and cardinalities.
- `docs/RaceDay_Database.sql` — SQL Server database and sample data setup.
- `docs/API_Endpoint_Plan.md` — methods, routes, roles, request bodies, and responses.
- `docs/Part2_Implementation_Blueprint.md` — mapping from Part 1 to EF Core.
- `src/RaceDay.Api/Data/Migrations` — the initial EF Core Code-First migration.
- `tests/RaceDay.Api.Tests` — automated controller, model, and real HTTP session-flow tests.
- `docs/Part2_Rubric_Checklist.md` — criterion-by-criterion status and manual evidence checklist.
- `docs/Video_Presentation_Guide.md` — recording checklists for Parts 1 and 2.
- `docs/Part2_Video_Presentation_Guide.md` — detailed Part 2 demonstration sequence.

## Database setup

The default development connection uses `(localdb)\\MSSQLLocalDB` and the database name
`RaceDayDB`. Choose one setup method for a new, empty database:

1. **Part 1 SQL script:** In SSMS, open `docs/RaceDay_Database.sql` and execute it against a fresh
   disposable SQL Server instance. It creates and selects `RaceDayDB`, creates all six tables, and
   adds sample records. It stops if RaceDay tables already exist; it does not drop or replace them.
2. **EF Core migration:** Install the EF command-line tool once, then update a fresh database:

   ```powershell
   dotnet tool install --global dotnet-ef --version 8.0.31
   dotnet ef database update --project src/RaceDay.Api/RaceDay.Api.csproj --startup-project src/RaceDay.Api/RaceDay.Api.csproj
   ```

   The migration creates the schema but does not add the Part 1 sample records. Do not apply the
   migration to a database already created by the SQL script: that database has no EF migration
   history, so EF would try to create tables that already exist. Never run setup against a database
   whose data must be kept. The sample password hashes in the SQL script are placeholders and
   cannot be used to sign in.

If your SQL Server is different from LocalDB, edit the connection string in
`src/RaceDay.Api/appsettings.Development.json`. The API does not create or migrate a database on
startup.

## Build, test, and run

From the repository root in PowerShell:

```powershell
dotnet restore RaceDay.sln
dotnet build RaceDay.sln --no-restore --configuration Release
dotnet test RaceDay.sln --no-build --no-restore --configuration Release
dotnet run --project src/RaceDay.Api/RaceDay.Api.csproj --launch-profile https
```

The HTTPS launch profile opens Swagger at `https://localhost:7107/swagger` (or follow the URL
printed by `dotnet run`). Swagger lists and describes the endpoints. To exercise database-backed
requests, first configure one of the database setup options above. The automated tests use an
in-memory database and a local HTTP server; the SQL Server model is inspected without connecting to
a database. A live SQL Server run has not been performed here.

## Verification and GitHub Actions

The workflow validates Part 1 files, converts the DBML, lints Markdown, restores and builds the
solution, and runs all tests on pushes and pull requests for `main` and `part2`. The local Part 1
validator and Markdown lint passed. The final Release build passed with 0 warnings and 0 errors,
and all 41 tests passed. NuGet restore succeeded using a repository-local configuration because
the sandbox could not read the roaming NuGet configuration. No GitHub Actions run or screenshot is
claimed: the `part2` branch and its work are local and have not been pushed.

## Submission evidence still to add

- [ ] **Part 1 CI screenshot:** after pushing the final Part 1/Part 2 branch, capture the genuine
  green GitHub Actions run for the submitted commit as `docs/evidence/part1-ci-success.png`.
- [ ] **Part 2 CI screenshot:** capture the genuine run showing successful build and test jobs as
  `docs/evidence/part2-ci-success.png`.
- [ ] **Part 1 video:** in your own voice, explain the ERD and six entities, cardinalities, key
  constraints, endpoint plan, and SQL design; run the script live in SSMS on a disposable database.
  Upload as Unlisted and add the real link below.
- [ ] **Part 2 video:** in your own voice, demonstrate the running API, all Swagger endpoint groups,
  the session cookie, both roles, ownership rules, enrolment/result workflows, composite EF key, and
  passing tests. Upload as Unlisted and add the real link below.
- [ ] **Optional supporting SQL screenshot:** save the SSMS database/table verification screen as
  `docs/evidence/part1-sql-run.png`. The brief requires the SQL script to run live in the Part 1
  video; it does not separately require this screenshot.
- [ ] Review and understand the code before submission, push the final `part2` branch to the
  existing repository, then submit `https://github.com/uncle-james-boy/RaceDay` on ARC and verify
  the submitted repository shows the latest commit.

Part 1 video: **to be added after recording**
Part 2 video: **to be added after recording**

See [`docs/Part2_Rubric_Checklist.md`](docs/Part2_Rubric_Checklist.md) for exact capture and
verification steps. No screenshots or recordings have been fabricated.

## AI assistance disclosure

AI assistance was used to review the assignment and help draft documentation and code. The student
must review, understand, and adapt the work in line with the assignment's academic integrity
instructions.
