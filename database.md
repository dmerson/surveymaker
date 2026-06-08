# Database Design

## General Rules
- All table names are plural
- All tables have a natural key or surrogate key as primary key
- Index all foreign keys
- In EF, configure navigation properties to enable `.Include(relatedTable)`

---

## Tables

### Forms
| Column | Type | Notes |
|--------|------|-------|
| FormId | uniqueidentifier | PK — used in URLs so it is not guessable |
| FormName | nvarchar(50) | NOT NULL |
| FormCreatorEmail | nvarchar(255) | NOT NULL, indexed |
| Description | nvarchar(255) | nullable |
| SecurityTypeId | int | NOT NULL, FK → SecurityTypes, indexed |
| RandomizeOrder | bit | |
| Published | bit | default false (false = draft, true = published) |
| Quota | int | nullable — max responses allowed |
| CreatedAt | datetime2 | |
| UpdatedAt | datetime2 | |

---

### SecurityTypes
| Column | Type | Notes |
|--------|------|-------|
| SecurityTypeId | int | PK |
| SecurityTypeValue | nvarchar(25) | NOT NULL |

#### Seeded Values
| Id | Value |
|----|-------|
| 1 | Public |
| 2 | Private |
| 3 | Url Allowed |

---

### FormAllowedUsers
Stores per-form allow-list for private/URL-restricted forms.

| Column | Type | Notes |
|--------|------|-------|
| FormAllowedUserId | int identity | PK |
| FormId | uniqueidentifier | NOT NULL, FK → Forms, indexed |
| UserEmail | nvarchar(255) | NOT NULL |

---

### FormSubmissions
One row per survey attempt. `IsComplete` flips to true on final submit.

| Column | Type | Notes |
|--------|------|-------|
| SubmissionId | uniqueidentifier | PK |
| FormId | uniqueidentifier | NOT NULL, FK → Forms, indexed |
| UserEmail | nvarchar(255) | nullable (null = anonymous), indexed |
| StartedAt | datetime2 | when the first page loaded |
| SubmittedAt | datetime2 | nullable — null means not yet finished |
| IsComplete | bit | false until the user clicks Submit |
| IpAddress | nvarchar(45) | nullable — helps detect duplicates/bots |

---

### Sections
Note: a new Form automatically gets one blank-name Section.

| Column | Type | Notes |
|--------|------|-------|
| SectionId | int identity | PK |
| FormId | uniqueidentifier | NOT NULL, FK → Forms, indexed |
| SectionName | nvarchar(50) | NOT NULL — blank name means heading is hidden |
| IsMatrix | bit | when true, questions with the same options render as a single table |
| Order | int | |
| ShowAsPage | bit | group questions on one page vs. show one at a time |

---

### Questions
| Column | Type | Notes |
|--------|------|-------|
| QuestionId | int identity | PK |
| SectionId | int | NOT NULL, FK → Sections, indexed |
| Order | int | |
| Text | nvarchar(max) | NOT NULL (column stored as "Question") |
| QuestionTypeId | int | NOT NULL, FK → QuestionTypes, indexed |
| QuestionAttributes | nvarchar(max) | nullable — JSON blob of type-specific config (see below) |

#### QuestionAttributes JSON fields (by type)
| Field | Types that use it | Purpose |
|-------|-------------------|---------|
| `required` | all answerable types | bool — makes the field mandatory |
| `min` / `max` | Text(1), Long Text(2) | character length limits |
| `min` / `max` | Number(3), Rating(12), Range(14) | numeric value limits |
| `options` | Radio(4), Checkbox(5), Dropdown(6) | `string[]` of choices |
| `options` | Checkbox Val(20), Dropdown Val(21), Radio Val(22) | `{ text, value }[]` scored choices |
| `scale` | Likert(13) | number of scale points (5, 7, or 10) |
| `yesNoStyle` | Yes/No(19) | `"radio"` or `"checkbox"` |
| `html` | Instruction(0) | raw HTML content displayed full-width |
| `help` | any type | HTML help text shown on demand via ? icon |
| `tokens` | Calculation(24) | `FormulaToken[]` — formula definition |
| `graphType` | Graph(25) | `"bar"`, `"line"`, `"histogram"`, `"pie"`, or `"radar"` |
| `sourceQuestionIds` | Graph(25) | `number[]` — question IDs whose answers are plotted |
| `conditionalLogic` | Conditional Logic(26) | `{ condition: { questionId, operator, value }, thenActions: [{ questionId, action }] }` |

---

### QuestionTypes
| Column | Type | Notes |
|--------|------|-------|
| QuestionTypeId | int | PK |
| QuestionTypeName | nvarchar(50) | NOT NULL |

#### Seeded Values
| Id | Name | Notes |
|----|------|-------|
| 0 | Instruction | Displays HTML content full-width; not a question, not submitted |
| 1 | Text | Single-line text input |
| 2 | Long Text | Multi-line textarea |
| 3 | Number | Numeric input |
| 4 | Radio Button | Single-select from a list of options |
| 5 | Checkbox List | Multi-select from a list of options |
| 6 | Dropdown | Single-select dropdown |
| 7 | Date | Date picker |
| 8 | Time | Time picker |
| 9 | Date Time | Combined date and time |
| 10 | Image | File upload — image only; stored in AnswerFiles |
| 11 | PDF | File upload — PDF only; stored in AnswerFiles |
| 12 | Rating Scale | Numeric rating (configurable min/max) |
| 13 | Likert | Agreement scale (5, 7, or 10 points) |
| 14 | Range | Slider between a min and max value |
| 15 | Email | Text input validated as email |
| 16 | Phone | Text input for phone number |
| 17 | Url | Text input validated as URL |
| 18 | Net Promoter Score | 0–10 NPS scale |
| 19 | Yes/No | Two-option question; renders as radio buttons or a single checkbox |
| 20 | Checklist With Number Values | Checkbox list where each option has a numeric score |
| 21 | Dropdown With Number Values | Dropdown where each option has a numeric score |
| 22 | Radio With Number Values | Radio list where each option has a numeric score |
| 23 | Insert Previous Answer | Embeds `{{Q:ID}}` placeholders in question text; resolved to live answers at render time |
| 24 | Calculation | Computes a numeric result from a formula referencing other question answers |
| 25 | Graph | Renders a chart (bar, line, histogram, pie, radar) from other numeric question answers |
| 26 | Conditional Logic | Defines an IF/THEN rule that shows, hides, or requires other questions based on answer values; invisible to respondents |

---

### Answers
One row per question per submission. Either `AnswerScalar` or `AnswerJson` is populated.

| Column | Type | Notes |
|--------|------|-------|
| AnswerId | int identity | PK |
| SubmissionId | uniqueidentifier | NOT NULL, FK → FormSubmissions, indexed |
| QuestionId | int | NOT NULL, FK → Questions (NoAction on delete), indexed |
| AnswerScalar | nvarchar(max) | nullable — single-value answers (text, number, date, selected option, etc.) |
| AnswerJson | nvarchar(max) | nullable — multi-value answers (checkbox selections stored as JSON array) |

---

### AnswerFiles
Stores binary file uploads for Image (type 10) and PDF (type 11) questions. File content is stored directly in the database as `varbinary(max)`.

| Column | Type | Notes |
|--------|------|-------|
| FileId | uniqueidentifier | PK, default NEWID() |
| SubmissionId | uniqueidentifier | NOT NULL, FK → FormSubmissions (cascade delete), indexed |
| QuestionId | int | NOT NULL — denormalized reference, not a FK |
| FileName | nvarchar(260) | NOT NULL — original uploaded file name |
| ContentType | nvarchar(100) | NOT NULL — MIME type e.g. `image/jpeg`, `application/pdf` |
| FileData | varbinary(max) | NOT NULL — raw file bytes |
| FileSizeBytes | bigint | |
| UploadedAt | datetime2 | |

> **Note:** QuestionId is stored for query efficiency but is not a foreign key — this avoids cascade conflicts when questions are deleted independently of submissions.

---

## Relationships

| Child Table | Foreign Key | Parent Table | Parent Key | On Delete |
|-------------|-------------|--------------|------------|-----------|
| Forms | SecurityTypeId | SecurityTypes | SecurityTypeId | Cascade |
| FormAllowedUsers | FormId | Forms | FormId | Cascade |
| FormSubmissions | FormId | Forms | FormId | Cascade |
| Sections | FormId | Forms | FormId | Cascade |
| Questions | SectionId | Sections | SectionId | Cascade |
| Questions | QuestionTypeId | QuestionTypes | QuestionTypeId | Cascade |
| Answers | SubmissionId | FormSubmissions | SubmissionId | Cascade |
| Answers | QuestionId | Questions | QuestionId | NoAction |
| AnswerFiles | SubmissionId | FormSubmissions | SubmissionId | Cascade |

---

## Identity Tables (ASP.NET Core Identity)
Standard Identity tables — not modified. Key tables:

| Table | Purpose |
|-------|---------|
| AspNetUsers | User accounts; `UserName` and `Email` store the Google account email |
| AspNetRoles | Role definitions |
| AspNetUserRoles | User ↔ Role assignments |
| AspNetUserLogins | External login providers (Google OAuth) |
| AspNetUserClaims | Claims attached to users |
| AspNetUserTokens | Tokens (refresh, 2FA, etc.) |
