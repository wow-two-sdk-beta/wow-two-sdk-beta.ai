-- Inverts 003-seed-prices — removes the seeded rows only; prices put later stay.

DELETE FROM {{schema}}.ai_model_prices
WHERE effective_from = '2026-11-10T00:00:00Z'
  AND (provider, model) IN (('anthropic', 'claude-haiku-5-5'), ('anthropic', 'claude-sonnet-5-5'), ('anthropic', 'claude-opus-5-5'),
                            ('openai', 'gpt-6-luna'), ('openai', 'gpt-6.1-terra'));
