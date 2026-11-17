-- Inverts 005-eval-run-queue-indexes — drops its indexes. A rollback runs in a transaction, so the drops are plain, not
-- concurrent: dev/test recovery only. The validated checks and key stay; 004's rollback drops them.

DROP INDEX IF EXISTS {{schema}}.ix_ai_inferences_scope;
DROP INDEX IF EXISTS {{schema}}.ix_ai_eval_runs_scope;
DROP INDEX IF EXISTS {{schema}}.ix_ai_eval_runs_batch;
DROP INDEX IF EXISTS {{schema}}.ix_ai_eval_runs_running;
DROP INDEX IF EXISTS {{schema}}.ix_ai_eval_runs_queued;
