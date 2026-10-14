# Part 2 Rubric Checklist and Evidence Map

This checklist is transcribed from the Part 2 requirements and rubric in the supplied assignment brief. It is a work plan, not a prediction of marks.

| Assignment criterion | Highest rubric band asks for | Implementation and verification map | Evidence / status |
| --- | --- | --- | --- |
| Correct submission (5) | Clear structure; complete setup instructions; CI screenshot; video link | `src/RaceDay.Api`, `tests/RaceDay.Api.Tests`, this repository README; verify clean clone setup and run instructions | Local README/source can be checked here. A genuine CI screenshot and unlisted video URL are manual tasks; pending |
| GitHub and CI/CD (10) | 20+ meaningful commits; workflow validates structure and has green build | Genuine Part 2 implementation commits; `.github/workflows/build.yml` builds and tests on every push; inspect actual GitHub Actions run | Requirement: 20 meaningful commits for Part 2. Current history has 25 total commits before Part 2; Part 2 commit count is 0. Green remote run and screenshot pending |
| Authentication and role access (20) | Working registration/login for both roles; server-side session stores UserID and Role; every protected route checks role; password hashes | Auth service/controller, session middleware and authorization filters/policies; API integration tests for success, unauthenticated access, wrong role, and logout | Must verify with tests and live API; pending |
| API endpoints and database (25) | All planned endpoints; full CRUD where required; EF Core; schema matches Part 1 ERD/SQL | `docs/API_Endpoint_Plan.md`; six EF entities and `RaceDayDbContext`; SQL Server provider; explicit composite `(CategoryID, EventID)` FK to Categories; endpoint tests | Compare model metadata and migration/schema to DBML/SQL; build, tests, and optional safe local DB test; pending |
| Swagger (10) | Every endpoint visible/testable; clear purpose, inputs, and responses | OpenAPI descriptions, request/response schemas, session-cookie security description; configure Swagger UI in development | Start API and inspect every planned route in browser; capture evidence if helpful; pending |
| Unit testing (15) | Comprehensive, structured tests all passing locally and through CI | `tests/RaceDay.Api.Tests`; cover register/login, event management, wrong roles, enrolment data integrity, profile, categories/routes, results, validation/not-found/conflict paths | Run `dotnet test` locally and verify the actual Actions run; pending |
| Video presentation (15) | Thorough API demo: endpoints, authentication, Swagger, tests, clear code explanation | Update `docs/Video_Presentation_Guide.md` with actual implementation flow and test commands | Student records own voice; unlisted YouTube URL added to README. Manual task; pending |

## Requirement checklist

- [ ] ASP.NET Core Web API in C# connects to SQL Server through EF Core Code-First.
- [ ] Database model matches all six Part 1 entities, including EventRoutes.
- [ ] Enrolments enforce the composite CategoryID/EventID foreign key.
- [ ] Register and login support Organiser and Participant roles; passwords are hashed.
- [ ] Server-side session persists UserID and Role; logout invalidates it; no JWT is used.
- [ ] Public routes are public; every protected route requires authentication, correct role, and organiser ownership where specified.
- [ ] Implement every endpoint in the Part 1 API plan, including profile, event/category/route management, enrolments, and results.
- [ ] Swagger shows and describes all implemented endpoints, inputs, and responses.
- [ ] Tests cover success and failure for registration/login, event management, role rejection, participant enrolment and correct stored relationship; broaden coverage across resources.
- [ ] GitHub Actions builds the API and runs all tests on every push.
- [ ] Make at least 20 genuine, substantive Part 2 commits; do not use empty or cosmetic commits to inflate the count.
- [ ] README includes setup/run instructions, role descriptions, a real green CI screenshot, and the real unlisted video URL.
- [ ] Submit the GitHub repository link on ARC (student action).

## Manual evidence checklist

1. **Part 1 SQL demonstration:** In SQL Server Management Studio, use a fresh disposable database/server. Review the preflight and database name before executing `docs/RaceDay_Database.sql`; do not execute it against an existing RaceDay database with data. In Results, run `SELECT DB_NAME() AS CurrentDatabase;` and a query listing `sys.tables`; verify `RaceDayDB` and the six expected tables. Capture the genuine SSMS window with query and results visible as `docs/evidence/part1-sql-run.png`. Confirm the saved PNG opens, shows the database context and six table names clearly, and contains no unrelated private information. Also show the live script execution in the Part 1 video.
2. **Part 1 CI screenshot:** After Part 1 files are pushed, open the repository on GitHub, choose Actions, open the successful `RaceDay CI Build and Validate` run for the pushed commit, and capture the page showing the green success state, workflow name, branch, and commit. Save as `docs/evidence/part1-ci-success.png`. Verify it is an actual GitHub page capture and the commit matches the submitted branch. Add it to README only after it exists.
3. **Part 2 CI screenshot:** After API code and tests are pushed, use the same GitHub Actions steps and save the genuine successful run as `docs/evidence/part2-ci-success.png`. Confirm the successful run is for the latest submitted Part 2 commit and visibly includes build and test jobs.
4. **Part 2 video:** Use a screen recorder and your own spoken voice. Run the API and tests; demonstrate Swagger, registration/login/logout with session cookie, both roles, ownership restrictions, key endpoints, and the composite enrolment/category/event relationship in code. Save a local review copy as `docs/evidence/part2-presentation.mp4` if storage permits; upload to YouTube as **Unlisted**, check playback and audio, copy its real URL into README, and verify it opens in a private browser window without signing in.
5. **Final submission:** On ARC, submit the GitHub repository URL required by the brief. Verify the submitted URL points to the intended repository and the latest pushed commit is visible. This submission action is the student's responsibility.

`docs/evidence/` is an intended evidence folder; no screenshots or video have been fabricated or added by this checklist.
