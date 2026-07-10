# AGENTS.md — AgroControl

## Product

AgroControl is a management platform for Argentine feed stores. It covers inventory, sales, cash, fiscal documents, customer accounts, suppliers, purchases, quotations, and reporting.

## Mandatory stack

- API: .NET 10, ASP.NET Core Web API, C#, PostgreSQL/Supabase, without Entity Framework, using Stored Procedures/PostgreSQL functions whenever possible.
- Web: Next.js App Router, TypeScript strict, Tailwind CSS, shadcn/ui.
- Database and storage: Supabase.
- Tests: xUnit for backend; unit/component/E2E tests for frontend.

## Language

- Source code, identifiers, database names, branches, and commits: English.
- User-facing text and functional documentation: Spanish.
- Never translate official fiscal terms when doing so would change their meaning.

## Working agreements

- Read relevant files before editing.
- Prefer small vertical slices over broad scaffolding.
- Do not change unrelated files.
- Do not add a dependency unless it clearly reduces risk or complexity.
- Run the relevant formatter, linter, build, and tests after each change.
- Report commands executed and any validation that could not run.
- Never claim an external integration is working without evidence.
- Use official documentation for version-specific behavior.

## Architecture

- Keep Domain independent from infrastructure.
- Put use cases and application orchestration in Application.
- Keep external services, EF Core, Supabase, ARCA, payment providers, and storage adapters in Infrastructure.
- Keep HTTP concerns in Api.
- Do not use Entity Framework. Use Npgsql through Infrastructure and prefer Stored Procedures/PostgreSQL functions for business operations. Use direct SQL only when justified.
- Use explicit DTOs at API boundaries.
- Prefer feature folders within each layer.

## Domain rules

- Use `decimal` for money and quantities requiring precision.
- Every stock change must create an immutable stock movement.
- Confirmed financial movements must not be silently edited or deleted.
- Correct confirmed transactions using reversal or compensating movements.
- Sales, stock, customer accounts, payments, and cash movements must be transactionally consistent.
- External requests that can be repeated must use idempotency keys.
- Fiscalization failure must not duplicate the sale or fiscal document.
- Keep full audit history for sensitive operations.

## Database

- PostgreSQL naming conventions must be consistent.
- Create indexes for foreign keys, common filters, SKU, barcode, dates, and document numbers.
- Use optimistic concurrency for stock-sensitive operations.
- Use soft delete only for master data that must remain referenced.
- Migrations must be deterministic, SQL-based or handled by an approved lightweight migration tool, and include versioned tables, indexes, grants, Stored Procedures/PostgreSQL functions, and reviewed routine changes.
- Never place service-role credentials in browser code.
- Apply RLS only with explicit policies and tests.


## Stored Procedures and database routines

- Prefer Stored Procedures/PostgreSQL functions for business workflows and data mutations.
- Critical workflows such as sales, stock movements, payments, fiscal documents, cash, customer accounts, supplier accounts, purchases, quotations, and reversals should be implemented as database routines whenever practical.
- Every routine must have a documented contract, parameters, output shape, transaction behavior, side effects, errors, and idempotency expectations.
- Direct SQL from the API is allowed only for justified simple reads, health checks, or infrastructure tasks.
- Never concatenate user input into SQL.
- Avoid duplicating business rules in C# and PostgreSQL.

## Backend standards

- Enable nullable reference types.
- Treat warnings as errors in CI where practical.
- Use Problem Details for API errors.
- Use FluentValidation or equivalent explicit validation.
- Use cancellation tokens for I/O.
- Use structured logging and redact secrets.
- Add integration tests for critical persistence flows.
- Keep controllers or endpoints thin.

## Frontend standards

- Enable strict TypeScript.
- Prefer Server Components by default; use Client Components only when needed.
- Validate forms with Zod.
- Centralize API access and error handling.
- Provide loading, empty, error, and success states.
- Keep POS keyboard-friendly and optimized for fast operation.
- Meet WCAG AA for primary flows.
- Never expose Supabase service keys to the client.

## Security

- Enforce authorization server-side.
- Validate ownership and organization scope on every protected resource.
- Store certificates and secrets outside source control.
- Do not log tokens, passwords, private keys, complete fiscal payloads with sensitive data, or card information.
- Payment card data must never pass through AgroControl unless the selected provider explicitly requires and supports a compliant flow.
- Follow least privilege.

## ARCA

- Access ARCA through an `IFiscalService` abstraction.
- Separate homologation and production configuration.
- Store fiscal attempts, status, CAE, expiry, errors, and correlation identifiers.
- Implement retries only for safe and idempotent operations.
- Do not invent ARCA rules, endpoints, certificate requirements, or response fields.
- Verify all fiscal behavior against current official documentation before implementation.


## Documentation and Notion

- Use the `documentation_notion` agent after relevant implementation, architectural, database, integration, or product-scope changes.
- Maintain `docs/CHANGELOG_LOCAL.txt` as the local text changelog.
- Maintain `docs/PROJECT_SUMMARY_NOTION.md` as the structured Notion-ready project summary.
- Maintain `docs/DECISIONS.md` for durable technical and product decisions.
- Do not claim tests, builds, deployments, fiscal integrations, payment integrations, or Notion synchronization were completed unless there is evidence.
- Do not store secrets, private keys, tokens, or full sensitive payloads in documentation.
- If Notion sync is explicitly requested but the target page is unknown, update the local Notion-ready summary and ask for the destination page.

## Definition of done

A change is complete only when:
- Acceptance criteria are met.
- Authorization and validation are covered.
- Relevant tests pass.
- Build and lint pass.
- Database changes include a migration.
- User-facing changes include Spanish labels and meaningful states.
- Documentation is updated, including the local changelog and Notion-ready project summary when the change is relevant.
- No secrets or generated artifacts are committed.
