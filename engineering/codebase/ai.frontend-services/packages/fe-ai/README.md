# @wow-two-beta/fe-ai

The Ai domain's UI package: the TypeScript mirror of the AI contract, small formatting helpers, and a read-only eval
report screen. It fetches nothing; a console or product passes it the data.

```bash
pnpm add @wow-two-beta/fe-ai @wow-two-beta/fe vue
```

```ts
import { AiConstants, type EvalReportModel } from '@wow-two-beta/fe-ai/contracts';
import { formatLatency, formatMicroUsd } from '@wow-two-beta/fe-ai/kit';
import { EvalReportView } from '@wow-two-beta/fe-ai/admin';
```

The view is styled with Tailwind utilities on the fe sdk theme; add the package to the consumer's Tailwind sources:

```css
@source '../../node_modules/@wow-two-beta/fe-ai/dist';
```

| Export | Holds |
|---|---|
| `./contracts` | `AiConstants` and the types `AiOutcome`, `AiErrorKind`, `PromptMode`, `UseCaseModel`, `PromptModel`, `PromptParameters`, `PromptLabelModel`, `ModelPriceModel`, `InferenceModel`, `EvalSetModel`, `EvalItemModel`, `EvalRunModel`, `EvalReportModel`, `EvalRunSummary`, `EvalItemResult` - camelCase JSON, enums as strings, timestamps as ISO strings, GUIDs as strings |
| `./kit` | `formatMicroUsd(micro)` (`$0.0041`) and `formatLatency(ms)` (`850 ms`, `1.50 s`) |
| `./admin` | `EvalReportView` - takes an `EvalReportModel` and renders a runs table and an items table |
