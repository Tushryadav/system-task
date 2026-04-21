-- ═══════════════════════════════════════════════════════════════
--  SRAAS — Supabase Schema Creation Script
--  Run this directly in Supabase SQL Editor
--  Version: 1.0 | Stack: PostgreSQL (Supabase)
-- ═══════════════════════════════════════════════════════════════

-- ─── ENUMS ───────────────────────────────────────────────────

DO $$ BEGIN
  CREATE TYPE app_type_enum AS ENUM ('chat', 'announcements', 'tasks', 'custom');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE member_role_enum AS ENUM ('admin', 'manager', 'member');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE member_status_enum AS ENUM ('active', 'suspended', 'left');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE invite_type_enum AS ENUM ('single', 'multi');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE channel_type_enum AS ENUM ('general', 'direct', 'group', 'announcement');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE content_type_enum AS ENUM ('text', 'image', 'file', 'system', 'deleted');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ═══════════════════════════════════════════════════════════════
--  1. ORGANIZATIONS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS organizations (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name         TEXT NOT NULL,
  slug         TEXT UNIQUE NOT NULL,
  seat_limit   INTEGER NOT NULL DEFAULT 10,
  seats_used   INTEGER NOT NULL DEFAULT 0,
  settings     JSONB DEFAULT '{}',
  created_at   TIMESTAMPTZ DEFAULT now(),
  updated_at   TIMESTAMPTZ DEFAULT now()
);


-- ═══════════════════════════════════════════════════════════════
--  2. APPS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS apps (
  id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  org_id     UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
  name       TEXT NOT NULL,
  app_type   app_type_enum NOT NULL DEFAULT 'chat',
  config     JSONB DEFAULT '{}',
  is_active  BOOLEAN DEFAULT true,
  created_at TIMESTAMPTZ DEFAULT now()
);


-- ═══════════════════════════════════════════════════════════════
--  3. ORG INVITES (must come before org_members for FK)
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS org_invites (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  org_id      UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
  created_by  UUID,  -- FK added after org_members exists
  invite_code TEXT UNIQUE NOT NULL,
  invite_type invite_type_enum DEFAULT 'multi',
  max_uses    INTEGER NOT NULL DEFAULT 1,
  used_count  INTEGER NOT NULL DEFAULT 0,
  expires_at  TIMESTAMPTZ NOT NULL,
  is_active   BOOLEAN DEFAULT true,
  created_at  TIMESTAMPTZ DEFAULT now()
);


-- ═══════════════════════════════════════════════════════════════
--  4. ORG MEMBERS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS org_members (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  org_id        UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
  invite_id     UUID REFERENCES org_invites(id),
  name          TEXT NOT NULL,
  email         TEXT NOT NULL,
  password_hash TEXT NOT NULL,
  role          member_role_enum NOT NULL DEFAULT 'member',
  status        member_status_enum NOT NULL DEFAULT 'active',
  is_active     BOOLEAN DEFAULT true,
  joined_at     TIMESTAMPTZ DEFAULT now(),
  deleted_at    TIMESTAMPTZ,

  UNIQUE(org_id, email)
);

-- Now add the FK from org_invites.created_by → org_members.id
ALTER TABLE org_invites
  ADD CONSTRAINT fk_org_invites_created_by
  FOREIGN KEY (created_by) REFERENCES org_members(id);


-- ═══════════════════════════════════════════════════════════════
--  5. REFRESH TOKENS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS refresh_tokens (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  member_id    UUID NOT NULL REFERENCES org_members(id) ON DELETE CASCADE,
  org_id       UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
  token_hash   TEXT NOT NULL,
  device_info  JSONB DEFAULT '{}',
  expires_at   TIMESTAMPTZ NOT NULL,
  is_revoked   BOOLEAN DEFAULT false,
  created_at   TIMESTAMPTZ DEFAULT now(),
  last_used_at TIMESTAMPTZ DEFAULT now()
);


-- ═══════════════════════════════════════════════════════════════
--  6. CHANNELS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS channels (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  app_id       UUID NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
  org_id       UUID NOT NULL REFERENCES organizations(id),
  name         TEXT,
  channel_type channel_type_enum NOT NULL DEFAULT 'general',
  is_private   BOOLEAN DEFAULT false,
  created_by   UUID REFERENCES org_members(id),
  created_at   TIMESTAMPTZ DEFAULT now()
);


-- ═══════════════════════════════════════════════════════════════
--  7. CHANNEL MEMBERS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS channel_members (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  channel_id    UUID NOT NULL REFERENCES channels(id) ON DELETE CASCADE,
  org_member_id UUID NOT NULL REFERENCES org_members(id) ON DELETE CASCADE,
  last_read_at  TIMESTAMPTZ DEFAULT now(),
  joined_at     TIMESTAMPTZ DEFAULT now(),

  UNIQUE(channel_id, org_member_id)
);


-- ═══════════════════════════════════════════════════════════════
--  8. APP MEMBERS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS app_members (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  app_id        UUID NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
  org_member_id UUID NOT NULL REFERENCES org_members(id) ON DELETE CASCADE,
  role          TEXT DEFAULT 'member',
  joined_at     TIMESTAMPTZ DEFAULT now(),

  UNIQUE(app_id, org_member_id)
);


-- ═══════════════════════════════════════════════════════════════
--  9. MESSAGES
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS messages (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  channel_id   UUID NOT NULL REFERENCES channels(id) ON DELETE CASCADE,
  org_id       UUID NOT NULL REFERENCES organizations(id),
  sender_id    UUID REFERENCES org_members(id) ON DELETE SET NULL,
  reply_to_id  UUID REFERENCES messages(id) ON DELETE SET NULL,
  content      TEXT,
  content_type content_type_enum NOT NULL DEFAULT 'text',
  metadata     JSONB DEFAULT '{}',
  is_edited    BOOLEAN DEFAULT false,
  is_deleted   BOOLEAN DEFAULT false,
  deleted_at   TIMESTAMPTZ,
  created_at   TIMESTAMPTZ DEFAULT now(),
  updated_at   TIMESTAMPTZ DEFAULT now()
);


-- ═══════════════════════════════════════════════════════════════
--  10. MESSAGE ATTACHMENTS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS message_attachments (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  message_id   UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
  org_id       UUID NOT NULL REFERENCES organizations(id),
  file_name    TEXT NOT NULL,
  file_type    TEXT NOT NULL,
  file_size_kb INTEGER,
  storage_key  TEXT NOT NULL,
  created_at   TIMESTAMPTZ DEFAULT now()
);


-- ═══════════════════════════════════════════════════════════════
--  11. MESSAGE REACTIONS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS message_reactions (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  message_id    UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
  org_member_id UUID NOT NULL REFERENCES org_members(id) ON DELETE CASCADE,
  emoji         TEXT NOT NULL,
  created_at    TIMESTAMPTZ DEFAULT now(),

  UNIQUE(message_id, org_member_id, emoji)
);


-- ═══════════════════════════════════════════════════════════════
--  12. AUDIT LOGS
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS audit_logs (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  org_id      UUID NOT NULL REFERENCES organizations(id),
  actor_id    UUID REFERENCES org_members(id) ON DELETE SET NULL,
  action      TEXT NOT NULL,
  target_type TEXT,
  target_id   UUID,
  metadata    JSONB DEFAULT '{}',
  created_at  TIMESTAMPTZ DEFAULT now()
);


-- ═══════════════════════════════════════════════════════════════
--  INDEXES (Performance)
-- ═══════════════════════════════════════════════════════════════

-- Messages: latest messages in a channel (most common query)
CREATE INDEX IF NOT EXISTS idx_messages_channel_created
  ON messages(channel_id, created_at DESC)
  WHERE is_deleted = false;

-- Messages: by org (RLS base filter)
CREATE INDEX IF NOT EXISTS idx_messages_org
  ON messages(org_id);

-- Channels: by app
CREATE INDEX IF NOT EXISTS idx_channels_app
  ON channels(app_id);

-- Members: by org + active status
CREATE INDEX IF NOT EXISTS idx_org_members_org_active
  ON org_members(org_id, is_active);

-- Invites: fast lookup by code
CREATE INDEX IF NOT EXISTS idx_invites_code
  ON org_invites(invite_code)
  WHERE is_active = true;

-- Refresh tokens: lookup by hash
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_hash
  ON refresh_tokens(token_hash)
  WHERE is_revoked = false;

-- Audit logs: org + time (admin dashboard)
CREATE INDEX IF NOT EXISTS idx_audit_logs_org_created
  ON audit_logs(org_id, created_at DESC);

-- Reactions: per message
CREATE INDEX IF NOT EXISTS idx_reactions_message
  ON message_reactions(message_id);


-- ═══════════════════════════════════════════════════════════════
--  DONE — Schema created successfully
-- ═══════════════════════════════════════════════════════════════
