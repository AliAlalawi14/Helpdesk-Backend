# Helpdesk API

REST API for the support helpdesk. ASP.NET Core (.NET 10), EF Core, PostgreSQL, JWT bearer
auth, FluentValidation. Serves the `helpdesk-web` frontend in the sibling folder.

## Running

You need the .NET 10 SDK and a PostgreSQL instance. Docker is the quickest way to the
database; a local Postgres works just as well.

### 1. Configuration

`appsettings.Development.json` is **not** in the repository — it holds the database
connection string. Copy the template and fill it in:

```bash
cd backend
cp appsettings.Development.example.json appsettings.Development.json
```

| Setting | What it is |
|---|---|
| `ConnectionStrings:Database` | Postgres connection string. Must match the database you start below. |
| `Jwt:Key` | Signing key for access tokens. Any string of at least 32 characters. |
| `Jwt:Issuer` / `Jwt:Audience` | Leave as `helpdesk-api` / `helpdesk-client`. |
| `Encryption:Key` | Must be exactly 32 bytes. |
| `Cors:AllowedOrigins` | Already lists the Vite dev server. |

### 2. Database

With Docker, from the `backend` folder:

```bash
docker compose up -d devhabit.postgres
```

That publishes Postgres on **5439**, so the connection string is
`Host=localhost;Port=5439;Database=TicketingSystem;Username=postgres;Password=postgres`.

The compose file reads `POSTGRES_PASSWORD` from the environment and falls back to
`postgres`, so no credential is committed. Set your own in a local `.env` beside
`docker-compose.yml` if you prefer.

Running Postgres yourself instead? Create an empty database and point the connection
string at it — the schema is created for you in the next step.

### 3. Run

```bash
cd backend
dotnet run --launch-profile http     # http://localhost:5000
```

**Migrations and seeding both run automatically on startup, in Development only**
(`Program.cs`, the `app.Environment.IsDevelopment()` block). There is no separate seed
command to run:

1. `ApplyMigrationsAsync()` — creates the application schema
2. `ApplyIdentityMigrationsAsync()` — creates the ASP.NET Identity schema
3. `SeedUsersAsync()` — the four accounts below, credentials plus domain records
4. `SeedInitialDataAsync()` — three categories and twelve tickets

Both seeders are idempotent: each checks whether its data already exists and returns early,
so restarting the API never duplicates anything. To reseed from scratch, drop the database
(or `docker compose down -v`) and start the API again.

In any environment other than Development none of this runs, and the schema must be applied
with `dotnet ef database update` for each context.

### 4. Frontend

Start `../helpdesk-web` (`npm install && npm run dev`) and open http://localhost:5173. Its
Vite dev server proxies `/api` to `http://localhost:5000`, so no CORS setup is needed for
local work.

## Seeded logins

All four use the password `Password123!`:

| Email | Role | Lands on |
|---|---|---|
| `admin@example.com` | admin | `/admin` |
| `sam@example.com` | moderator | `/queue` |
| `priya@example.com` | moderator | `/queue` |
| `jordan@example.com` | user | `/tickets` |

Seed data is twelve tickets spread across all four statuses, all four priorities and the
three categories, with five unassigned — enough to exercise every filter on the queue
without creating anything.

## API

| Method | Route | Who |
|---|---|---|
| `POST` | `/api/auth/login` | anonymous |
| `GET` | `/api/auth/me` | authenticated |
| `POST` | `/api/auth/logout` | authenticated |
| `GET` | `/api/tickets` | authenticated — a user sees only their own, staff see all |
| `POST` | `/api/tickets` | authenticated |
| `GET` | `/api/tickets/{id}` | its requester, or any moderator/admin |
| `PATCH` | `/api/tickets/{id}` | moderator, admin |
| `DELETE` | `/api/tickets/{id}` | admin — soft delete |
| `POST` | `/api/tickets/{id}/comments` | its requester, or any moderator/admin |
| `GET` | `/api/categories` | authenticated |
| `POST` `PATCH` `DELETE` | `/api/categories`, `/api/categories/{id}` | admin |
| `GET` | `/api/users` | admin |
| `GET` | `/api/users/assignable` | moderator, admin |
| `PATCH` | `/api/users/{id}` | admin |
| `GET` | `/api/metrics/overview`, `/agents`, `/requesters` | admin |

Every one of these is enforced on the route itself, not only in the UI. Ownership rules that
an attribute cannot express — a requester reading or commenting on their own ticket — are
checked in the action and answer 403.

OpenAPI is served at `/openapi/v1.json` in Development.

### Conventions

**Query-driven lists.** `GET /api/tickets`, `/api/categories`, `/api/users` and
`/api/metrics/requesters` all take `page`, `limit` and `sort`, and return the same envelope:

```json
{ "data": [ … ], "pagination": { "page": 1, "limit": 20, "totalItems": 12, "totalPages": 1 } }
```

Pagination, filtering and sorting are executed in the database, never in memory.

**Ticket filters** — `status` and `priority` and `category` and `requester` take
comma-separated lists; `assignee` takes one of `me`, `unassigned` or a user id; `search`
matches subject or description; `createdBefore` and `updatedBefore` take ISO instants.

**Sorting** is one `sort` parameter carrying an ordered list: `?sort=priority desc,createdAt`.
Direction follows the field after a space and defaults to ascending. Fields are whitelisted
per resource and an unknown one is a 400 rather than a silent no-op.

**Errors are RFC 7807 ProblemDetails** — `type`, `title`, `status`, `detail`, plus a
`requestId`. Validation failures add an `errors` object keyed by lowercased field name, which
is what lets the frontend place a server error on the field that caused it.

**Enum casing is asymmetric on purpose.** Responses emit PascalCase (`InProgress`), while
filters accept `in_progress`, `inprogress` or `InProgress`. The frontend adapts this in one
named place rather than at each call site.
