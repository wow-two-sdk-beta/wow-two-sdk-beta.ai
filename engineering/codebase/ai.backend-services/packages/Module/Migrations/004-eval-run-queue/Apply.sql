-- 004-eval-run-queue — eval runs become a queue a worker drains, and every queued comparison repeats exactly; calls,
-- sets and runs gain a data scope (ai-layer-analysis #15).
--
--   - a batch pins what every cell of one matrix shares: the sampled items in run order, each with the expected answer
--     and the input hash it was queued with, and the set's scoring rules; a later relabel or rule change reaches later
--     batches only
--   - a run pins its cell's resolved prompt: the row, its version, text, parameters and the use case's output schema; a
--     later rewrite or label move never changes what a queued run sends
--   - a run keeps its status, progress, heartbeat and error, the batch it was queued in, who asked for it and how it
--     sampled; a score keeps the expected answer it was scored against, which the report view now shows
--   - scope: the data scope (`tenant:{id}`, '' for the unscoped bucket) of each inference, set and run
--
-- Expand only: every column is new, nullable or with a constant default (no table rewrite), and the runs' checks and key
-- are added NOT VALID; 005 validates them and builds the indexes on the existing tables concurrently, outside this
-- transaction. Runs made before the queue read as succeeded, with their scored items as their total and done counts.

ALTER TABLE {{schema}}.ai_inferences
    ADD COLUMN IF NOT EXISTS scope varchar(128) NOT NULL DEFAULT '';

ALTER TABLE {{schema}}.ai_eval_sets
    ADD COLUMN IF NOT EXISTS scope varchar(128) NOT NULL DEFAULT '';

-- What one matrix's cells share, pinned when it was queued.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_eval_batches (
    id            uuid         NOT NULL,
    set_key       varchar(64)  NOT NULL,
    scope         varchar(128) NOT NULL,
    score_fields  text[]       NOT NULL,
    severe_misses text[]       NOT NULL,
    created_at    timestamptz  NOT NULL,
    CONSTRAINT pk_ai_eval_batches PRIMARY KEY (id),
    CONSTRAINT fk_ai_eval_batches_set FOREIGN KEY (set_key) REFERENCES {{schema}}.ai_eval_sets (key)
);

CREATE INDEX IF NOT EXISTS ix_ai_eval_batches_set ON {{schema}}.ai_eval_batches (set_key, created_at);

-- The items a batch takes, in run order, each with the expected answer and the input hash it was pinned with.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_eval_batch_items (
    batch_id   uuid        NOT NULL,
    item_id    uuid        NOT NULL,
    position   integer     NOT NULL,
    expected   jsonb       NOT NULL,
    input_hash varchar(64) NOT NULL,
    CONSTRAINT pk_ai_eval_batch_items PRIMARY KEY (batch_id, item_id),
    CONSTRAINT fk_ai_eval_batch_items_batch FOREIGN KEY (batch_id) REFERENCES {{schema}}.ai_eval_batches (id),
    CONSTRAINT fk_ai_eval_batch_items_item FOREIGN KEY (item_id) REFERENCES {{schema}}.ai_eval_items (id),
    CONSTRAINT ck_ai_eval_batch_items_position CHECK (position >= 0)
);

CREATE INDEX IF NOT EXISTS ix_ai_eval_batch_items_item ON {{schema}}.ai_eval_batch_items (item_id);

ALTER TABLE {{schema}}.ai_eval_runs
    ADD COLUMN IF NOT EXISTS status             varchar(16)  NOT NULL DEFAULT 'succeeded',
    ADD COLUMN IF NOT EXISTS items_total        integer      NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS items_done         integer      NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS error              text         NULL,
    ADD COLUMN IF NOT EXISTS batch_id           uuid         NULL,
    ADD COLUMN IF NOT EXISTS requested_by       varchar(256) NULL,
    ADD COLUMN IF NOT EXISTS heartbeat_at       timestamptz  NULL,
    ADD COLUMN IF NOT EXISTS fresh              boolean      NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS parallelism        integer      NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS sample_count       integer      NULL,
    ADD COLUMN IF NOT EXISTS sample_seed        integer      NULL,
    ADD COLUMN IF NOT EXISTS sample_tags        text[]       NOT NULL DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS sample_item_ids    uuid[]       NOT NULL DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS scope              varchar(128) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS prompt_id          uuid         NULL,
    ADD COLUMN IF NOT EXISTS prompt_model_match varchar(128) NULL,
    ADD COLUMN IF NOT EXISTS prompt_system      text         NULL,
    ADD COLUMN IF NOT EXISTS prompt_user        text         NULL,
    ADD COLUMN IF NOT EXISTS prompt_parameters  jsonb        NULL,
    ADD COLUMN IF NOT EXISTS output_schema      jsonb        NULL;

-- Added NOT VALID, so the existing rows are not scanned under this transaction's lock; 005 validates them.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_ai_eval_runs_status'
                   AND conrelid = '{{schema}}.ai_eval_runs'::regclass) THEN
        ALTER TABLE {{schema}}.ai_eval_runs ADD CONSTRAINT ck_ai_eval_runs_status
            CHECK (status IN ('queued', 'running', 'succeeded', 'failed', 'canceled')) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_ai_eval_runs_progress'
                   AND conrelid = '{{schema}}.ai_eval_runs'::regclass) THEN
        ALTER TABLE {{schema}}.ai_eval_runs ADD CONSTRAINT ck_ai_eval_runs_progress
            CHECK (items_total >= 0 AND items_done >= 0 AND parallelism > 0 AND (sample_count IS NULL OR sample_count > 0)) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ai_eval_runs_batch'
                   AND conrelid = '{{schema}}.ai_eval_runs'::regclass) THEN
        ALTER TABLE {{schema}}.ai_eval_runs ADD CONSTRAINT fk_ai_eval_runs_batch
            FOREIGN KEY (batch_id) REFERENCES {{schema}}.ai_eval_batches (id) NOT VALID;
    END IF;
END
$$;

ALTER TABLE {{schema}}.ai_eval_scores
    ADD COLUMN IF NOT EXISTS expected jsonb NULL;

-- The runs made before the queue: every scored item counts as taken and done.
UPDATE {{schema}}.ai_eval_runs r
SET items_total = s.items, items_done = s.items
FROM (SELECT run_id, count(*)::integer AS items FROM {{schema}}.ai_eval_scores GROUP BY run_id) s
WHERE r.id = s.run_id AND r.batch_id IS NULL AND r.status = 'succeeded' AND r.items_total = 0;

-- The combined report: as in 002, but an item reads the expected answer its run pinned; a run made before pinning
-- reads the item's current one.
CREATE OR REPLACE VIEW {{schema}}.ai_eval_report_rows AS
SELECT
    r.set_key,
    r.id                                AS run_id,
    r.use_case,
    r.prompt_version,
    r.prompt_hash,
    r.provider,
    r.model,
    r.started_at,
    r.finished_at,
    r.note                              AS run_note,
    s.item_id,
    i.subject,
    i.tags,
    coalesce(s.expected, i.expected)    AS expected,
    i.created_at                        AS item_created_at,
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
    coalesce(f.input_tokens, 0)         AS input_tokens,
    coalesce(f.output_tokens, 0)        AS output_tokens,
    coalesce(f.cache_read_tokens, 0)    AS cache_read_tokens,
    coalesce(f.priced, false)           AS priced
FROM {{schema}}.ai_eval_scores s
JOIN {{schema}}.ai_eval_runs r ON r.id = s.run_id
JOIN {{schema}}.ai_eval_items i ON i.id = s.item_id
LEFT JOIN {{schema}}.ai_inferences f ON f.id = s.inference_id;
