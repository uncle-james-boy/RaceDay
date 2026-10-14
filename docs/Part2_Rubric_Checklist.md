# Part 2 Rubric Checklist and Evidence Map

This checklist follows the official PROG6212/W assignment brief and Part 2 rubric. Status means
the current repository was checked; it does not predict a mark. Local verification is distinct
from evidence that only exists after a GitHub run, live SQL setup, or student recording.

## Assessed criteria

| Criterion and weight | Highest-band target | Implementation and verification | Status and remaining evidence |
| --- | --- | --- | --- |
| Correct submission (5) | Complete, understandable submission with required files, setup instructions, CI screenshot, and video link | `README.md`, six-entity Part 1 artifacts, API project, tests, this checklist; verify a fresh checkout from the submitted branch | **Mostly complete.** Repository files and local setup instructions exist. A clean-machine setup was not tested. Add real CI screenshots and both video links; submit the repository URL on ARC. |
| GitHub and CI/CD (10) | At least 20 meaningful commits and a working workflow that validates the project | `part2` branch preserves prior history; `.github/workflows/build.yml` runs Part 1 validation, DBML conversion, Markdown lint, restore, Release build, and tests | **20 genuine Part 2 commits are local**, counted from the Part 1 repair commit `62f3ba4` through this rubric closeout. No remote run or screenshot is claimed. When ready, push the branch, confirm Actions is green for the submitted commit, and capture that actual run. |
| Authentication and role access (20) | Registration/login for both roles, hashed passwords, server-side session, role checks, and correct access restrictions | `AuthController`, `SessionAuthorizeAttribute`, session setup in `Program.cs`, profile and resource controllers, HTTP integration tests | **Implemented and locally tested.** Real Kestrel tests check login/session/logout, cookie flags, anonymous denial, role denial, and ownership. Production SQL Server sign-in has not been exercised. |
| API endpoints and database (25) | All planned endpoint operations, correct database relationships, and working API behavior | `docs/API_Endpoint_Plan.md`; seven controllers; six EF entities; SQL Server provider and migration; composite Category/Event key; API integration/model tests | **Implemented and locally tested against an in-memory test database.** The SQL Server model and migration were inspected, but no live SQL Server database was updated or queried. |
| Swagger (10) | All endpoints documented and testable, with clear inputs and responses | XML comments and `SessionCookieOperationFilter`; Swagger integration assertions check 23 operations and descriptions | **Locally verified.** Test suite checks the generated OpenAPI document and session-cookie security metadata. Demonstrate Swagger workflows in the video. |
| Unit testing (15) | Structured tests cover required successful and denied behavior | `tests/RaceDay.Api.Tests`; tests include HTTP session flow, roles/ownership, validation, conflicts, not-found cases, composite relationship and Swagger | **41 tests passed locally** in the final Release run. The remote Actions run remains pending. |
| Video presentation (15) | Clear spoken explanation and thorough demonstration of API, endpoints, authentication, Swagger, and tests | `docs/Part2_Video_Presentation_Guide.md` and `docs/Video_Presentation_Guide.md` | **Student task pending.** Record in your own voice, demonstrate the required workflows, upload as Unlisted, verify playback, and add real URLs to the README. |

## Assignment alignment checklist

- [x] ASP.NET Core Web API with EF Core and SQL Server provider is present.
- [x] Six Part 1 entities are represented: Users, Events, Categories, EventRoutes, Enrolments, and Results.
- [x] Enrolment references Category through the composite `(CategoryID, EventID)` key; model tests verify it.
- [x] Passwords are hashed; register/login/logout use server-side session state, with no JWT.
- [x] Organiser ownership and Participant role restrictions are enforced by the API.
- [x] The 23 operations in the API plan are implemented and represented in Swagger.
- [x] Request validation, duplicate/conflict handling, and missing-record responses have tests.
- [x] GitHub Actions is configured to validate Part 1, convert DBML, lint Markdown, restore, build, and test.
- [x] Complete at least 20 genuine Part 2 commits; 18 existed before these two documentation closeout commits, bringing the local total to 20.
- [x] Run a final Release build/test after the implementation and documentation changes; results are recorded below.
- [ ] Confirm a green GitHub Actions run for the final pushed commit; local checks are not a substitute.
- [ ] Add real Part 1 and Part 2 video links and CI screenshots; placeholders are not evidence.
- [ ] Submit the existing repository link on ARC and verify the submitted URL.

## Evidence tasks for the student

No screenshots, recordings, SQL execution, or remote CI results have been fabricated. Capture the
items below only from the genuine software or service named.

### Part 1 SQL demonstration — explicitly required in the Part 1 video

1. Use **SQL Server Management Studio (SSMS)** and a fresh disposable SQL Server database. Review
   `docs/RaceDay_Database.sql` first. Do not run it against a database containing data; the script
   intentionally stops if RaceDay tables already exist and must not be modified to drop them.
2. In the Part 1 recording, show the script and execute it live. Then run
   `SELECT DB_NAME() AS CurrentDatabase;` and a `sys.tables` query showing the six RaceDay tables.
3. The brief explicitly requires the live SQL demonstration in the Part 1 video. A separate
   screenshot is useful supporting evidence but is not separately specified by the brief. If you
   capture one, save the genuine SSMS screen as `docs/evidence/part1-sql-run.png`; make sure the
   database name and six table names are readable and no private information is visible.

### Part 1 CI evidence — screenshot explicitly listed for submission

1. After the final Part 1 files are available on GitHub, open the existing **RaceDay** repository,
   select **Actions**, and open the successful `RaceDay CI Build and Validate` run.
2. Capture the real browser page with the workflow name, green success state, branch, commit, and
   completed validation jobs visible. Save it as `docs/evidence/part1-ci-success.png`.
3. Reopen the PNG and confirm it is readable and corresponds to the commit being submitted. Do not
   use a mock image or a local test result as a CI screenshot.

### Part 2 CI evidence — screenshot explicitly listed for submission

1. Push the completed `part2` branch to the existing repository when you choose to do so. On
   GitHub, open **Actions** and select the successful run for the final Part 2 commit.
2. Capture the genuine page showing the workflow, `part2` branch, commit, and successful build and
   test jobs. Save it as `docs/evidence/part2-ci-success.png`.
3. Verify the commit shown is the latest commit you intend to submit and that both jobs passed.
   GitHub Actions cannot be evidenced before a remote run exists.

### Part 1 video — student recording required

Use OBS Studio or another screen recorder with your microphone. In your own voice, explain the six
entities, ERD cardinalities/keys/constraints, endpoint plan, and SQL setup. Run the SQL script live
in SSMS on a fresh disposable database and show the resulting tables. AI-generated voice is not
permitted by the brief. Save a review copy as `docs/evidence/part1-presentation.mp4`, upload it to
YouTube as **Unlisted**, replay it with sound, and put the real URL in the README. Check the link in
a private browser window.

### Part 2 video — student recording required

Follow `docs/Part2_Video_Presentation_Guide.md`. Use OBS Studio or another screen recorder and your
own voice. Demonstrate the running API in Swagger, all endpoint groups, registration/login/logout,
the session cookie, both roles, ownership denials, enrolment and result workflows, the composite EF
relationship in code, and the passing test run. Explain the project in your own words. Save a review
copy as `docs/evidence/part2-presentation.mp4`, upload it to YouTube as **Unlisted**, replay it to
check audio and readability, and add the real URL to README. Check access in a private browser
window. Do not record until the final implementation and CI run are ready.

### Final submission

Review the code so you can explain it, then push the chosen final branch to
`https://github.com/uncle-james-boy/RaceDay`. Submit that repository URL on ARC as required by the
course and verify the submitted URL points to the intended repository and final commit. The
current local work has not been pushed by this task.

## Local verification record

Verified locally on 14 October 2026:

- `dotnet restore RaceDay.sln --configfile <repo-local NuGet.Config> -m:1` — passed for API and
  test projects. A repository-local config was used because the sandbox denied access to the
  user's roaming NuGet configuration directory.
- `dotnet build RaceDay.sln --no-restore --configuration Release -m:1` — passed with 0 warnings
  and 0 errors.
- `dotnet test RaceDay.sln --no-build --no-restore --configuration Release -m:1` — passed, 41
  passed, 0 failed, 0 skipped.
- `tools/validate_part1.py` — passed: six entities, matching SQL tables, constraints, seed data,
  and endpoint plan.
- Markdown lint on the README and all listed documentation files — passed.
- `git diff --check` — passed.

The local test server exercises real HTTP requests and session cookies using an in-memory database.
It does not prove the API has connected to SQL Server. No SQL script or EF migration was executed
against a database. No GitHub Actions run, screenshot, or video exists yet. Keep those facts
separate from local results.
