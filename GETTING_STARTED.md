# SRAAS API — Getting Started Guide

> Complete end-to-end guide: from first run to making your first API call.

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0+ |
| PostgreSQL (Supabase) | Account + project ready |

---

## Step 1 — Set Up the Database

1. Open your **Supabase project** → **SQL Editor**
2. Copy the entire contents of [`SupabaseSchema.sql`](./SupabaseSchema.sql)
3. Paste it into the editor and click **Run**
4. You should see all 12 tables created with ✅ no errors

---

## Step 2 — Configure Secrets (Never in source code)

Run these commands in your terminal from the project root (`d:\system-task`):

### Set your Supabase connection string
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=aws-1-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.aqgtqesaisqhcptfygqd;Password=YOUR_ACTUAL_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```
> Replace `YOUR_ACTUAL_PASSWORD` with your real Supabase project password.

### Set the JWT signing secret (use a strong random string ≥ 32 chars)
```powershell
dotnet user-secrets set "Jwt:Secret" "YourStrongSecretKeyHere-MinimumOf32Characters!!"
```

---

## Step 3 — Run the API

```powershell
dotnet run
```

The API will start on:
- **HTTP**: `http://localhost:5000`
- **HTTPS**: `https://localhost:5001`

---

## Step 4 — Open the Scalar API Explorer

Once the API is running, open in your browser:

```
http://localhost:5000/scalar/v1
```

You will see the **SRAAS API Explorer** — a dark, interactive UI where you can:
- Browse all 21 endpoints grouped by controller
- Read request/response schemas
- Make live API calls with your auth token
- See example request bodies

The raw OpenAPI JSON is also available at:
```
http://localhost:5000/openapi/v1.json
```

---

## Step 5 — Your First API Flow

Follow this exact order for a complete working flow:

### 5.1 — Seed an organisation (direct DB insert, one-time setup)

Since there's no self-registration, you'll need to manually insert the first admin via Supabase SQL Editor or psql:

```sql
-- 1. Insert your organisation
INSERT INTO organizations (name, slug, seat_limit)
VALUES ('My Company', 'my-company', 50);

-- 2. Insert the first admin member (password: 'Admin@1234' — change after login!)
-- NOTE: You must set a proper Argon2id hash. 
-- Run the app, call POST /api/invites/join with the bootstrap invite below.
```

**Easier approach — use the bootstrap flow:**

```sql
-- Insert org
INSERT INTO organizations (id, name, slug, seat_limit)
VALUES ('00000000-0000-0000-0000-000000000001', 'SRAAS Demo', 'sraas-demo', 100);

-- Insert a permanent invite for first admin setup
INSERT INTO org_invites (org_id, invite_code, invite_type, max_uses, expires_at)
VALUES (
  '00000000-0000-0000-0000-000000000001',
  'ADMIN001',
  'single',
  1,
  now() + interval '7 days'
);
```

### 5.2 — Join the org (creates the first member)

**Endpoint:** `POST /api/invites/join`

```json
{
  "inviteCode": "ADMIN001",
  "orgSlug":    "sraas-demo",
  "name":       "Admin User",
  "email":      "admin@company.com",
  "password":   "Admin@SecurePassword123!"
}
```

Then **manually update the role** to admin in Supabase:
```sql
UPDATE org_members SET role = 'admin' WHERE email = 'admin@company.com';
```

---

### 5.3 — Login and get your token

**Endpoint:** `POST /api/auth/login`

```json
{
  "email":    "admin@company.com",
  "password": "Admin@SecurePassword123!",
  "orgSlug":  "sraas-demo"
}
```

**Response:**
```json
{
  "accessToken":  "eyJhbG...",   // Valid 15 minutes
  "refreshToken": "base64..."    // Valid 30 days
}
```

Copy the `accessToken`.

---

### 5.4 — Authenticate in Scalar

1. In Scalar UI (`/scalar/v1`) → click **Authorize** (🔒 icon)
2. Paste your `accessToken`
3. All subsequent requests will include `Authorization: Bearer <token>` automatically

---

### 5.5 — Create an App

**Endpoint:** `POST /api/apps` *(coming soon — seed directly for now)*

```sql
INSERT INTO apps (org_id, name, app_type)
VALUES ('00000000-0000-0000-0000-000000000001', 'Team Chat', 'chat');
```

---

### 5.6 — Create an Invite for team members (as Admin)

**Endpoint:** `POST /api/invites/create`

```json
{
  "maxUses":    10,
  "expiryDays": 7,
  "inviteType": "multi"
}
```

**Response:**
```json
{
  "inviteCode": "X7KP2MQA",
  "inviteUrl":  "https://app.sraas.com/join/sraas-demo/X7KP2MQA",
  "maxUses":    10,
  "expiresAt":  "2026-04-28T..."
}
```

Share the `inviteUrl` with your team.

---

### 5.7 — Create a Channel

**Endpoint:** `POST /api/channels`

```json
{
  "appId":       "<your-app-uuid>",
  "name":        "general",
  "channelType": "general",
  "isPrivate":   false
}
```

---

### 5.8 — Send a Message

**Endpoint:** `POST /api/messages?channelId=<channel-uuid>`

```json
{
  "content":     "Hello, team! 👋",
  "contentType": "text"
}
```

---

### 5.9 — Read Messages (paginated)

**Endpoint:** `GET /api/channels/{channelId}/messages?limit=50`

For older messages (cursor pagination):
```
GET /api/channels/{channelId}/messages?before={messageId}&limit=50
```

---

### 5.10 — Refresh your token before it expires

**Endpoint:** `POST /api/auth/refresh`

```json
{
  "refreshToken": "your-refresh-token-here"
}
```

**Response:** new `accessToken` + new `refreshToken` (old one is deleted).

---

## API Reference — Full Endpoint Table

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/login` | Public | Login → get access + refresh token |
| `POST` | `/api/auth/refresh` | Public | Rotate refresh token |
| `POST` | `/api/auth/logout` | Bearer | Revoke all sessions |
| `GET` | `/api/orgs/me` | Bearer | Current org info |
| `PUT` | `/api/orgs/seat-limit` | Admin | Update seat limit |
| `POST` | `/api/invites/create` | Admin | Generate invite link |
| `GET` | `/api/invites` | Admin | List all invites |
| `DELETE` | `/api/invites/{id}` | Admin | Deactivate invite |
| `POST` | `/api/invites/join` | Public | Join org via invite code |
| `GET` | `/api/members` | Bearer | List all org members |
| `DELETE` | `/api/members/{id}` | Admin | Remove member (soft-delete) |
| `GET` | `/api/apps` | Bearer | List apps in the org |
| `GET` | `/api/apps/{id}/channels` | Bearer | List channels in an app |
| `POST` | `/api/channels` | Bearer | Create a channel |
| `GET` | `/api/channels/{id}/messages` | Bearer | Get messages (cursor paginated) |
| `POST` | `/api/messages` | Bearer | Send a message |
| `DELETE` | `/api/messages/{id}` | Bearer | Soft-delete a message |
| `POST` | `/api/messages/{id}/reactions` | Bearer | Add emoji reaction |
| `POST` | `/api/files/upload` | Bearer | Upload file attachment |
| `GET` | `/api/files/{id}/url` | Bearer | Get signed download URL (15 min) |
| `GET` | `/api/audit-logs` | Admin | View audit history |

---

## Rate Limits

| Endpoint | Limit |
|---|---|
| `POST /api/auth/login` | 10 requests / 5 minutes |
| `POST /api/invites/join` | 5 requests / 10 minutes |
| `POST /api/messages` | 60 requests / 1 minute |
| `POST /api/files/upload` | 10 requests / 1 minute |

---

## Production Deployment

Do **not** use User Secrets in production. Use environment variables instead:

```bash
# Linux / Docker
export ConnectionStrings__DefaultConnection="Host=...;Password=REAL_PASSWORD;..."
export Jwt__Secret="your-strong-secret"

# Windows
$env:ConnectionStrings__DefaultConnection="..."
$env:Jwt__Secret="..."
```

Then run:
```bash
dotnet run --environment Production
```

> In production, Scalar UI is disabled. Only the API endpoints are active.

---

## Security Notes

| Feature | Detail |
|---|---|
| Passwords | Argon2id hashed — never stored plain |
| JWT tokens | 15-minute expiry, HMAC-SHA256 |
| Refresh tokens | SHA-256 hashed in DB, rotated on every use |
| File uploads | Magic-byte MIME detection, 10 MB max |
| Org isolation | Every query is scoped to `org_id` from the JWT |
| Audit trail | All admin actions are logged to `audit_logs` (append-only) |
