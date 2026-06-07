#Database thoughts
## General Rules
-All table names are plural
-All have a natural key or a surrogate key for a primary key
-Index all foreign keys
-In EF, make so you can .Include(related_table)

## Database Rough Draft

### Forms

- FormId Guid - note will be used to be a parameter when you link to an url and want it not to be guessable
- FormName -string 50 max
- FormCreatorEmail string email of person nvhar(255)
- Description string 255 max
- SecurityTypeId int Lookup table 
- RandomizeOrder bool
- Published bool (Defaults to false ie draft and true is published)
- Quota int nullable (limit to number of responses)
- CreatedAt datetime2
- UpdatedAt datetime2

### SecurityTypes
- SecurityTypeId int
- SecurityTypeValue string 25

#### Values for security types
 - 1 - Public 
 - 2 -Private 
 - 3- Url allowed

### Sections
- (Note when you enter a new form you also enter in a new section with blank name)
- SectionId int identity
- FormID Guid
- SectionName string 50 max (blank name is not shown)
- IsMatrix bool
- Order int
- ShowAsPage bool (allows the user to group questions or make the user answer 1 at a time or the whole lot)

### Questions
- QuestionId int identity
- SectionId int foriegn key
- Order int
- Question string max
- QuestionType int Lookup Table -See QuestionTypes and seeded values below
- QuestionAttributes max json table of special attributes which enforce things like min, max, values, required, value pairs for radio and checklists

### Answers
- AnswerId int identity
- SubmissionId GUID fk
- QuestionId int foreign key
- AnswerScalar max (for single value answers)
- AnswerJSON max (for multi value answers)

### QuestionTypes
- QuestionTypeId int 
- QuestionTypeName string 50

#### List of QuestionTypes to be pre entered on database creation
* 1- Text 
* 2- Long text
* 3 -Number
* 4 -Radio Button 
* 5 -Checkbox List
* 6 -Dropdown 
* 7- Date
* 8 -Time
* 9 -Date Time
* 10 -Image
* 11 -PDF
* 12- Rating Scales
* 13 - Likert (5 or 10)
* 14 -Range
* 15 -Email
* 16- Phone
* 17 - Url
* 18 - Net Promotor Score
* 19 - Yes/No
* 20- CheckList with number values
* 21 - Dropdown with number values
* 22 - Radio with number values
* 23 - Insert previous answer
* 24 - Calcuation (+ - * / mean mod max min abs ( ) squareroot -must be numbers)



### FormAllowedUsers
- FormAllowedUserId int identity
- FormId Guid FK
- UserEmail string 255


### FormSubmissions

-   SubmissionId   GUID        
-   FormId         Guid        FK → Forms — which form was taken
-   UserEmail         nvarchar    FK → AspNetUsers>User ie email address, nullable (null = anonymous) 255
-  StartedAt      datetime2   when the first page loaded
-  SubmittedAt    datetime2   nullable — null means they never finished
-  IsComplete     bool        false until they click Submit
-  IpAddress      nvarchar    nullable — helps catch duplicate/bot submissions