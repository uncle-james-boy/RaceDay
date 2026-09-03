# RaceDay API Endpoint Plan

| HTTP Method | Endpoint Route | Allowed Role / Auth | Description & Purpose | Request Body Schema (JSON) | Response Status Codes & Failure Cases |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **POST** | `/api/auth/register` | Public | Registers a new user. | `{"fullName":"str","email":"str","password":"str","role":"str"}` | **201** Created, **400** Bad Request, **409** Conflict (email exists) |
| **POST** | `/api/auth/login` | Public | Authenticates user and returns JWT. | `{"email":"str","password":"str"}` | **200** OK, **400** Bad Request, **401** Unauthorized |
| **GET** | `/api/events` | Public | Retrieves all upcoming events. | *N/A* | **200** OK, **500** Internal Server Error |
| **POST** | `/api/events` | Organiser | Creates a new event. | `{"name":"str","date":"datetime","loc":"str","dist":"num","type":"str"}` | **201** Created, **400** Bad Request, **401** Unauthorized, **403** Forbidden |
| **GET** | `/api/events/{id}/categories` | Auth | Retrieves categories for an event. | *N/A* | **200** OK, **404** Not Found |
| **POST** | `/api/events/{id}/categories` | Organiser | Adds a category to an event. | `{"categoryName":"str","minAge":"num","maxAge":"num"}` | **201** Created, **400** Bad Request, **403** Forbidden, **404** Not Found |
| **POST** | `/api/enrolments` | Participant | Enrols in an event category. | `{"eventId":"num","categoryId":"num"}` | **201** Created, **400** Bad Request, **401** Unauthorized, **409** Conflict (duplicate) |
| **POST** | `/api/results` | Organiser | Submits results for an enrolment. | `{"enrolmentId":"num","finishTime":"str","position":"num"}` | **201** Created, **400** Bad Request, **403** Forbidden, **404** Not Found |
| **GET** | `/api/users/profile` | Authenticated | Retrieves user profile details. | *N/A* | **200** OK, **401** Unauthorized |
| **PUT** | `/api/users/profile` | Authenticated | Updates user profile details. | `{"fullName":"str","profilePictureUrl":"str"}` | **200** OK, **400** Bad Request, **401** Unauthorized |