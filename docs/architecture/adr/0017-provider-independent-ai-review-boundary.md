# ADR 0017: Provider-independent AI review boundary

- Status: Proposed
- Date: 2026-07-25

## Context

AI-assisted curation may use Gemini, OpenAI, or a local model over time. Generic TomeOfTongues projects must not depend directly on a vendor SDK, and provider choice must not leak into feedback, content, or MAUI contracts.

Different providers have different retention, training, regional-processing, credential, structured-output, cost, and incident characteristics. No provider is acceptable by default without explicit configuration and review.

## Decision

A future provider-neutral curation boundary will use versioned JSON contracts:

- `CurationRequest` contains only validated, consented, redacted feedback and bounded task instructions.
- `CurationResponse` contains structured proposals, confidence, evidence references, warnings, and provider metadata.
- `IAiCurationProvider` exposes no vendor SDK types.

Provider-neutral ingestion, validation, deduplication, categorization, staleness checks, audit records, and proposal policy belong in a future `TomeOfTongues.Curation` library or tool. Generic application projects may own feedback contracts and local composition/export use cases, but they will not reference vendor packages.

Vendor adapters will live outside the product solution under automation tooling and communicate through the versioned JSON boundary. Runtime or workflow composition selects an adapter explicitly. Local providers may run manually or on an explicitly trusted self-hosted runner.

No provider will be enabled by default. Activation requires review of data retention, training use, processing location, authentication, cost limits, incident handling, and model-input consent. Repository writing remains a separate minimal-permission step.

## Consequences

- Provider replacement does not change generic application or content contracts.
- Vendor-specific security and operational choices remain isolated.
- Structured-output validation and adapter conformance tests are required.
- Local models remain possible without assuming they are automatically safe or capable.
- Additional tooling is required to translate provider-specific APIs into the common protocol.

## Rejected alternatives

- **A vendor SDK in generic projects:** rejected because it creates architectural coupling and complicates privacy review.
- **Provider-native tool access to GitHub:** rejected because model inference and repository mutation require separate trust boundaries.
- **A default cloud provider:** rejected because provider terms and acceptable processing are unresolved human decisions.

