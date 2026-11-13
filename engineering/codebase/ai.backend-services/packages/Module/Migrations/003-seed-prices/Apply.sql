-- 003-seed-prices — the model prices in effect on 2026-11-10, micro-USD per 1M tokens (input, output, cache write,
-- cache read), read from each provider's pricing page. A later price is a new row with a later effective_from, through
-- `ai price put` or a new migration; the runner prices each attempt from the row in effect at its start.

INSERT INTO {{schema}}.ai_model_prices (provider, model, effective_from, input_per_mtok_micro_usd, output_per_mtok_micro_usd,
                                        cache_write_per_mtok_micro_usd, cache_read_per_mtok_micro_usd, source)
VALUES
    ('anthropic', 'claude-haiku-5-5',  '2026-11-10T00:00:00Z',  100000,   500000,  125000,  10000, 'https://platform.claude.com/docs/en/about-claude/pricing'),
    ('anthropic', 'claude-sonnet-5-5', '2026-11-10T00:00:00Z', 2000000, 10000000, 2500000, 100000, 'https://platform.claude.com/docs/en/about-claude/pricing'),
    ('anthropic', 'claude-opus-5-5',   '2026-11-10T00:00:00Z', 4000000, 20000000, 5000000, 200000, 'https://platform.claude.com/docs/en/about-claude/pricing'),
    ('openai',    'gpt-6-luna',        '2026-11-10T00:00:00Z',  100000,   500000,  125000,  10000, 'https://developers.openai.com/api/docs/pricing'),
    ('openai',    'gpt-6.1-terra',     '2026-11-10T00:00:00Z', 1000000,  5000000, 1250000,  50000, 'https://developers.openai.com/api/docs/pricing')
ON CONFLICT (provider, model, effective_from) DO NOTHING;
