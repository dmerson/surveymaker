# Page Structure and Architecture

## Pages

### `/` — Login Page
Ocean-themed login page with an animated wave backdrop, frosted glass card, Google sign-in button,
and a spinner while the app checks for an existing session. If the user is already logged in when
they land here, they are immediately redirected to `/dashboard` (or the `returnUrl` query param if
one was set by the auth guard).

### `/dashboard` — Post-Login Home
The first page a user sees after signing in. Displays a personalised greeting based on time of day,
three stat cards (My Forms, Responses Received, Surveys Completed), quick-action buttons (New Form,
Browse Surveys), and two side-by-side content panels for Recent Forms and Recent Activity. All counts
start at zero and will populate as the user creates forms and takes surveys.

### `/my-forms` — Form Library
Lists all forms the user has created. Currently shows an empty state with a Create Your First Form
call-to-action. The grid layout and form card structure are in place ready for real data to be wired
in from the API.

### `/create-form` — Form Builder
A form builder shell. Has working Title and Description inputs at the top. Below those is a question
builder area showing placeholder buttons for the four planned question types (Short Answer, Long
Answer, Multiple Choice, Rating). The question builder itself is marked as coming soon — the
scaffolding is there for the real implementation.

### `/my-surveys` — Completed Surveys History
Shows surveys the logged-in user has taken. Currently shows an empty state with a Find a Survey to
Take call-to-action. The row layout with status badges (complete/incomplete) is ready for data.

### `/public-survey` — Community Survey Browser
A browsable list of surveys that form creators have made public. Includes a search bar at the top
(disabled until data is wired up) and a card grid layout. Empty state links to Create Form so users
can contribute.

---

## How Authentication Works Across All Pages

All five pages above are protected. The flow works as follows:

1. A user visits any protected URL (e.g. `/dashboard`) without being logged in.
2. The `authGuard` in `src/app/guards/auth.guard.ts` calls `GET /api/account/user`. If the response
   says `isAuthenticated: false`, the guard redirects to `/?returnUrl=/dashboard`.
3. The login page reads the `returnUrl` query parameter and stores it.
4. The user clicks **Sign in with Google**. The login call navigates to
   `/api/account/login?returnUrl=/dashboard`.
5. The .NET `AccountController` initiates the Google OAuth challenge, embedding the `returnUrl` in
   the OAuth state so it survives the round-trip to Google and back.
6. After Google authenticates the user, `AccountController.GoogleCallback` creates or finds the user
   account, signs them in (sets the auth cookie), and redirects to `/dashboard`.
7. Angular loads the dashboard, calls `/api/account/user` again (this time authenticated), and
   renders the user's name and data.

If the user is already logged in when the guard runs, the cached value in `AuthService.currentUser`
is used and no HTTP call is made, so navigation between pages is instant.

---

## Shared Layout

All five protected pages share the `Layout` component (`src/app/layout/`). It provides:

- **Sticky header** with an ocean-gradient background (deep navy to mid-blue).
- **Survey Maker icon** — an inline SVG showing two form lines and a teal wave, rendered at 36 px
  in the header. The same icon appears larger on the login card.
- **Navigation links** — Dashboard, My Forms, Create Form, My Surveys, Public Surveys — with an
  active-state underline highlight in bright teal.
- **User menu** on the right — an avatar circle showing the user's initials, their display name,
  and a Sign Out button.
- A `<router-outlet>` below the header where each page component renders.

---

## Color Palette

Sourced from the electric teal wave face, deep navy shadow, and warm sand edges of the wave pool
reference image.

| Token          | Hex       | Used for                                  |
|----------------|-----------|-------------------------------------------|
| `--c-deep`     | `#023047` | Header background, headings, dark text    |
| `--c-ocean`    | `#006994` | Brand icon fill, primary buttons          |
| `--c-teal`     | `#0096c7` | Active nav highlight, stat values         |
| `--c-bright`   | `#00b4d8` | Wave accent, gradient buttons, focus ring |
| `--c-aqua`     | `#90e0ef` | Welcome banner subtitle, light accents    |
| `--c-foam`     | `#caf0f8` | Badge backgrounds, light fills            |
| `--c-surface`  | `#f0f9ff` | Page background                           |
| `--c-sand`     | `#b47b55` | Warm accent (reserved for future use)     |
