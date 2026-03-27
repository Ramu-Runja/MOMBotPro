# ⚡ MOMBot Pro — Full Autonomous Pipeline

> Client speaks a bug in Zoom → Jira ticket → Code fix → GitHub PR. Zero human steps.

## 🚀 Setup

### 1. Add API Keys — `backend/MOMBotPro.API/appsettings.json`
```json
{
  "Anthropic": { "ApiKey": "sk-ant-YOUR_KEY" },
  "OpenAI":    { "ApiKey": "sk-YOUR_KEY" },
  "GitHub": {
    "Token": "ghp_YOUR_GITHUB_TOKEN",
    "Owner": "your-username",
    "Repo":  "your-repo"
  }
}
```

### 2. Run Backend
```bash
cd backend/MOMBotPro.API
dotnet run
# → http://localhost:5000
```

### 3. Run Frontend
```bash
cd frontend
npm install && npm run dev
# → http://localhost:3000
```

## 🔁 Full Pipeline Flow

```
🎙️  Zoom Tenglish recording
        ↓
📝  Claude extracts MOM + bug summary
        ↓
📋  Auto-creates Jira ticket (PROJ-XXXX)
        ↓
🔍  Scans GitHub codebase → finds file + line
        ↓
🤖  Claude generates the fix
        ↓
🚀  Creates branch + Raises PR on GitHub
        ↓
✅  Developer reviews and clicks Merge
```

## 🔑 GitHub Token Permissions
Generate at: github.com → Settings → Developer Settings → Personal Access Tokens
Required scopes: `repo`, `pull_requests`, `contents`

## 📡 API Endpoints
- `GET  /api/pipeline`         — list all pipelines
- `GET  /api/pipeline/{id}`    — get pipeline (for live polling)
- `POST /api/pipeline/start`   — start the full pipeline
