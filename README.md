# RaceDay Event Management Platform

![CI](https://github.com/uncle-james-boy/RaceDay/actions/workflows/build.yml/badge.svg)

## Project overview

RaceDay helps Organisers manage road events and Participants browse events, enrol in a category,
view route details, and track results. This repository contains Part 1's plan and schema plus the
beginning of the Part 2 ASP.NET Core Web API.

The API foundation currently includes the six EF Core entities, SQL Server configuration,
registration, login, logout, password hashing, and server-side session storage. Eight authentication
unit tests currently pass. Event, category, route, profile, enrolment, and result endpoints and
their role and ownership checks are still being implemented.

## Part 1 files

- `schema.dbml` is the schema source.
- `docs/Part1_ERD.png` shows the six entities, keys, constraints, and cardinalities.
- `docs/RaceDay_Database.sql` creates the schema and sample records on a fresh SQL Server.
- `docs/API_Endpoint_Plan.md` specifies the API methods, routes, roles, bodies, and responses.
- `docs/Part2_Implementation_Blueprint.md` maps the model to EF Core.
- `docs/Part2_Rubric_Checklist.md` maps Part 2 rubric criteria to implementation, tests, and evidence.
- `docs/Video_Presentation_Guide.md` outlines the Part 1 walkthrough.

The six entities are Users, Events, Categories, EventRoutes, Enrolments, and Results. EventRoutes
stores named course details for an event: distance, optional description, and optional map URL. An
event can have multiple routes. Enrolments use a composite foreign key so that their CategoryID
must belong to the same EventID.

## SQL Server setup

In SSMS, connect to SQL Server and open `docs/RaceDay_Database.sql`. It creates `RaceDayDB` if
needed and selects it. The script does not drop or replace tables. If it detects existing RaceDay
tables, it stops; use a reviewed migration for an existing database. The sample password hashes
are placeholders and cannot be used to sign in. Do not run the setup script against a database
whose data must be preserved.

## Build and run

The development connection string uses `(localdb)\\MSSQLLocalDB`. Before using database-backed
endpoints, create the schema on a fresh SQL Server instance with the safe setup instructions above,
or edit `src/RaceDay.Api/appsettings.Development.json` for your SQL Server. The API can start before
the database is available, but database-backed requests will fail until the connection works.

From the repository root, run:

```powershell
dotnet restore RaceDay.sln -m:1
dotnet build RaceDay.sln --no-restore --configuration Release -m:1
dotnet test RaceDay.sln --no-build --no-restore --configuration Release -m:1
dotnet run --project src/RaceDay.Api/RaceDay.Api.csproj
```

When the API is running in Development, open the Swagger URL printed by `dotnet run`. The current
unit tests use an in-memory database and do not require executing the SQL script.

## Validation and CI

GitHub Actions is configured to validate the Part 1 files and DBML, then restore, build, and test
the solution. The same local restore/build/test sequence passed, including eight tests. No GitHub
Actions run or screenshot has been verified yet; the commits are not pushed.

## Remaining manual submission tasks

- [ ] In SSMS, run the safe script only on a fresh disposable database, verify `DB_NAME()` and the
  six tables, then save a clear screenshot as `docs/evidence/part1-sql-run.png`.
- [ ] On GitHub, open the successful Part 1 Actions run for the pushed commit and save the actual
  green run page as `docs/evidence/part1-ci-success.png`.
- [ ] After Part 2 CI passes, save a screenshot showing its successful build and test jobs as
  `docs/evidence/part2-ci-success.png`.
- [ ] Record the presentation in your own voice, demonstrate the completed API and tests, save a
  review copy as `docs/evidence/part2-presentation.mp4`, upload it as Unlisted on YouTube, verify
  playback/audio, and replace the video link below with its real URL.
- [ ] Review, understand, and adapt the work before submission.
- [ ] Submit the repository URL on ARC and confirm it points to the latest pushed commit.

Video link: **to be added after recording**. See
[`docs/Part2_Rubric_Checklist.md`](docs/Part2_Rubric_Checklist.md) for exact evidence checks.

## AI assistance disclosure

AI assistance was used to review the assignment and help draft documentation and code. The
student should review, understand, and adapt the work before submission in line with the
assignment's academic integrity instructions.
