-- @no-transaction
-- 005-eval-run-queue-indexes — the indexes 004's queue and scopes read, built on the existing tables without blocking
-- their writes, and 004's NOT VALID checks and key validated without blocking them either.
--
--   - each statement runs on its own, outside a transaction (`@no-transaction`), as CREATE INDEX CONCURRENTLY requires
--   - each index is dropped first, then built: an interrupted run leaves an invalid index that CREATE INDEX IF NOT EXISTS
--     would keep, so a rerun drops it and builds it again; a finished index is rebuilt only when this file runs again
--   - VALIDATE CONSTRAINT scans under a SHARE UPDATE EXCLUSIVE lock, so writes go on; it is a no-op once valid
--
-- A worker claims the oldest queued run with FOR UPDATE SKIP LOCKED over ix_ai_eval_runs_queued, and fails a running run
-- whose heartbeat went quiet over ix_ai_eval_runs_running; ix_ai_eval_runs_batch lists one matrix's runs, and
-- ix_ai_inferences_scope one tenant's calls.

DROP INDEX CONCURRENTLY IF EXISTS {{schema}}.ix_ai_eval_runs_queued;
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_ai_eval_runs_queued ON {{schema}}.ai_eval_runs (started_at, id) WHERE status = 'queued';

DROP INDEX CONCURRENTLY IF EXISTS {{schema}}.ix_ai_eval_runs_running;
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_ai_eval_runs_running ON {{schema}}.ai_eval_runs (heartbeat_at) WHERE status = 'running';

DROP INDEX CONCURRENTLY IF EXISTS {{schema}}.ix_ai_eval_runs_batch;
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_ai_eval_runs_batch ON {{schema}}.ai_eval_runs (batch_id) WHERE batch_id IS NOT NULL;

DROP INDEX CONCURRENTLY IF EXISTS {{schema}}.ix_ai_eval_runs_scope;
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_ai_eval_runs_scope ON {{schema}}.ai_eval_runs (scope, started_at) WHERE scope <> '';

DROP INDEX CONCURRENTLY IF EXISTS {{schema}}.ix_ai_inferences_scope;
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_ai_inferences_scope ON {{schema}}.ai_inferences (scope, at) WHERE scope <> '';

ALTER TABLE {{schema}}.ai_eval_runs VALIDATE CONSTRAINT ck_ai_eval_runs_status;
ALTER TABLE {{schema}}.ai_eval_runs VALIDATE CONSTRAINT ck_ai_eval_runs_progress;
ALTER TABLE {{schema}}.ai_eval_runs VALIDATE CONSTRAINT fk_ai_eval_runs_batch;
