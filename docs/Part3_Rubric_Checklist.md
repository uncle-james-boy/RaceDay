# Part 3 rubric and evidence checklist

This checklist follows the nine Part 3 criteria supplied for the PROG6212 assessment. “Implemented” describes source code; it does not by itself mean the feature has been manually demonstrated or accepted by the marker.

| Criterion | Current implementation status | Evidence still needed |
| --- | --- | --- |
| Correct Submission (5) | Part 3 source, Docker guide, API plan, README and rubric checklist are present. The application browser checks have been completed according to the student. The final assignment submission is not complete. | Save the genuine Part 3 CI screenshot, add the Part 3 video link after upload, and submit the repository on ARC. |
| GitHub and CI/CD (10) | The workflow runs on `main`, `part2` and `part3`, builds/tests .NET, validates Compose and builds API/Web images. [Run #15](https://github.com/uncle-james-boy/RaceDay/actions/runs/44269215155) succeeded on commit `2289cb8`. There are 17 meaningful Part 3 commits relative to `origin/main`, short of the 20-commit target. | Save a screenshot of Run #15 and complete three more genuine, meaningful development commits; do not split work artificially. |
| Authentication and Role-Based Navigation (10) | API registration/login/logout uses server-side session state; MVC forwards each user's API session cookie; role-specific navigation and server-side role filters are implemented. The student reports the browser checks are complete. | Keep the successful Run #15 test result as automated evidence and include the relevant demonstration in the Part 3 video. |
| Organiser Interface (15) | Source includes organiser dashboard, event CRUD, categories, enrolment roster, result capture and banner upload. API checks organiser role and event ownership. Results become visible immediately; there is no separate draft/publish state. The student reports the browser checks are complete. | Include the organiser workflow in the Part 3 video. |
| Participant Interface (15) | Source includes upcoming-event browsing/filtering, details, categories/routes, enrolment, own events/statuses, own results, profile update and profile-picture upload/retrieval. The student reports the browser checks are complete. | Include the participant workflow in the Part 3 video. |
| Azure Blob Storage (10) | API-owned Blob service supports public event banners and private profile pictures; MVC sends uploads to API and proxies authenticated profile-picture reads. Image formats/content and size limits are checked in code. The student reports the application browser checks are complete. | Include both image flows in the Part 3 video. The saved CI screenshot and Part 3 video link are still outstanding. Real Azure resources are not required for local Azurite evidence. |
| Docker (10) | API/Web Dockerfiles, Compose services, persistent volumes, Azurite setup, environment configuration and Docker guide are present. The Compose configuration and image builds are validated by Run #15. The student reports application/browser checks are complete. | Show Docker startup and operation in the Part 3 video. |
| UI Design (10) | Bootstrap-based RaceDay styling, responsive layout, role-specific navigation, status badges and validation/error/empty states are present in views. The student reports the browser checks are complete. | Include the running application in the Part 3 video; do not treat the missing video as completed evidence. |
| Video Presentation (15) | Part 1 and Part 2 video links are in the README. A Part 3 video link is not recorded. | Record and upload the Part 3 demonstration, add its real unlisted URL, and verify that it plays from a private browser window. |

## Part 3 video outline

Demonstrate these rubric areas in one coherent recording:

1. Start the complete Docker deployment and show the running API, MVC, SQL Server and Azurite services.
2. Briefly show the MVC/API project structure and explain that MVC communicates with the API rather than accessing SQL Server or Blob Storage directly.
3. Register or sign in as an Organiser, show role-specific navigation, dashboard, event/category management, enrolment roster and result capture.
4. Sign in as a Participant, show event filtering/details, category enrolment, My Events, results and profile editing.
5. Upload/display an event banner and a private profile picture; explain that the API owns Blob operations and profile pictures require authenticated retrieval.
6. Show the Docker Compose setup and the actual successful GitHub Actions run after it exists.

Do not show passwords, connection secrets, session-cookie values or personal data in the recording.
