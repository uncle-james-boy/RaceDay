# Part 1 Video Presentation Guide

Record a short, clear walkthrough using your own voice. The assignment prohibits AI-generated
voices. Publish the recording as an unlisted YouTube video and add the real link to the README.

## Suggested order

1. Introduce RaceDay and the two roles: Organiser and Participant.
2. Show the ERD and explain all six entities and their relationships. Explain that an Event can
   have multiple EventRoutes for named courses and route details.
3. Point out primary keys, foreign keys, cardinality, important constraints, and the composite
   Enrolments `(CategoryID, EventID)` foreign key.
4. Explain how the SQL setup creates and selects `RaceDayDB`, and that it stops rather than
   dropping existing RaceDay tables. If demonstrating SQL, use a disposable test database and
   verify it contains no data you need.
5. Walk through the API plan, including session-based login, role checks, ownership checks, and
   the route/category/enrolment/result endpoints.
6. Demonstrate the real CI run only after GitHub Actions has completed successfully. Show the
   actual green run; do not use a placeholder screenshot.
7. When Part 2 is implemented, demonstrate the running API, Swagger, session behaviour, and
   relevant passing tests.

Explain the choices in your own words. Do not claim that a feature or check works unless you
have demonstrated or otherwise verified it.
