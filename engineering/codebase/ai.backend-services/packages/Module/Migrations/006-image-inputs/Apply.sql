-- 006-image-inputs — calls and eval items carry images as references; the log and the queue keep their hashes, never
-- their bytes (v0.3).
--
--   - ai_inferences: how many images an attempt sent and the SHA-256 of each, in order; the input hash already covers
--     them, so a stored answer is reused only for the same images
--   - ai_eval_items: the item's image references (`uri` or `key`, with an optional `mediaType` and `sha256`), frozen
--     with its input; the set export carries them as they are
--   - ai_eval_batch_items: the hashes of the images a batch's prompts take, resolved when it was queued; a run whose
--     images no longer match fails, as one whose input changed does
--
-- Expand only: every column is new with a constant default, so no table is rewritten and the rows made before read as
-- calls and items with no image.

ALTER TABLE {{schema}}.ai_inferences
    ADD COLUMN IF NOT EXISTS image_count  integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS image_hashes text[]  NOT NULL DEFAULT '{}';

ALTER TABLE {{schema}}.ai_eval_items
    ADD COLUMN IF NOT EXISTS images jsonb NOT NULL DEFAULT '[]';

ALTER TABLE {{schema}}.ai_eval_batch_items
    ADD COLUMN IF NOT EXISTS image_hashes text[] NOT NULL DEFAULT '{}';
