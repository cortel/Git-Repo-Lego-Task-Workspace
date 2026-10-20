# Repository agent instructions

## Refine requests before acting

For each user request in this repository, apply the following approach to improve depth and precision while preserving the user's intended scope:

1. Identify what an experienced professional in the relevant field would consider that the request has not explicitly covered. Include only considerations material to the intended outcome; do not invent extra deliverables.
2. Identify the most direct primary evidence available: original requirements, source code, actual data, logs, observed behavior, or official documentation, rather than relying only on summaries or assumptions.
3. Identify the concrete methods and tools appropriate to the task.
4. Identify independent evidence that would demonstrate correctness: acceptance criteria, reproducible checks, meaningful tests, authoritative references, or observable outcomes. Distinguish verified facts from assumptions and untested claims.
5. Briefly present these considerations and a rewritten, actionable version of the user's request before answering or implementing it. The rewrite should state the objective, relevant constraints, approach, and verification criteria without changing the user's intent.

Scale this to the request: for a simple question or small edit, use a compact sentence or two; for substantive work, use a short structured explanation. Improve rigor rather than length or project scope. Do not require the user to resubmit or approve the rewritten prompt; continue with the already authorized work. Ask only when missing information materially prevents a correct result. Explicit user instructions to skip refinement take precedence.

## Commit and push changes

The user requests that repository changes made by the agent be committed and pushed to the current intended branch after appropriate verification. The working branch is currently `dev`; respect later branch instructions. Review the diff, commit only relevant changes, preserve unrelated user work, and push to the established origin. Do not force-push or overwrite remote history. Report the commit and push outcome, including any blocker.

## Source organization and schema changes

Keep each production entity, class, interface, enum, and contract record in its own appropriately named file. Generated EF migration partial classes and the hosting Program entry point follow their tooling conventions. Keep domain entities independent of EF; put per-entity mappings in Infrastructure/Configurations. Use EF Core Code First: edit entities/mappings, generate and review the migration plus updated snapshot, then apply the migration through the appropriate environment's deployment process. Do not generate empty migrations for file-only refactors or manually edit snapshots as a routine workflow.
