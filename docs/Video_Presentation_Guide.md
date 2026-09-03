# Video Presentation Guide & Script Outline

**Total Target Time:** 3 to 5 Minutes

## I. Introduction (0:00 - 0:45)
* **Welcome & Role:** Introduce yourself as the Lead DBA and Principal Architect for the RaceDay platform.
* **Project Goal:** Briefly state the purpose of Part 1: establishing a robust, scalable data foundation and API routing plan.
* **Agenda Overview:** Mention the three key areas: Database Execution, ERD Design, and the API Plan.

## II. SSMS Query Execution & Data Integrity (0:45 - 2:00)
* **Screen Share:** Open SSMS and display the `RaceDay_Database.sql` script.
* **Drop Logic:** Highlight the `DROP TABLE IF EXISTS` block, explaining the importance of dropping in *reverse dependency order*.
* **Constraints Execution:** Point out the use of `CHECK` constraints (e.g., restricting Roles and Event Types).
* **Seed Data:** Show the generated tables and briefly display the realistic seed data to prove relational integrity.

## III. ERD Design Choices & DBML (2:00 - 3:30)
* **Visual Transition:** Show the exported `Part1_ERD.png` or the DBML syntax.
* **Cardinality Deep Dive:** Explain the **1-to-Many** relationships and the **1-to-1** relationship between `Enrolments` and `Results` (enforced by the `UNIQUE` constraint on the foreign key).
* **Normalization:** Briefly mention how separating `Categories` from `Events` allows dynamic distance/age grouping.

## IV. API Routing Plan Review (3:30 - 4:45)
* **Screen Share:** Open `docs/API_Endpoint_Plan.md`.
* **Role-Based Access Control (RBAC):** Highlight how the "Allowed Role / Auth" column ensures Organisers cannot enrol in events, and Participants cannot post results.
* **Failure Handling:** Emphasize granular failure cases (e.g., returning `409 Conflict` for duplicate enrolments).

## V. Conclusion (4:45 - 5:00)
* **Summary:** Reiterate that Part 1 provides a bulletproof data layer and clear API contract.
* **Next Steps:** Mention Part 2 will focus on backend implementation.
* **Sign-off:** Thank the stakeholders.