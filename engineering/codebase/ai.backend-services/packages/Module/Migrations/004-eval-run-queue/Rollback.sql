-- Inverts 004-eval-run-queue — restores 002's report view, then drops the batches, the runs' queue and pin columns,
-- the scores' pinned expected answers and the scopes. Dev/test recovery only: every run's status, progress, batch, pin
-- and scope, and every inference's and set's scope, are lost; queued runs read as finished runs with no scores.

CREATE OR REPLACE VIEW {{schema}}.ai_eval_report_rows AS
SELECT
    r.set_key,
    r.id                               AS run_id,
    r.use_case,
    r.prompt_version,
    r.prompt_hash,
    r.provider,
    r.model,
    r.started_at,
    r.finished_at,
    r.note                             AS run_note,
    s.item_id,
    i.subject,
    i.tags,
    i.expected,
    i.created_at                       AS item_created_at,
    s.inference_id,
    s.outcome,
    s.correct,
    s.severe,
    s.misses,
    s.answer,
    s.cost_micro_usd,
    s.latency_ms,
    s.from_cache,
    s.rating,
    s.comment,
    s.rated_by,
    coalesce(f.input_tokens, 0)        AS input_tokens,
    coalesce(f.output_tokens, 0)       AS output_tokens,
    coalesce(f.cache_read_tokens, 0)   AS cache_read_tokens,
    coalesce(f.priced, false)          AS priced
FROM {{schema}}.ai_eval_scores s
JOIN {{schema}}.ai_eval_runs r ON r.id = s.run_id
JOIN {{schema}}.ai_eval_items i ON i.id = s.item_id
LEFT JOIN {{schema}}.ai_inferences f ON f.id = s.inference_id;

ALTER TABLE {{schema}}.ai_eval_scores
    DROP COLUMN IF EXISTS expected;

ALTER TABLE {{schema}}.ai_eval_runs
    DROP CONSTRAINT IF EXISTS fk_ai_eval_runs_batch,
    DROP CONSTRAINT IF EXISTS ck_ai_eval_runs_progress,
    DROP CONSTRAINT IF EXISTS ck_ai_eval_runs_status,
    DROP COLUMN IF EXISTS output_schema,
    DROP COLUMN IF EXISTS prompt_parameters,
    DROP COLUMN IF EXISTS prompt_user,
    DROP COLUMN IF EXISTS prompt_system,
    DROP COLUMN IF EXISTS prompt_model_match,
    DROP COLUMN IF EXISTS prompt_id,
    DROP COLUMN IF EXISTS scope,
    DROP COLUMN IF EXISTS sample_item_ids,
    DROP COLUMN IF EXISTS sample_tags,
    DROP COLUMN IF EXISTS sample_seed,
    DROP COLUMN IF EXISTS sample_count,
    DROP COLUMN IF EXISTS parallelism,
    DROP COLUMN IF EXISTS fresh,
    DROP COLUMN IF EXISTS heartbeat_at,
    DROP COLUMN IF EXISTS requested_by,
    DROP COLUMN IF EXISTS batch_id,
    DROP COLUMN IF EXISTS error,
    DROP COLUMN IF EXISTS items_done,
    DROP COLUMN IF EXISTS items_total,
    DROP COLUMN IF EXISTS status;

DROP TABLE IF EXISTS {{schema}}.ai_eval_batch_items;
DROP TABLE IF EXISTS {{schema}}.ai_eval_batches;

ALTER TABLE {{schema}}.ai_eval_sets
    DROP COLUMN IF EXISTS scope;

ALTER TABLE {{schema}}.ai_inferences
    DROP COLUMN IF EXISTS scope;
