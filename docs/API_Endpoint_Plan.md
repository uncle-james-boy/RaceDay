
#### [Code Block C] API Plan (Auth)
```markdown
# RaceDay API Endpoint Plan

| HTTP Method | Endpoint Route | Allowed Role / Auth | Description & Purpose | Request Body Schema (JSON) | Response Status Codes & Failure Cases |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **POST** | `/api/auth/register` | Public | Registers a new user. | `{"fullName":"str","email":"str","password":"str","role":"str"}` | **201** Created<br>**400** Bad Request<br>**409** Conflict |
| **POST** | `/api/auth/login` | Public | Authenticates user and returns JWT. | `{"email":"str","password":"str"}` | **200** OK<br>**400** Bad Request<br>**401** Unauthorized |

| **POST** | `/api/enrolments` | Participant | Enrols in an event category. | `{"eventId":"num","categoryId":"num"}` | **201** Created<br>**400** Bad Request<br>**401** Unauthorized<br>**409** Conflict |
| **POST** | `/api/results` | Organiser | Submits results for an enrolment. | `{"enrolmentId":"num","finishTime":"str","position":"num"}` | **201** Created<br>**400** Bad Request<br>**403** Forbidden<br>**404** Not Found |
| **GET** | `/api/users/profile` | Authenticated | Retrieves user profile details. | *N/A* | **200** OK<br>**401** Unauthorized |
| **PUT** | `/api/users/profile` | Authenticated | Updates user profile details. | `{"fullName":"str","profilePictureUrl":"str"}` | **200** OK<br>**400** Bad Request<br>**401** Unauthorized |