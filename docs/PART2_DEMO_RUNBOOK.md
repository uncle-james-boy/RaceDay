# RaceDay Part 2 Demo Runbook

This runbook is based on the checked-in controllers, request/response DTOs, session setup, EF model, tests, README, and Part 2 presentation guide. Use a separate disposable database for every request that creates or updates data. Do not use a database containing assignment or personal data.

## What I verified in the repository

- `Program.cs` uses SQL Server, serves Swagger only in Development, enables server-side ASP.NET Core sessions, and sets the `.RaceDay.Session` cookie to HttpOnly and SameSite Lax with a 30-minute idle timeout. Session values are held in the configured in-memory distributed cache. This is session authentication, not JWT.
- `RaceDayDbContext` maps six entities. An enrolment's `(CategoryID, EventID)` foreign key points to the category alternate key with the same two columns.
- There is no safe, ready-to-use login account. The SQL sample password hashes are documented placeholders. Users created in tests are confined to the in-memory test database.
- The Part 2 guide calls for showing all 23 Swagger operations. The checked-in rubric checklist and guide are available, but the original official assignment brief/rubric PDF is not in this repository.
- The README's latest recorded test result is 41 passed, 0 failed, 0 skipped. It records this from 14 October 2026; this runbook preparation did not rerun tests or connect to SQL Server.
- `docs/evidence/` is untracked in the current working tree. Leave it and its contents alone.

## 1. Prepare an isolated database

The normal app setting points to LocalDB database `RaceDayDB`. For this demo, temporarily override it in the PowerShell window to use a separate database named `RaceDayDemo`. This changes only that shell's environment; it does not edit appsettings files.

First confirm that LocalDB is available and that `RaceDayDemo` does not already exist. In SSMS, connect to `(localdb)\MSSQLLocalDB` with Windows Authentication and run this read-only query in `master`:

```sql
SELECT name FROM sys.databases WHERE name = N'RaceDayDemo';
```

If it returns a row, stop. Do not migrate that database unless you have personally confirmed it is a disposable demo database. If the connection fails, stop and install/start no services as part of this runbook; use a SQL Server instance you already have available and understand.

From PowerShell, at the repository root, set a one-shell connection override and apply the existing migration to the new database:

```powershell
$env:ConnectionStrings__RaceDayDatabase = 'Server=(localdb)\MSSQLLocalDB;Database=RaceDayDemo;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True'
dotnet ef database update --project src/RaceDay.Api/RaceDay.Api.csproj --startup-project src/RaceDay.Api/RaceDay.Api.csproj
```

If you use an already installed SQL Express instance instead of LocalDB, first connect to `.\SQLEXPRESS` in SSMS with Windows Authentication and make the same read-only absence check. Then use this environment override in place of the LocalDB line:

```powershell
$env:ConnectionStrings__RaceDayDatabase = 'Server=.\SQLEXPRESS;Database=RaceDayDemo;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True'
```

This migration creates the schema but no sample users or records. It does not run the Part 1 SQL script. Do not run both setup methods against the same database. If `dotnet ef` is unavailable, check `dotnet ef --version`; the README specifies `dotnet tool install --global dotnet-ef --version 8.0.31` as the one-time user-level installation. If LocalDB is unavailable, stop and select a known disposable SQL Server instance; do not point the app at the normal `RaceDayDB` until you have verified what is in it.

Keep this PowerShell window open so the environment override remains active. Start the API:

```powershell
dotnet run --project src/RaceDay.Api/RaceDay.Api.csproj --launch-profile https
```

The HTTPS launch profile uses Development, launches Swagger, and normally listens on `https://localhost:7107` and `http://localhost:5298`. **Use the HTTPS address actually printed by `dotnet run`**, then add `/swagger` if the printed URL does not already include that path. Do not guess a different port. Keep the API process running while recording. If the development HTTPS certificate is not trusted, use the normal .NET developer-certificate setup before recording; do not bypass a browser certificate warning.

## 2. Run the automated tests

In a second PowerShell window at the repository root, run:

```powershell
dotnet restore RaceDay.sln
dotnet build RaceDay.sln --no-restore --configuration Release -m:1
dotnet test RaceDay.sln --no-build --no-restore --configuration Release -m:1
```

The README's last recorded successful result is **41 passed, 0 failed, 0 skipped**. This is an expected reference result, not a fresh result from this runbook. Tests use EF Core InMemory and an in-process HTTP test server; they do not require SQL Server and do not prove a live SQL Server connection. For the video, show the actual output from the commands you run. If restore/build/test fails, show the real failure and resolve it before claiming a pass.

## 3. Swagger controls and safe account setup

Swagger UI is the same HTTPS origin as the API. For a protected operation, click **Try it out**, fill the listed fields, then click **Execute**. After `POST /api/auth/login` succeeds, the browser stores the HttpOnly `.RaceDay.Session` cookie and sends it on same-origin Swagger requests. You should not manually paste a cookie or JWT into Swagger's **Authorize** dialog. Never reveal or read out the cookie value. The server stores the user ID and role in the session; logout clears that session.

Use three new, clearly fictional accounts on `RaceDayDemo`: Organiser A, Organiser B, and Participant. Use unique addresses such as `organiser-a-<date-time>@example.invalid`, `organiser-b-<date-time>@example.invalid`, and `participant-<date-time>@example.invalid`. Replace `<date-time>` with a unique suffix and use a throwaway password that you do not use elsewhere. Registration does not send email. Do not put a real personal email or a reusable password in the recording. The password field may be visible while typing; pause recording or crop/cover that field during entry if needed.

The migration starts with no accounts. Register through `POST /api/auth/register`; do not rely on SQL sample users or test-only accounts. Registration inserts rows, so do it only after confirming the app is connected to the isolated `RaceDayDemo` database. Use the `UserID`, `EventID`, `CategoryID`, `RouteID`, and `EnrolmentID` returned by the live responses. Do not assume IDs from another database.

## 4. The 23 Swagger operations

There are no query-string parameters on these operations. The “fields” column lists the path fields or JSON body properties to use. For every executable operation, the Swagger button sequence is **Try it out → fill fields/body → Execute**. For deletions, inspect and explain the operation but do not click Execute during the video.

| # | Method and route | Authentication | Fields / body | Expected result and safe demo action |
| --- | --- | --- | --- | --- |
| 1 | `GET /api/events` | Public | None | Execute; `200`, events ordered by date. Empty array is valid in a fresh migrated database. |
| 2 | `GET /api/events/{eventId}` | Public | Path `eventId` | Use the EventID returned by your create request; `200` event. Unknown ID returns `404`. |
| 3 | `GET /api/events/{eventId}/categories` | Public | Path `eventId` | Use your event ID; `200` category list. Missing event returns `404`. |
| 4 | `GET /api/events/{eventId}/routes` | Public | Path `eventId` | Use your event ID; `200` route list. Missing event returns `404`. |
| 5 | `POST /api/auth/register` | Public | JSON: `fullName`, `email`, `password`, `role` | Register the three demo-only accounts. `201`; response has public account details and no password hash. Invalid input/role returns `400`; duplicate email returns `409`. |
| 6 | `POST /api/auth/login` | Public | JSON: `email`, `password` | Log in as the account needed for the next operation. `200`; browser receives session cookie. Wrong credentials return `401`. |
| 7 | `GET /api/users/profile` | Any signed-in user | None | Execute after login; `200` current user's profile. Anonymous request returns `401`. |
| 8 | `PUT /api/users/profile` | Any signed-in user | JSON: `fullName`, optional `profilePictureUrl` | Optional: change only the disposable demo account's display name; `200`. Invalid profile picture URL returns `400`. Do not use a real person's details. |
| 9 | `POST /api/events` | Organiser session | JSON `EventWriteRequest` below | As Organiser A, create a demo event; `201`. Participant or anonymous request returns `403` or `401`. |
| 10 | `PUT /api/events/{eventId}` | Owning Organiser session | Path `eventId`; JSON `EventWriteRequest` | As Organiser A, update only your demo event; `200`. As Organiser B, the same request returns `403` without updating it. |
| 11 | `DELETE /api/events/{eventId}` | Owning Organiser session | Path `eventId`; no body | **Do not Execute.** Owned event with categories/routes/enrolments returns `409`; an eligible empty owned event can be deleted with `204`. |
| 12 | `POST /api/events/{eventId}/categories` | Owning Organiser session | Path `eventId`; JSON `CategoryWriteRequest` below | As Organiser A create categories under your event; `201`. |
| 13 | `PUT /api/categories/{categoryId}` | Owning Organiser session | Path `categoryId`; JSON `CategoryWriteRequest` | Update only a category you created in the disposable DB; `200`. Other event owner gets `403`; unknown ID gets `404`. |
| 14 | `DELETE /api/categories/{categoryId}` | Owning Organiser session | Path `categoryId`; no body | **Do not Execute.** If an enrolment references it, `409`; otherwise an owned category can be deleted with `204`. |
| 15 | `POST /api/events/{eventId}/routes` | Owning Organiser session | Path `eventId`; JSON `EventRouteWriteRequest` below | As Organiser A create a route for your event; `201`. Duplicate route name for that event returns `409`. |
| 16 | `PUT /api/routes/{routeId}` | Owning Organiser session | Path `routeId`; JSON `EventRouteWriteRequest` | Update only your demo route; `200`. Duplicate route name returns `409`; non-owner returns `403`. |
| 17 | `DELETE /api/routes/{routeId}` | Owning Organiser session | Path `routeId`; no body | **Do not Execute.** An owned route is deleted with `204`; non-owner returns `403`. |
| 18 | `POST /api/enrolments` | Participant session | JSON `CreateEnrolmentRequest` below | As Participant, matching event/category returns `201`. Category from another event returns `400`; duplicate enrolment returns `409`. |
| 19 | `GET /api/enrolments` | Participant session | None | Execute as Participant; `200`, only that participant's enrolments. |
| 20 | `GET /api/events/{eventId}/enrolments` | Owning Organiser session | Path `eventId` | Execute as Organiser A; `200` enrolments for that owned event. Another organiser gets `403`. |
| 21 | `POST /api/results` | Owning Organiser session | JSON `CreateRaceResultRequest` below | For an enrolment in your past-dated event, create one result; `201`. Future event returns `400`, non-owner `403`, duplicate result `409`. |
| 22 | `GET /api/results` | Participant session | None | Execute as the enrolled Participant; `200`, only their results. |
| 23 | `POST /api/auth/logout` | Any signed-in user | No body | Execute; `204`. Then try profile or another protected GET; it must return `401`. |

### Request body examples

Use these JSON property names in Swagger. Replace placeholders with IDs returned by your own successful demo responses. Keep dates in ISO 8601 format.

Registration (repeat with distinct fictional emails and the three listed roles):

```json
{
  "fullName": "Demo Organiser A",
  "email": "organiser-a-<unique-suffix>@example.invalid",
  "password": "<enter a throwaway demo-only password privately>",
  "role": "Organiser"
}
```

For the participant and second organiser, use the same shape with their own names, emails, and `role` values (`Participant` or `Organiser`). For login, use:

```json
{
  "email": "<registered demo email>",
  "password": "<the same throwaway password>"
}
```

`EventWriteRequest` (use for create and update; choose a date safely in the past so a result can be recorded later):

```json
{
  "name": "RaceDay demo event",
  "description": "Disposable local presentation data",
  "eventDate": "2025-10-20T08:00:00",
  "location": "Demo location",
  "distance": 5.0,
  "eventType": "Run"
}
```

The organiser ID is taken from the session; do not enter `organiserID`. The server generates the event ID.

`CategoryWriteRequest`:

```json
{
  "categoryName": "Demo 5K",
  "minAge": 18,
  "maxAge": 65,
  "distance": 5.0
}
```

`EventRouteWriteRequest`:

```json
{
  "routeName": "Demo loop",
  "distance": 5.0,
  "routeDescription": "Disposable local presentation data",
  "routeMapUrl": null
}
```

`CreateEnrolmentRequest`:

```json
{
  "eventID": <your event ID>,
  "categoryID": <a category belonging to that event>
}
```

`CreateRaceResultRequest` (use the EnrolmentID returned by the successful enrolment):

```json
{
  "enrolmentID": <your enrolment ID>,
  "finishTime": "00:42:30",
  "position": 1
}
```

The profile update body is:

```json
{
  "fullName": "Demo Participant Updated",
  "profilePictureUrl": null
}
```

## 5. Recommended live demonstration order

All write operations below must target the fresh `RaceDayDemo` database only. They add or change demo rows; no step requires deleting data.

1. **Introduce the API and schema.** Show the six entity classes and `RaceDayDbContext`. In `ConfigureCategories` and `ConfigureEnrolments`, point to Category's alternate key `(CategoryID, EventID)` and the matching enrolment FK `(CategoryID, EventID)`. Explain that it prevents an enrolment combining one event with a category belonging to another.
2. **Show public browsing.** Run `GET /api/events`. Initially it may be `[]`; after event creation run it again. Use `GET /api/events/{eventId}`, `GET /api/events/{eventId}/categories`, and `GET /api/events/{eventId}/routes` with IDs from your own responses.
3. **Register the demo users.** Run `POST /api/auth/register` three times for Organiser A, Organiser B, and Participant. Use separate fictional emails and a disposable password. Show that the response omits `passwordHash`.
4. **Log in as Organiser A.** Run `POST /api/auth/login`; then `GET /api/users/profile`. If demonstrating profile update, use `PUT /api/users/profile` with a fictional display name and `profilePictureUrl: null`, then GET the profile again. Explain that the browser automatically retains the HttpOnly session cookie for same-origin Swagger calls. Do not reveal the cookie value.
5. **Create and update demo objects.** As Organiser A, use `POST /api/events` with the past date above. Save its returned EventID, then create one category and one route under it. Update your event with `PUT /api/events/{eventId}`, update its category with `PUT /api/categories/{categoryId}`, and update its route with `PUT /api/routes/{routeId}`. Create a second event with another past date and one category under that event. Save the returned IDs. All rows stay in `RaceDayDemo`.
6. **Show role and ownership denial safely.** Log in as the Participant and try `POST /api/events` with a valid event body. Expected: `403`; authorization rejects it before event creation. Then log in as Organiser B and try `PUT /api/events/{eventId}` for Organiser A's event. Expected: `403`; the owner check rejects it before saving. Use only your demo event ID and show its unchanged name afterward with the public GET.
7. **Show category/event consistency.** Log in as the Participant. Try `POST /api/enrolments` using the first event ID but the category ID created under the second event. Expected: `400` with the mismatch explanation and no enrolment saved. Then enrol the participant in the matching event/category; expected `201`. Show `GET /api/enrolments` as that participant.
8. **Show organiser enrolment access.** Log in as Organiser A and run `GET /api/events/{eventId}/enrolments`; expected `200` with the demo enrolment. The code restricts this list to the event's owner.
9. **Show results.** Still as Organiser A, run `POST /api/results` with the returned EnrolmentID, `finishTime` `00:42:30`, and a positive `position`. The event date must be in the past. Expected `201`. Log in as the Participant and run `GET /api/results`; expected `200` with that participant's result.
10. **Show logout.** Run `POST /api/auth/logout`; expected `204`. Immediately run `GET /api/users/profile`; expected `401`. Explain the server session has been cleared.
11. **Explain deletion routes without executing them.** Point to the three DELETE operations and their guard conditions. Do not execute DELETE against the live demo database. The integration tests cover conflict cases for events and categories.
12. **Show tests and real CI.** Run the test command in the recording and show its actual result. If showing GitHub Actions, open the actual successful run for the commit being submitted; do not present a previous run as current if the commit changed.

### Safe composite-key rejection setup

You need two events and one category under each. Create both events and categories using Organiser A in `RaceDayDemo`. As Participant, submit the first event ID with the second event's category ID. The controller looks up the category's owning EventID and returns `400` before adding an enrolment. Then submit the matching pair and continue. This is an additive, rejected request; it does not delete or alter existing rows.

### What the tests verify

- `ApiSessionFlowTests.RealHttpFlowRegistersLogsInEnforcesRolesAndClearsSessionOnLogout` uses an in-memory database and separate cookie containers. It covers registration, login, profile, role denial, event and category/route workflows, the category/event mismatch, enrolment and result access, ownership denial, logout, and post-logout `401`.
- `EventTests.UpdateEventRejectsAnOrganiserWhoDoesNotOwnIt` and related event tests cover organiser ownership rules.
- `EnrolmentTests.ParticipantCannotEnrolInCategoryFromAnotherEvent` and `EfCoreModelTests.EnrolmentCategoryForeignKeyUsesBothCategoryIdAndEventId` cover the composite relationship.
- Tests create their own in-memory users. They do not supply live database credentials or a ready-to-use demo login.

## 6. Recording checklist and speaking outline

Before recording:

- [ ] Confirm the API's `RaceDayDatabase` environment override points to `RaceDayDemo`, not the normal `RaceDayDB`.
- [ ] Confirm the database is the one created from the existing migration and contains only disposable demo data.
- [ ] Run restore, Release build, and tests. Keep the real output visible; do not claim success if a command fails.
- [ ] Start the API in Development and use the HTTPS Swagger URL printed by that process.
- [ ] Prepare three fictional account names/emails and a throwaway password. Keep the password and session-cookie value off camera.
- [ ] Keep one terminal visible for the API and a second for tests. Prepare the browser at Swagger and, if useful, a code editor showing `Program.cs`, `AuthController.cs`, and `RaceDayDbContext.cs`.
- [ ] Do not open personal browser tabs, real user profiles, database passwords, connection strings, or the normal assignment database on camera.
- [ ] Do not Execute any DELETE operation. Make all write requests against `RaceDayDemo` only.

Suggested narration:

1. **Architecture:** “RaceDay is an ASP.NET Core Web API using EF Core and SQL Server. It has six entities. Enrolment stores EventID and CategoryID together so the category must belong to the selected event.”
2. **Authentication:** “Registration hashes the password. Login stores the user ID and role in a server-side session. The browser holds an HttpOnly session cookie; this project does not use JWT.”
3. **Permissions:** “Events can be browsed publicly. Organiser writes require the Organiser role and, for existing records, ownership. Participants can create and view their own enrolments and view their own results.”
4. **Relationships and workflows:** “The participant's enrolment must use a category from the same event. The event organiser can list that event's enrolments and record results only after the event date.”
5. **Verification:** “The automated tests use an in-memory database, so they check API behavior without proving the SQL Server instance is available. This run shows the actual test output; this separate screen is the actual GitHub Actions result.”

## Verified versus still needing setup

**Verified from source/tests/docs:** all listed route mappings and role attributes; session cookie name/options and server-side session keys; no documented usable demo account; composite EF relationship; validation and expected response paths; tests use in-memory storage; no need to run destructive requests.

**Needs your machine/setup to confirm:** that LocalDB is installed and available; that the separate `RaceDayDemo` database is absent before migration; that EF tooling and NuGet restore work in your normal PowerShell session; that the API can connect to that database; actual Swagger responses and IDs; and the live test result while recording. No SQL Server connection or Swagger request was performed while preparing this runbook.
