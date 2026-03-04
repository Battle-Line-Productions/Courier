-- 0013_job_chains.sql
-- Job chains, chain members, chain executions, standalone job dependencies

-- Job chains
CREATE TABLE job_chains (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT NOT NULL,
    description TEXT,
    is_enabled  BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted  BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at  TIMESTAMPTZ
);
CREATE INDEX ix_job_chains_name ON job_chains (name) WHERE NOT is_deleted;

-- Chain members
CREATE TABLE job_chain_members (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chain_id                 UUID NOT NULL,
    job_id                   UUID NOT NULL,
    execution_order          INT NOT NULL,
    depends_on_member_id     UUID,
    run_on_upstream_failure  BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT fk_chain_members_chains  FOREIGN KEY (chain_id) REFERENCES job_chains(id) ON DELETE CASCADE,
    CONSTRAINT fk_chain_members_jobs    FOREIGN KEY (job_id) REFERENCES jobs(id) ON DELETE RESTRICT,
    CONSTRAINT fk_chain_members_depends FOREIGN KEY (depends_on_member_id) REFERENCES job_chain_members(id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX ix_chain_members_chain_order ON job_chain_members (chain_id, execution_order);
CREATE INDEX ix_chain_members_job ON job_chain_members (job_id);

-- Chain executions
CREATE TABLE chain_executions (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chain_id     UUID NOT NULL,
    triggered_by TEXT NOT NULL,
    state        TEXT NOT NULL DEFAULT 'pending',
    started_at   TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_chain_executions_chains FOREIGN KEY (chain_id) REFERENCES job_chains(id) ON DELETE CASCADE,
    CONSTRAINT ck_chain_executions_state CHECK (state IN ('pending','running','completed','failed','cancelled'))
);
CREATE INDEX ix_chain_executions_chain ON chain_executions (chain_id, created_at DESC);
CREATE INDEX ix_chain_executions_state ON chain_executions (state);

-- Standalone job dependencies
CREATE TABLE job_dependencies (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    upstream_job_id   UUID NOT NULL,
    downstream_job_id UUID NOT NULL,
    run_on_failure    BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT fk_job_deps_upstream   FOREIGN KEY (upstream_job_id)   REFERENCES jobs(id) ON DELETE CASCADE,
    CONSTRAINT fk_job_deps_downstream FOREIGN KEY (downstream_job_id) REFERENCES jobs(id) ON DELETE CASCADE,
    CONSTRAINT ck_job_deps_no_self_ref CHECK (upstream_job_id != downstream_job_id)
);
CREATE UNIQUE INDEX ix_job_deps_pair ON job_dependencies (upstream_job_id, downstream_job_id);
CREATE INDEX ix_job_deps_downstream ON job_dependencies (downstream_job_id);

-- Link job_executions to chain_executions
ALTER TABLE job_executions ADD COLUMN chain_execution_id UUID;
ALTER TABLE job_executions ADD CONSTRAINT fk_job_executions_chain
    FOREIGN KEY (chain_execution_id) REFERENCES chain_executions(id) ON DELETE SET NULL;
CREATE INDEX ix_job_executions_chain ON job_executions (chain_execution_id, created_at DESC)
    WHERE chain_execution_id IS NOT NULL;
