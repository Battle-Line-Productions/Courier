CREATE TABLE tags (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            TEXT NOT NULL,
    color           TEXT,
    category        TEXT,
    description     TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMPTZ
);
CREATE UNIQUE INDEX ix_tags_name ON tags (LOWER(name)) WHERE NOT is_deleted;
CREATE INDEX ix_tags_category ON tags (category) WHERE NOT is_deleted AND category IS NOT NULL;

CREATE TABLE entity_tags (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tag_id          UUID NOT NULL,
    entity_type     TEXT NOT NULL,
    entity_id       UUID NOT NULL,
    CONSTRAINT fk_entity_tags_tags FOREIGN KEY (tag_id) REFERENCES tags (id) ON DELETE CASCADE,
    CONSTRAINT ck_entity_tags_type CHECK (
        entity_type IN ('job','job_chain','connection','pgp_key','ssh_key','file_monitor')
    )
);
CREATE UNIQUE INDEX ix_entity_tags_unique ON entity_tags (tag_id, entity_type, entity_id);
CREATE INDEX ix_entity_tags_entity ON entity_tags (entity_type, entity_id);
