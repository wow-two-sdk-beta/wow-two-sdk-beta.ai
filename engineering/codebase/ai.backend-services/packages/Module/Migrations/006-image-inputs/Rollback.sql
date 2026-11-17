-- Inverts 006-image-inputs — drops the image columns. Dev/test recovery only: every inference's image count and hashes,
-- every item's image references and every batch's pinned image hashes are lost; items read as text-only again.

ALTER TABLE {{schema}}.ai_eval_batch_items
    DROP COLUMN IF EXISTS image_hashes;

ALTER TABLE {{schema}}.ai_eval_items
    DROP COLUMN IF EXISTS images;

ALTER TABLE {{schema}}.ai_inferences
    DROP COLUMN IF EXISTS image_hashes,
    DROP COLUMN IF EXISTS image_count;
