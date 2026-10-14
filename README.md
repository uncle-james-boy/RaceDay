# RaceDay Event Management Platform

![CI](https://github.com/uncle-james-boy/RaceDay/actions/workflows/build.yml/badge.svg)

## Project overview

RaceDay helps Organisers manage road events and Participants browse events, enrol in a category,
view route details, and track results. This repository holds Part 1's plan and schema. Part 2
will build the ASP.NET Core Web API on this design.

Organisers manage their own events, categories, and routes; view enrolments; and record results.
Participants browse events, enrol, and view only their own enrolments, results, and profile.
Authentication uses server-side sessions, as required by the assignment.

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

## Validation and CI

GitHub Actions validates the repository files, DBML, schema consistency, safe SQL setup, seed
data, endpoint plan, and generated ERD. When Part 2 is added, the workflow will also build the API
and run tests. The README must contain a screenshot captured from a real successful workflow run;
no screenshot is claimed yet.

## Remaining manual submission tasks

- [ ] Capture and add a screenshot of a real green GitHub Actions run.
- [ ] Record the required presentation in your own voice, publish it as an unlisted YouTube
  video, and replace the link below with the real URL.
- [ ] Review, understand, and adapt the work before submission.

Video link: **to be added after recording**.

## AI assistance disclosure

AI assistance was used to review the assignment and help draft documentation and code. The
student should review, understand, and adapt the work before submission in line with the
assignment's academic integrity instructions.
