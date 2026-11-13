-- 002-ai-layer — in the module's schema ({{schema}}, which the migrator substitutes): the prompt registry (use cases,
-- prompts in both modes, labels and their moves), model prices, the inference log and evals (sets, items, runs, scores),
-- with the report view one query reads (ai-layer-analysis #7-#10).
--
-- Enumerations are text with check constraints whose labels are the contract's members in snake_case. Costs are integer
-- micro-USD; prices are micro-USD per 1M tokens. Structured values (output schema, prompt parameters, eval inputs,
-- expected and scored answers) are jsonb.

-- One row per use case: how it keeps prompts, the answer's schema, and the models it tries in order.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_use_cases (
    key           varchar(64)  NOT NULL,
    description   text         NULL,
    prompt_mode   varchar(16)  NOT NULL,
    output_schema jsonb        NULL,
    route         text[]       NOT NULL,
    keep_outputs  boolean      NOT NULL,
    updated_at    timestamptz  NOT NULL,
    CONSTRAINT pk_ai_use_cases PRIMARY KEY (key),
    CONSTRAINT ck_ai_use_cases_prompt_mode CHECK (prompt_mode IN ('rewrite', 'versioned'))
);

-- Prompt texts for both modes: a rewrite updates its row in place, a versioned save inserts the next version.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_prompts (
    id              uuid          NOT NULL,
    use_case        varchar(64)   NOT NULL,
    model_match     varchar(128)  NULL,
    version         integer       NOT NULL,
    system_template text          NOT NULL,
    user_template   text          NOT NULL,
    parameters      jsonb         NOT NULL,
    content_hash    varchar(12)   NOT NULL,
    note            text          NULL,
    created_by      varchar(256)  NULL,
    created_at      timestamptz   NOT NULL,
    updated_at      timestamptz   NOT NULL,
    CONSTRAINT pk_ai_prompts PRIMARY KEY (id),
    CONSTRAINT fk_ai_prompts_use_case FOREIGN KEY (use_case) REFERENCES {{schema}}.ai_use_cases (key),
    CONSTRAINT ck_ai_prompts_version CHECK (version > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ai_prompts_version ON {{schema}}.ai_prompts (use_case, (coalesce(model_match, '')), version);

-- Movable pointers of versioned use cases (production, candidate, …), and every move they made.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_prompt_labels (
    use_case varchar(64)  NOT NULL,
    label    varchar(64)  NOT NULL,
    version  integer      NOT NULL,
    moved_at timestamptz  NOT NULL,
    moved_by varchar(256) NULL,
    CONSTRAINT pk_ai_prompt_labels PRIMARY KEY (use_case, label),
    CONSTRAINT fk_ai_prompt_labels_use_case FOREIGN KEY (use_case) REFERENCES {{schema}}.ai_use_cases (key)
);

CREATE TABLE IF NOT EXISTS {{schema}}.ai_prompt_label_moves (
    id           uuid         NOT NULL,
    use_case     varchar(64)  NOT NULL,
    label        varchar(64)  NOT NULL,
    from_version integer      NULL,
    version      integer      NOT NULL,
    moved_at     timestamptz  NOT NULL,
    moved_by     varchar(256) NULL,
    CONSTRAINT pk_ai_prompt_label_moves PRIMARY KEY (id),
    CONSTRAINT fk_ai_prompt_label_moves_use_case FOREIGN KEY (use_case) REFERENCES {{schema}}.ai_use_cases (key)
);

CREATE INDEX IF NOT EXISTS ix_ai_prompt_label_moves_label ON {{schema}}.ai_prompt_label_moves (use_case, label, moved_at);

-- Model prices with history: the row in effect at an attempt's start prices it, and a newer row never rewrites the past.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_model_prices (
    provider                       varchar(64)  NOT NULL,
    model                          varchar(128) NOT NULL,
    effective_from                 timestamptz  NOT NULL,
    input_per_mtok_micro_usd       bigint       NOT NULL,
    output_per_mtok_micro_usd      bigint       NOT NULL,
    cache_write_per_mtok_micro_usd bigint       NULL,
    cache_read_per_mtok_micro_usd  bigint       NULL,
    source                         text         NULL,
    CONSTRAINT pk_ai_model_prices PRIMARY KEY (provider, model, effective_from),
    CONSTRAINT ck_ai_model_prices_positive CHECK (
        input_per_mtok_micro_usd >= 0 AND output_per_mtok_micro_usd >= 0
        AND coalesce(cache_write_per_mtok_micro_usd, 0) >= 0 AND coalesce(cache_read_per_mtok_micro_usd, 0) >= 0)
);

-- Eval sets: frozen inputs of one use case with their expected answers, and how to score them.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_eval_sets (
    key           varchar(64) NOT NULL,
    use_case      varchar(64) NOT NULL,
    score_fields  text[]      NOT NULL,
    severe_misses text[]      NOT NULL,
    note          text        NULL,
    created_at    timestamptz NOT NULL,
    CONSTRAINT pk_ai_eval_sets PRIMARY KEY (key),
    CONSTRAINT fk_ai_eval_sets_use_case FOREIGN KEY (use_case) REFERENCES {{schema}}.ai_use_cases (key)
);

-- One item: its prompt variables, frozen when added, and the expected answer. A removed item keeps its row
-- (removed_at), so the runs that scored it still report it.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_eval_items (
    id          uuid         NOT NULL,
    set_key     varchar(64)  NOT NULL,
    subject     varchar(256) NULL,
    input       jsonb        NOT NULL,
    expected    jsonb        NOT NULL,
    tags        text[]       NOT NULL,
    labelled_by varchar(256) NULL,
    created_at  timestamptz  NOT NULL,
    removed_at  timestamptz  NULL,
    CONSTRAINT pk_ai_eval_items PRIMARY KEY (id),
    CONSTRAINT fk_ai_eval_items_set FOREIGN KEY (set_key) REFERENCES {{schema}}.ai_eval_sets (key)
);

CREATE INDEX IF NOT EXISTS ix_ai_eval_items_set ON {{schema}}.ai_eval_items (set_key, created_at);

-- One cell of the matrix: a set run through one prompt version on one model.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_eval_runs (
    id             uuid         NOT NULL,
    set_key        varchar(64)  NOT NULL,
    use_case       varchar(64)  NOT NULL,
    prompt_version integer      NOT NULL,
    prompt_hash    varchar(12)  NOT NULL,
    provider       varchar(64)  NOT NULL,
    model          varchar(128) NOT NULL,
    started_at     timestamptz  NOT NULL,
    finished_at    timestamptz  NULL,
    note           text         NULL,
    CONSTRAINT pk_ai_eval_runs PRIMARY KEY (id),
    CONSTRAINT fk_ai_eval_runs_set FOREIGN KEY (set_key) REFERENCES {{schema}}.ai_eval_sets (key)
);

CREATE INDEX IF NOT EXISTS ix_ai_eval_runs_set ON {{schema}}.ai_eval_runs (set_key, started_at);

-- Every model attempt, fallbacks and eval runs included, with its prompt, tokens, cost and latency.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_inferences (
    id                 uuid         NOT NULL,
    at                 timestamptz  NOT NULL,
    use_case           varchar(64)  NOT NULL,
    prompt_version     integer      NOT NULL,
    prompt_hash        varchar(12)  NOT NULL,
    prompt_model_match varchar(128) NULL,
    provider           varchar(64)  NOT NULL,
    model              varchar(128) NOT NULL,
    attempt            integer      NOT NULL,
    subject            varchar(256) NULL,
    input_hash         varchar(64)  NOT NULL,
    input_tokens       bigint       NOT NULL,
    output_tokens      bigint       NOT NULL,
    cache_read_tokens  bigint       NOT NULL,
    cache_write_tokens bigint       NOT NULL,
    reasoning_tokens   bigint       NOT NULL,
    cost_micro_usd     bigint       NOT NULL,
    priced             boolean      NOT NULL,
    latency_ms         bigint       NOT NULL,
    outcome            varchar(16)  NOT NULL,
    error_kind         varchar(32)  NULL,
    error              text         NULL,
    finish_reason      varchar(64)  NULL,
    output             text         NULL,
    eval_run_id        uuid         NULL,
    CONSTRAINT pk_ai_inferences PRIMARY KEY (id),
    CONSTRAINT ck_ai_inferences_outcome CHECK (outcome IN ('succeeded', 'failed', 'refused', 'schema_invalid')),
    CONSTRAINT ck_ai_inferences_error_kind CHECK (error_kind IS NULL OR error_kind IN (
        'unknown', 'network', 'timeout', 'rate_limited', 'unauthorized', 'unavailable', 'bad_request', 'content_filtered',
        'invalid_response', 'schema_mismatch', 'not_configured')),
    CONSTRAINT ck_ai_inferences_attempt CHECK (attempt > 0)
);

-- The runner's cache lookup: the same use case, prompt text, model and rendered input.
CREATE INDEX IF NOT EXISTS ix_ai_inferences_cache ON {{schema}}.ai_inferences (use_case, prompt_hash, provider, model, input_hash);
CREATE INDEX IF NOT EXISTS ix_ai_inferences_subject ON {{schema}}.ai_inferences (subject) WHERE subject IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_ai_inferences_eval_run ON {{schema}}.ai_inferences (eval_run_id) WHERE eval_run_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_ai_inferences_at ON {{schema}}.ai_inferences (at);

-- One item in one run: the outcome, the score against the expected answer, the item's cost and latency, and a rating.
CREATE TABLE IF NOT EXISTS {{schema}}.ai_eval_scores (
    run_id         uuid         NOT NULL,
    item_id        uuid         NOT NULL,
    inference_id   uuid         NULL,
    outcome        varchar(16)  NOT NULL,
    correct        boolean      NOT NULL,
    severe         boolean      NOT NULL,
    misses         text[]       NOT NULL,
    answer         jsonb        NULL,
    cost_micro_usd bigint       NOT NULL,
    latency_ms     bigint       NOT NULL,
    from_cache     boolean      NOT NULL,
    rating         smallint     NULL,
    comment        text         NULL,
    rated_by       varchar(256) NULL,
    CONSTRAINT pk_ai_eval_scores PRIMARY KEY (run_id, item_id),
    CONSTRAINT fk_ai_eval_scores_run FOREIGN KEY (run_id) REFERENCES {{schema}}.ai_eval_runs (id),
    CONSTRAINT fk_ai_eval_scores_item FOREIGN KEY (item_id) REFERENCES {{schema}}.ai_eval_items (id),
    CONSTRAINT ck_ai_eval_scores_outcome CHECK (outcome IN ('succeeded', 'failed', 'refused', 'schema_invalid')),
    CONSTRAINT ck_ai_eval_scores_rating CHECK (rating IS NULL OR rating BETWEEN 1 AND 5)
);

CREATE INDEX IF NOT EXISTS ix_ai_eval_scores_item ON {{schema}}.ai_eval_scores (item_id);

-- The combined report: one row per run and item, joined with the run, the item and the inference that answered. The
-- tokens are that inference's; a cached answer reports the stored inference it reused. Readable from psql as it is:
--   select model, prompt_version, avg(correct::int), sum(cost_micro_usd) from ai.ai_eval_report_rows group by 1, 2;
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
