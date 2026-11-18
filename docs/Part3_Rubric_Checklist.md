# Part 3 rubric and evidence checklist

This checklist follows the nine Part 3 criteria supplied for the PROG6212 assessment. “Implemented” describes source code; it does not by itself mean the feature has been manually demonstrated or accepted by the marker.

| Criterion | Current implementation status | Evidence still needed |
| --- | --- | --- |
| Correct Submission (5) | Part 3 source, Docker guide, API plan and README are present. The final Part 3 submission is not complete. | Commit and push the reviewed Part 3 work; confirm the submission contents and URL on ARC. |
| GitHub and CI/CD (10) | The workflow is configured for `main`, `part2` and `part3`; it builds/tests .NET and validates/builds the Compose application. Local workflow edits are not yet a GitHub run. The recorded Part 3 history contains 9 commits relative to `origin/main`. | Obtain a real green Actions run for the final Part 3 commit, save its genuine screenshot, and continue with meaningful development commits only. |
| Authentication and Role-Based Navigation (10) | API registration/login/logout uses server-side session state; MVC forwards each user's API session cookie; role-specific navigation and server-side role filters are implemented. | Obtain a successful complete test run and retain a clear demonstration of both roles, logout and protected-page redirection. |
| Organiser Interface (15) | Source includes organiser dashboard, event CRUD, categories, enrolment roster, result capture and banner upload. API checks organiser role and event ownership. Results become visible immediately; there is no separate draft/publish state. | Complete browser verification of all CRUD, validation, ownership rejection, roster and result flows. Confirm whether immediate publication meets the marker's expected meaning of “publish.” |
| Participant Interface (15) | Source includes upcoming-event browsing/filtering, details, categories/routes, enrolment, own events/statuses, own results, profile update and profile-picture upload/retrieval. | Complete browser verification of enrolment/category selection, own-data isolation, profile update and private picture upload/display. |
| Azure Blob Storage (10) | API-owned Blob service supports public event banners and private profile pictures; MVC sends uploads to API and proxies authenticated profile-picture reads. Image formats/content and size limits are checked in code. | Verify the full profile-picture flow against Azurite and retain evidence for both image flows. Real Azure resources are not part of local verification. |
| Docker (10) | API/Web Dockerfiles, Compose services, persistent volumes, Azurite setup, environment configuration and Docker guide are present. Compose configuration validates locally. | Build/run the exact reviewed Compose version and retain genuine evidence that the containers work. Codex currently cannot access the Docker Desktop engine. |
| UI Design (10) | Bootstrap-based RaceDay styling, responsive layout, role-specific navigation, status badges and validation/error/empty states are present in views. | Review the complete application in a browser at desktop and narrow widths; capture genuine screenshots if required by the submission. |
| Video Presentation (15) | Part 1 and Part 2 video links are in the README. A Part 3 video link is not recorded. | Record/upload the Part 3 demonstration and replace no placeholders: add the real URL and verify it opens. |

## Part 3 video outline

Demonstrate these rubric areas in one coherent recording:

1. Start the complete Docker deployment and show the running API, MVC, SQL Server and Azurite services.
2. Briefly show the MVC/API project structure and explain that MVC communicates with the API rather than accessing SQL Server or Blob Storage directly.
3. Register or sign in as an Organiser, show role-specific navigation, dashboard, event/category management, enrolment roster and result capture.
4. Sign in as a Participant, show event filtering/details, category enrolment, My Events, results and profile editing.
5. Upload/display an event banner and a private profile picture; explain that the API owns Blob operations and profile pictures require authenticated retrieval.
6. Show the Docker Compose setup and the actual successful GitHub Actions run after it exists.

Do not show passwords, connection secrets, session-cookie values or personal data in the recording.
