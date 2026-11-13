-- Inverts 002-ai-layer — drops the report view and every ai table. Dev/test recovery only: prompts, the inference log,
-- prices and evals are lost.

DROP VIEW IF EXISTS {{schema}}.ai_eval_report_rows;
DROP TABLE IF EXISTS {{schema}}.ai_eval_scores;
DROP TABLE IF EXISTS {{schema}}.ai_inferences;
DROP TABLE IF EXISTS {{schema}}.ai_eval_runs;
DROP TABLE IF EXISTS {{schema}}.ai_eval_items;
DROP TABLE IF EXISTS {{schema}}.ai_eval_sets;
DROP TABLE IF EXISTS {{schema}}.ai_model_prices;
DROP TABLE IF EXISTS {{schema}}.ai_prompt_label_moves;
DROP TABLE IF EXISTS {{schema}}.ai_prompt_labels;
DROP TABLE IF EXISTS {{schema}}.ai_prompts;
DROP TABLE IF EXISTS {{schema}}.ai_use_cases;
