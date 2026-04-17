# MOMBotPro

MOMBotPro is an AI-powered automation platform built for software development teams. It joins Zoom meetings, transcribes the conversation, identifies bugs discussed during the meeting, creates a Jira ticket, scans the relevant codebase, generates a code fix, and raises a GitHub pull request — all without manual intervention.

Built for the **OutCreate 2025 Hackathon** at Innoworks Software.

---

## Table of Contents

- [How It Works](#how-it-works)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Integration Setup](#integration-setup)
- [API Reference](#api-reference)
- [Database](#database)
- [Security](#security)

---

## How It Works

MOMBotPro runs a six-step automated pipeline triggered either by a live Zoom meeting or a manually entered transcript.

```
Step 1 — Transcribe Audio
         Recall.ai bot joins the Zoom call, records it, and
         sends the audio to Whisper for transcription.

Step 2 — Extract Bug from MOM
         GPT-4o reads the transcript and extracts the bug
         summary, minutes of meeting, and client context.

Step 3 — Create Jira Ticket
         A Jira task is created automatically with the bug
         summary, priority level, and client details.

Step 4 — Scan Codebase
         The GitHub repository is scanned for files relevant
         to the bug using keyword matching on the file tree.

Step 5 — Generate Fix
         GPT-4o analyzes the relevant code and produces a
         fix including the original code, corrected code,
         root cause, and line number.

Step 6 — Create Branch and Raise PR
         A new branch is created from master, the fix is
         committed, and a pull request is raised to Uat1.
         A Slack notification is sent on completion.
```

---

## Tech Stack

### Frontend

| Technology | Purpose |
|---|---|
| React 18 + Vite | UI framework and build tooling |
| Tailwind CSS | Styling |
| React Router v6 | Client-side routing |
| Chart.js | Analytics and usage charts |

### Backend

| Technology | Purpose |
|---|---|
| ASP.NET Core (.NET 8) | REST API |
| Entity Framework Core 8 | ORM and database migrations |
| SQL Server | Primary database |
| JWT Bearer Authentication | Auth token management |
| BCrypt.Net | Password hashing |
| Swagger / Swashbuckle | API documentation |
| Xabe.FFmpeg | Audio extraction from recordings |

### External Services

| Service | Purpose |
|---|---|
| OpenAI GPT-4o | Transcript analysis, bug extraction, code fix generation |
| Anthropic Claude | Alternative AI for code generation tasks |
| Recall.ai | Zoom bot — joins, records, and transcribes meetings |
| Jira REST API v3 | Automated ticket creation |
| GitHub REST API v3 | Branch creation, file commits, pull requests |
| Zoom OAuth 2.0 | Meeting access and recording retrieval |
| Slack Incoming Webhooks | Pipeline completion notifications |

---

## Project Structure

```
MOMBotPro/
|
|-- frontend/
|   |-- src/
|   |   |-- pages/
|   |   |   |-- Dashboard.jsx              Overview and recent pipeline runs
|   |   |   |-- PipelineDashboard.jsx      Full list of all pipelines
|   |   |   |-- PipelineDetail.jsx         Live step-by-step pipeline tracker
|   |   |   |-- NewPipeline.jsx            Manually start a pipeline
|   |   |   |-- ZoomJoin.jsx               Join and trigger from a Zoom meeting
|   |   |   |-- Integrations.jsx           Connect Jira, GitHub, Slack, Gmail, Zoom
|   |   |   |-- Analytics.jsx              Charts and usage statistics
|   |   |   |-- Pricing.jsx                Subscription plans
|   |   |   |-- Settings.jsx               Account settings
|   |   |   |-- Login.jsx
|   |   |   └-- Register.jsx
|   |   |-- components/
|   |   |   |-- Layout.jsx                 Sidebar and navigation wrapper
|   |   |   |-- ui.jsx                     Shared UI components
|   |   |   └-- Footer.jsx
|   |   |-- context/
|   |   |   |-- AuthContext.jsx            JWT auth state management
|   |   |   |-- ThemeContext.jsx           Dark/light mode
|   |   |   └-- ToastContext.jsx           Toast notifications
|   |   └-- utils/
|   |       └-- authFetch.js               Fetch wrapper with Bearer token
|   └-- vite.config.js
|
└-- backend/
    └-- MOMBotPro.API/
        |-- Controllers/
        |   |-- AuthController.cs          Register, login, /me
        |   |-- PipelineController.cs      Start and poll pipeline runs
        |   |-- IntegrationController.cs   Save and retrieve integrations
        |   |-- ZoomController.cs          Zoom bot and OAuth callback
        |   └-- SubscriptionController.cs  Plan management
        |-- Services/
        |   |-- PipelineOrchestrator.cs    Core 6-step pipeline engine
        |   |-- RealJiraService.cs         Jira REST API v3 integration
        |   |-- GitHubService.cs           Branch creation and PR management
        |   |-- RecallService.cs           Recall.ai bot and recording download
        |   |-- OpenAIService.cs           GPT-4o calls
        |   |-- ClaudeService.cs           Anthropic Claude calls
        |   |-- UserRepository.cs          User and integration CRUD via stored procs
        |   |-- TokenService.cs            JWT generation and validation
        |   └-- ZoomSessionRepository.cs   Active Zoom session tracking
        |-- Data/
        |   |-- ApplicationDbContext.cs    EF Core DB context
        |   └-- StoredProcedureHelper.cs   Stored procedure execution helper
        |-- Models/
        |   |-- Models.cs                  Pipeline, BugAnalysis, GitHubResult, etc.
        |   └-- AuthModels.cs              AppUser, Integration, request/response DTOs
        |-- Migrations/                    EF Core auto-generated migrations
        |-- SqlScripts/
        |   └-- StoredProcedures/
        |       |-- 00_RunAll.sql          Run this once in SSMS after migrations
        |       |-- sp_GetUserByEmail.sql
        |       |-- sp_GetUserById.sql
        |       |-- sp_CreateUser.sql
        |       |-- sp_DecrementFreeTrial.sql
        |       |-- sp_UpdateSubscription.sql
        |       |-- sp_GetIntegrationsByUser.sql
        |       └-- sp_SaveIntegration.sql
        |-- appsettings.Example.json       Safe template — copy and rename this
        └-- Program.cs
```

---

## Prerequisites

The following must be installed before running the project:

- Node.js v18 or later
- .NET 8 SDK
- SQL Server (local instance or SQL Server Express)
- SQL Server Management Studio (SSMS)
- Git

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/Ramu-Runja/MOMBotPro.git
cd MOMBotPro
```

### 2. Configure backend secrets

```bash
cd backend/MOMBotPro.API
cp appsettings.Example.json appsettings.json
```

Open `appsettings.json` and fill in all values:

```json
{
  "Anthropic": {
    "ApiKey": "YOUR_ANTHROPIC_API_KEY"
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY"
  },
  "GitHub": {
    "Token": "YOUR_GITHUB_PAT",
    "Owner": "YOUR_GITHUB_USERNAME",
    "Repo":  "YOUR_TARGET_REPO"
  },
  "Recall": {
    "ApiKey": "YOUR_RECALL_API_KEY",
    "Region": "ap-northeast-1"
  },
  "Zoom": {
    "ClientId":      "YOUR_ZOOM_CLIENT_ID",
    "ClientSecret":  "YOUR_ZOOM_CLIENT_SECRET",
    "RedirectUri":   "http://localhost:5000/api/oauth/zoom/callback",
    "WebhookSecret": "YOUR_ZOOM_WEBHOOK_SECRET"
  },
  "Jwt": {
    "Key":      "MinimumThirtyTwoCharacterSecretKeyHere",
    "Issuer":   "MOMBotPro",
    "Audience": "MOMBotProClient"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MOMBotProDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Slack": {
    "WebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
  }
}
```

### 3. Set up the database

Run EF Core migrations to create all tables:

```bash
cd backend/MOMBotPro.API
dotnet ef database update
```

Then open SSMS, connect to your local SQL Server, and run the following file against the `MOMBotProDB` database:

```
backend/MOMBotPro.API/SqlScripts/StoredProcedures/00_RunAll.sql
```

This creates all seven stored procedures the application depends on.

### 4. Start the backend

```bash
cd backend/MOMBotPro.API
dotnet run
```

The API will start on `http://localhost:5000`.  
Swagger documentation is available at `http://localhost:5000/swagger`.

### 5. Start the frontend

```bash
cd frontend
npm install
npm run dev
```

The application will open at `http://localhost:3000`.

### 6. First-time setup in the app

1. Navigate to `http://localhost:3000` and register an account
2. Go to the Integrations page and connect each service
3. Go to New Pipeline, enter a client name and transcript, and start the pipeline
4. Monitor each of the six steps completing in real time on the pipeline detail page

---

## Integration Setup

### Jira

1. Generate an API token at `https://id.atlassian.com/manage-profile/security/api-tokens`
2. In MOMBotPro, go to Integrations and enter the following:
   - Domain: `yoursite.atlassian.net`
   - Email: your Atlassian account email
   - API Token: the token generated above
   - Project Key: for example, `DEV`

The application creates `Task` issue types only. It does not set `labels` or `reporter` fields, which are not supported in next-gen (team-managed) Jira projects.

### GitHub

1. Generate a personal access token at `https://github.com/settings/tokens/new`
2. Enable the `repo` (full) and `workflow` scopes
3. In MOMBotPro, enter the repository owner, repo name, and token
4. Ensure a branch named `Uat1` exists in the target repository — all pull requests are raised against this branch

### Zoom

1. Go to `https://marketplace.zoom.us/develop/apps` and open your app
2. Under the Scopes tab, add the following:
   - `meeting:read:meeting`
   - `meeting:read:list_meetings`
   - `meeting:write:meeting`
   - `cloud_recording:read:list_user_recordings`
   - `cloud_recording:read:list_recording_files`
   - `user:read:user`
3. Under the Information tab, set the OAuth Redirect URL to `http://localhost:5000/api/oauth/zoom/callback`
4. In MOMBotPro, go to Integrations and click Connect Zoom to complete the OAuth flow

### Slack

1. Create an app at `https://api.slack.com/apps` and enable Incoming Webhooks
2. Add the webhook to your target channel and copy the URL
3. In MOMBotPro, go to Integrations and paste the webhook URL

### Gmail

1. Enable 2-step verification on your Google account
2. Generate an app password at `https://myaccount.google.com/apppasswords`
3. In MOMBotPro, enter your Gmail address and the app password

---

## API Reference

All endpoints except register, login, and the Zoom OAuth callback require a valid JWT in the `Authorization: Bearer <token>` header.

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/register` | Register a new user |
| POST | `/api/auth/login` | Login and receive a JWT |
| GET | `/api/auth/me` | Get the current authenticated user |
| GET | `/api/pipeline` | List all pipelines for the current user |
| POST | `/api/pipeline/start` | Start a new pipeline run |
| GET | `/api/pipeline/{id}` | Get the status and result of a pipeline |
| GET | `/api/integrations` | Get all integrations for the current user |
| POST | `/api/integrations/connect` | Save or update an integration |
| DELETE | `/api/integrations/{type}` | Disconnect an integration |
| POST | `/api/zoom/join` | Send the bot to join a Zoom meeting |
| GET | `/api/zoom/session/{id}` | Poll the status of a Zoom bot session |
| GET | `/api/oauth/zoom/callback` | Zoom OAuth redirect handler |

---

## Database

Table structure is managed entirely by EF Core migrations. Tables should not be modified manually.

| Table | Purpose |
|---|---|
| Users | User accounts, subscription plan, and free trial count |
| Integrations | Per-user credentials for each connected service |
| Pipelines | Full state and output of every pipeline run, stored as JSON |
| ZoomSessions | Active bot sessions and their current status |

Stored procedures handle all User and Integration read/write operations. They are located in `SqlScripts/StoredProcedures/` and must be executed in SSMS after the EF migrations have created the tables.

---

## Security

The following files must never be committed to version control:

```
appsettings.json
appsettings.Development.json
appsettings.Production.json
.env
.env.*
```

These are excluded via `.gitignore`. Only `appsettings.Example.json`, which contains no real credentials, should exist in the repository.

If credentials are accidentally pushed to GitHub, rotate them immediately in each respective service and remove them from the repository history:

```bash
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch backend/MOMBotPro.API/appsettings.json" \
  --prune-empty --tag-name-filter cat -- --all

git push origin --force --all
```

---

Innoworks Software — OutCreate 2025
