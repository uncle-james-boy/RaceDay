# Part 2 Implementation Blueprint

This blueprint maps the six-entity Part 1 model and endpoint plan to the implemented ASP.NET Core
Web API using EF Core and SQL Server. Use the source files and tests named below as the current
implementation; this document explains how they correspond to the original design.

## EF Core entities and relationships

| Entity | Main relationships |
| --- | --- |
| `User` | Organiser of many Events; Participant in many Enrolments |
| `Event` | Belongs to one Organiser; has many Categories, EventRoutes, and Enrolments |
| `Category` | Belongs to one Event; selected by many Enrolments |
| `EventRoute` | Belongs to one Event; stores named course distance, description, and map URL |
| `Enrolment` | Belongs to one Participant, Event, and matching Category; has zero or one Result |
| `Result` | Belongs to one Enrolment; unique EnrolmentID enforces at most one result |

Configure the composite category/event relationship explicitly. The SQL requires the
Enrolment's `CategoryID` and `EventID` to refer to the same Category row:

```csharp
modelBuilder.Entity<Category>()
    .HasAlternateKey(category => new { category.CategoryID, category.EventID });

modelBuilder.Entity<Enrolment>()
    .HasOne(enrolment => enrolment.Category)
    .WithMany()
    .HasForeignKey(enrolment => new { enrolment.CategoryID, enrolment.EventID })
    .HasPrincipalKey(category => new { category.CategoryID, category.EventID });
```

`EventRoute` uses `EventID` as a normal foreign key and an Event navigation collection.
Route write endpoints must first verify that the route's parent Event belongs to the
logged-in Organiser.

## Session authentication

Register hashes the password. Login verifies the hash, then stores `UserID` and `Role` in
ASP.NET Core session state. The session cookie identifies the server-side session; no JWT
is created. A shared authorization filter/policy returns 401 without a session, 403 for
the wrong role, and 403 when an Organiser does not own the requested event.

## Controllers

- `AuthController`: register, login, logout.
- `UsersController`: current user's profile.
- `EventsController`: public browse plus organiser-owned event CRUD.
- `CategoriesController`: public listing plus owner-only category CRUD.
- `EventRoutesController`: public event route listing plus owner-only route CRUD.
- `EnrolmentsController`: Participant enrolment/list and owner Organiser event listing.
- `ResultsController`: owner Organiser capture and Participant's own result listing.

Match the routes and response codes in `API_Endpoint_Plan.md`. Use request/response DTOs
so password hashes and server-assigned ownership IDs are never returned or accepted from
the caller.

## Swagger, tests, and CI

Swagger documents all 23 planned operations, including request and response descriptions and the
session-cookie scheme for protected operations. The xUnit suite covers the HTTP session flow,
roles and ownership, CRUD workflows, matching category/event IDs, conflicts, and not-found
responses. GitHub Actions restores, builds, tests, and validates the Part 1 documents and DBML.
Local checks do not count as proof of a successful remote Actions run; use the actual run as
evidence after the branch has been pushed.
