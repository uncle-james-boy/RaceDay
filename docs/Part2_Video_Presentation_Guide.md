# Part 2 Video Presentation Guide

The assignment requires a Part 2 video showing the running API and explaining the code structure,
endpoints, authentication, and unit tests. Record in your own voice; AI-generated voices are not
permitted. Upload the finished video to YouTube as **Unlisted**.

## Prepare

1. Configure a fresh RaceDay database using one method in `README.md`: the Part 1 SQL script or the
   EF Core migration. Do not apply both methods to the same database.
2. From the repository root, run:

   ```powershell
   dotnet restore RaceDay.sln
   dotnet build RaceDay.sln --no-restore --configuration Release
   dotnet test RaceDay.sln --no-build --no-restore --configuration Release
   dotnet run --project src/RaceDay.Api/RaceDay.Api.csproj --launch-profile https
   ```

3. Open the Swagger URL printed by `dotnet run` (normally `https://localhost:7107/swagger`). Use
   demonstration accounts and data that are safe to show on screen.
4. Start a screen recording with your microphone enabled. OBS Studio or another screen recorder can
   capture both the application and your voice. Do not use an AI-generated voice.

## Demonstrate and explain

1. Explain the project folders, six EF Core entities, SQL Server provider, migration, and the
   composite `(CategoryID, EventID)` foreign key in `RaceDayDbContext`.
2. Expand all 23 Swagger operations. Explain their purpose, inputs, responses, and required roles.
   Point out the session-cookie security description.
3. Register an Organiser and a Participant. Log in and show the session cookie; show that the
   profile response does not include a password hash. Log out and show that a protected request
   returns 401.
4. As an Organiser, create an event, category, and EventRoute. List and update them. Show that the
   owner can view the event's enrolments and that dependent records prevent unsafe event/category
   deletion.
5. As a Participant, browse the event, view categories and routes, enrol in a category, and view
   your own enrolments. Show that a category from another event is rejected.
6. Show that a Participant cannot manage an event and that another Organiser cannot update an event
   they do not own. Demonstrate the 403 response.
7. For an event with a past date, record a result as its Organiser. Log in as the Participant and
   show their personal result. Explain that early results and duplicate results are rejected.
8. Show and run the unit tests. Explain the checks for session cookies, roles, ownership,
   validation, not-found responses, and the composite EF key.
9. Show the real successful GitHub Actions run if it has completed. Identify it as a remote run,
   separate from the local build and test results.

## Save and verify

- Save a local review copy as `docs/evidence/part2-presentation.mp4`.
- Upload to YouTube as **Unlisted** and replace the Part 2 placeholder in `README.md` with the real
  link.
- Replay the local file and open the YouTube link in a private browser window. Check that the voice
  is clear, the code and Swagger responses are readable, and the video opens without signing in.

## GitHub Actions screenshot

After the final branch is pushed, open the successful `RaceDay CI Build and Validate` run in the
repository's **Actions** tab. Save a genuine capture showing the workflow, branch, commit, and
successful build/test jobs as `docs/evidence/part2-ci-success.png`. Confirm the screenshot is for
the latest commit being submitted. Do not create or substitute a mock image.
