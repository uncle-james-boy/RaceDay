# RaceDay API Endpoint Plan

This document records the API contracts used by the Part 2 and Part 3 applications. Part 3 adds
two owner-scoped organiser reads, one event-banner upload, and two authenticated profile-picture
operations. The original Part 2 contract has 23 operations; the current API has 28. The data model is in
[`schema.dbml`](../schema.dbml) and [`RaceDay_Database.sql`](RaceDay_Database.sql). The six
entities are Users, Events, Categories, EventRoutes, Enrolments, and Results.

## Session authentication

Login creates an ASP.NET Core server-side session. The session stores the authenticated `UserID`
and `Role`; the browser receives only the session cookie. Protected endpoints return 401 when
there is no valid session and 403 when the role or resource ownership is wrong. Passwords are
hashed before storage. No JWT is issued.

## Access rules

- **Public:** register, login, browse events, event details, categories, and routes.
- **Authenticated:** view/update the caller's profile and logout.
- **Organiser:** create events; manage only owned events, categories, and routes; view enrolments
  and capture results for owned events.
- **Participant:** enrol and view only the caller's enrolments and results.

## Authentication and profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/auth/register` | Creates an account and hashes its password. | Public | `{ fullName, email, password, role }` | 201 Created; 400 invalid fields; 409 email exists |
| POST | `/api/auth/login` | Verifies credentials and starts a server-side session. | Public | `{ email, password }` | 200 OK with session cookie; 400 invalid fields; 401 bad credentials |
| POST | `/api/auth/logout` | Clears the current session. | Authenticated | None | 204 No Content; 401 no session |
| GET | `/api/users/profile` | Reads the caller's profile. | Authenticated | None | 200 OK; 401 no session |
| PUT | `/api/users/profile` | Updates the caller's profile details. A caller cannot submit a Blob URL. | Authenticated | `{ fullName }` | 200 OK; 400 invalid fields; 401 no session |
| POST | `/api/users/profile/picture` | Uploads or replaces the signed-in user's private profile picture. Accepts JPEG, PNG, or WebP after content validation; maximum 2 MB. The API streams the image only through the authenticated retrieval route. | Authenticated; own account is selected from the session | Multipart form field `file` | 200 OK; 400 invalid image/size; 401 no session; 500 database save failed; 502 storage unavailable |
| GET | `/api/users/profile/picture` | Streams the caller's private profile picture; does not return a public Blob URL. | Authenticated; own account is selected from the session | None | 200 image; 401 no session; 404 no image; 502 storage unavailable |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/events` | Lists events available for browsing; optional `search`, `eventType`, `fromDate`, and `toDate` filters are supported. | Public | None | 200 OK; 400 invalid filter |
| GET | `/api/events/mine` | Lists only the signed-in Organiser's events with enrolment totals. | Organiser | None | 200 OK; 401 no session; 403 wrong role |
| GET | `/api/events/{eventId}` | Reads one event's details. | Public | None | 200 OK; 404 event not found |
| POST | `/api/events` | Creates an event owned by the caller. | Organiser | `{ name, description, eventDate, location, distance, eventType }` | 201 Created; 400 invalid fields; 401 no session; 403 wrong role |
| PUT | `/api/events/{eventId}` | Updates an event owned by the caller. | Owner Organiser | Same as event create | 200 OK; 400 invalid fields; 401 no session; 403 not owner; 404 not found |
| DELETE | `/api/events/{eventId}` | Deletes an owned event when no dependent records prevent deletion. | Owner Organiser | None | 204 No Content; 401 no session; 403 not owner; 404 not found; 409 event has related records |
| POST | `/api/events/{eventId}/banner` | Uploads or replaces the owned event's public banner after JPEG, PNG, or WebP content validation; maximum 5 MB. The server generates the Blob name and stores the resulting URL. | Owner Organiser | Multipart form field `file` | 200 OK; 400 invalid image/size; 401 no session; 403 not owner; 404 event not found; 500 database save failed; 502 storage unavailable |

## Categories and routes

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/events/{eventId}/categories` | Lists categories for an event. | Public | None | 200 OK; 404 event not found |
| POST | `/api/events/{eventId}/categories` | Adds a category to an event owned by the caller. | Owner Organiser | `{ categoryName, minAge, maxAge, distance }` | 201 Created; 400 invalid fields; 401 no session; 403 not owner; 404 event not found |
| PUT | `/api/categories/{categoryId}` | Updates a category under an event owned by the caller. | Owner Organiser | Same as category create | 200 OK; 400 invalid fields; 401 no session; 403 not owner; 404 not found |
| DELETE | `/api/categories/{categoryId}` | Deletes an unused category under an event owned by the caller. | Owner Organiser | None | 204 No Content; 401 no session; 403 not owner; 404 not found; 409 category has enrolments |
| GET | `/api/events/{eventId}/routes` | Lists route details for an event. | Public | None | 200 OK; 404 event not found |
| POST | `/api/events/{eventId}/routes` | Adds a route to an event owned by the caller. | Owner Organiser | `{ routeName, distance, routeDescription, routeMapUrl }` | 201 Created; 400 invalid fields; 401 no session; 403 not owner; 404 event not found |
| PUT | `/api/routes/{routeId}` | Updates a route under an event owned by the caller. | Owner Organiser | Same as route create | 200 OK; 400 invalid fields; 401 no session; 403 not owner; 404 not found |
| DELETE | `/api/routes/{routeId}` | Deletes a route under an event owned by the caller. | Owner Organiser | None | 204 No Content; 401 no session; 403 not owner; 404 not found |

## Enrolments and results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/enrolments` | Enrols the caller using a category from the selected event. | Participant | `{ eventId, categoryId }` | 201 Created; 400 category/event mismatch; 401 no session; 403 wrong role; 404 event/category not found; 409 duplicate enrolment |
| GET | `/api/enrolments` | Lists only the caller's enrolments; identity is read from the session. | Participant | None | 200 OK; 401 no session; 403 wrong role |
| GET | `/api/events/{eventId}/enrolments` | Lists enrolments for an event owned by the caller. | Owner Organiser | None | 200 OK; 401 no session; 403 not owner; 404 event not found |
| GET | `/api/events/{eventId}/results` | Lists published results for an event owned by the caller. | Owner Organiser | None | 200 OK; 401 no session; 403 not owner; 404 event not found |
| POST | `/api/results` | Records a result for an enrolment in an event owned by the caller. | Owner Organiser | `{ enrolmentId, finishTime, position }` | 201 Created; 400 invalid fields; 401 no session; 403 not owner; 404 enrolment not found; 409 result already exists |
| GET | `/api/results` | Lists only the caller's results; identity is read from the session. | Participant | None | 200 OK; 401 no session; 403 wrong role |

API responses never include `PasswordHash`. Organiser ownership is checked against the event
that owns the resource. A Participant cannot read another Participant's records. An enrolment's
`CategoryID` and `EventID` must refer to the same event, enforced by the composite foreign key.
