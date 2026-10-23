# Part 2 Rubric Checklist and Evidence Map

This checklist follows the official PROG6212/W assignment brief and Part 2 rubric. Status means
the current repository was checked; it does not predict a mark. Local verification is distinct
from evidence that only exists after a GitHub run, live SQL setup, or student recording.

## Assessed criteria

| Criterion and weight | Highest-band target | Implementation and verification | Status and remaining evidence |
| --- | --- | --- | --- |
| Correct submission (5) | Complete, understandable submission with required files, setup instructions, CI screenshot, and video link | `README.md`, six-entity Part 1 artifacts, API project, tests, this checklist; verify a fresh checkout from the submitted branch | **Implementation and documentation are present.** A clean-machine setup was not tested. Video URLs have been supplied but playback/access have not been checked here. The screenshots currently show CI run #8 for commit `53b2901`; because these docs are being edited after that run, capture a new run for the final pushed commit. Submit the repository URL on ARC. |
| GitHub and CI/CD (10) | At least 20 meaningful commits and a working workflow that validates the project | `part2` branch preserves prior history; `.github/workflows/build.yml` runs Part 1 validation, DBML conversion, Markdown lint, restore, Release build, and tests | **A genuine remote run is verified:** [run #8](https://github.com/uncle-james-boy/RaceDay/actions/runs/39057052022) succeeded on `part2`, commit `53b2901`. Both supplied CI screenshots are present, but they document that commit, not these subsequent documentation edits. Push the reviewed final changes, confirm a new successful run for that commit, and replace/refresh the screenshots if required by the submission. |
| Authentication and role access (20) | Registration/login for both roles, hashed passwords, server-side session, role checks, and correct access restrictions | `AuthController`, `SessionAuthorizeAttribute`, session setup in `Program.cs`, profile and resource controllers, HTTP integration tests | **Implemented and locally tested.** Real Kestrel tests check login/session/logout, cookie flags, anonymous denial, role denial, and ownership. Production SQL Server sign-in has not been exercised. |
| API endpoints and database (25) | All planned endpoint operations, correct database relationships, and working API behavior | `docs/API_Endpoint_Plan.md`; seven controllers; six EF entities; SQL Server provider and migration; composite Category/Event key; API integration/model tests | **Implemented and locally tested against an in-memory test database.** The SQL Server model and migration were inspected, but no live SQL Server database was updated or queried. |
| Swagger (10) | All endpoints documented and testable, with clear inputs and responses | XML comments and `SessionCookieOperationFilter`; Swagger integration assertions check 23 operations and descriptions | **Locally verified.** Test suite checks the generated OpenAPI document and session-cookie security metadata. Demonstrate Swagger workflows in the video. |
| Unit testing (15) | Structured tests cover required successful and denied behavior | `tests/RaceDay.Api.Tests`; tests include HTTP session flow, roles/ownership, validation, conflicts, not-found cases, composite relationship and Swagger | **41 tests passed locally** in the recorded Release run on 14 October 2026. The successful Actions run #8 also ran the test step for commit `53b2901`. A fresh local rerun in this environment did not complete because VSTest could not connect to its test host; see the verification note below. |
| Video presentation (15) | Clear spoken explanation and thorough demonstration of API, endpoints, authentication, Swagger, and tests | `docs/Part2_Video_Presentation_Guide.md` and `docs/Video_Presentation_Guide.md` | **Links supplied:** [Part 1](https://youtu.be/DtnpXK5yhq0?si=AsyMH0gIYYsvAGLj) and [Part 2](https://youtu.be/jX8b3rDCzcM?si=upj6Y6qbrTz7oh3E). Their playback, audio, privacy/access settings, and match to the rubric still need the student's manual review. |

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
- [x] A green GitHub Actions run exists for commit `53b2901`; the supplied screenshots depict that run.
- [ ] After the final documentation/evidence changes are pushed, confirm a green run for that exact commit and capture it. Earlier CI screenshots do not verify later changes.
- [x] Part 1 and Part 2 video URLs have been supplied and added to README; [ ] student must verify playback, sound, and unlisted access in a private browser.
- [ ] Submit the existing repository link on ARC and verify the submitted URL.

## Evidence tasks for the student

The evidence folder currently contains genuine screenshots supplied by the student. The CI images
show run #8 on commit `53b2901`. The SSMS image shows a successful read-only query listing six
tables; it does not show execution of `RaceDay_Database.sql`. Video URLs were supplied by the
student, but their playback/access could not be checked here. Do not treat local tests, screenshots
from an older commit, or a table-listing query as proof of other actions.

### Part 1 SQL demonstration — explicitly required in the Part 1 video

1. Use **SQL Server Management Studio (SSMS)** and a fresh disposable SQL Server database. Review
   `docs/RaceDay_Database.sql` first. Do not run it against a database containing data; the script
   intentionally stops if RaceDay tables already exist and must not be modified to drop them.
2. In the Part 1 recording, show the script and execute it live. Then run
   `SELECT DB_NAME() AS CurrentDatabase;` and a `sys.tables` query showing the six RaceDay tables.
3. The supplied `docs/evidence/part1-sql-run.png.png` is a genuine SSMS capture showing `RaceDayDB`,
   a successful query, and the six table names. It supports that the tables were present at capture
   time; it does not establish that the setup script was run in the capture. The live SQL script
   demonstration remains a Part 1 video requirement. The unusual filename is preserved as supplied.

### Part 1 CI evidence — screenshot explicitly listed for submission

1. After the final Part 1 files are available on GitHub, open the existing **RaceDay** repository,
   select **Actions**, and open the successful `RaceDay CI Build and Validate` run.
2. The supplied `docs/evidence/part1-ci-success.png.png` shows the real green run summary, branch
   `part2`, and commit `53b2901`. Re-capture the final run after the latest changes are pushed;
   check that the image shows the exact commit intended for submission.

### Part 2 CI evidence — screenshot explicitly listed for submission

1. Push the completed `part2` branch to the existing repository when you choose to do so. On
   GitHub, open **Actions** and select the successful run for the final Part 2 commit.
2. The supplied `docs/evidence/docsevidencepart2-ci-success.png..png` shows the successful
   `part1-validation` job's steps for the same run #8. The successful run summary confirms the job
   completed; this detailed view is supplementary. Re-capture after the latest changes are pushed.
3. Verify the commit shown is the latest commit you intend to submit and that the job passed.

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
relationship in code, and the passing test run. Explain the project in your own words. The student
has supplied a Part 2 URL and it is in the README; open it in a private browser, verify sound and
readability, and confirm the recorded content covers the rubric. Keep a local recording if needed
for course submission, using the destination/name specified by the course. Do not claim playback
verification until completed.

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
It does not prove the API has connected to SQL Server. The repository's prior local verification
record does not show a SQL script or EF migration being executed by that validation. A genuine
GitHub Actions run and screenshots now exist for commit `53b2901`, but a new run is needed after the
current documentation changes are committed and pushed. Video links have been supplied; playback
and access remain for the student to verify.

This final documentation audit attempted to rerun the tests, but VSTest did not connect to the
testhost within 90 seconds (and a retry with a longer connection timeout also did not complete).
The Part 1 validator was also attempted but could not run its ERD image check because Pillow is not
installed in this environment. These are environment verification failures, not evidence that the
implementation or Part 1 schema failed. `git diff --check` and the README's referenced local paths
passed.
