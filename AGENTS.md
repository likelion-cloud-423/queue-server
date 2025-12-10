# Agent Guidelines

This document describes guidelines and best practices for agents (human or automated) working in this project.

## Purpose

Agents perform code changes, documentation updates, testing, and maintenance. The aim of this document is to ensure a safe, consistent workflow that makes it easy for collaborators to review and merge changes.

## Version Control (Git)

Use Git for version control and follow these rules:

- Always work on a topic branch and submit changes via Pull Requests (PRs).
- Do not force-push to main or push directly to protected branches.
- Suggested workflow:
  - `git fetch --all`
  - `git switch -c feature/<short-desc>` or `git checkout -b feature/<short-desc>`
  - Make your changes and `git add` / `git commit` (use Conventional Commits if possible)
  - `git push -u origin <branch>` and open a PR

Branch naming examples:

- `feature/<ticket>-<short-desc>`
- `bugfix/<ticket>-<short-desc>`
- `hotfix/<ticket>-<short-desc>`
- `chore/<short-desc>` or `refactor/<short-desc>`

PR checklist:

- Clearly explain what you changed and why
- Link any related issues or tickets
- Keep changes scoped and logically grouped
- Ensure tests/build/lint/type checks pass
- Update documentation if your change affects behavior or configuration

Commit message examples:

- `Add JWT refresh token support`
- `Fix correct user info serialization`
- `Bump gradle wrapper`

## Code Writing and Change Rules

- Read Before Write: Always open and review the latest file contents before editing.
- Consistency: When renaming symbols, update all occurrences across the repo.
- Self-Correction: Run builds, tests, and linters after editing to catch errors early.
- Keep commits small and focused to make review easy.

## Tests and CI

- Add automated tests (unit and/or integration) when applicable for new features or bug fixes.
- Do not merge a PR until CI passes; CI should cover build, tests, lint, and security checks.
- Document how to run the tests locally in the PR description (e.g., `./gradlew test`, `npm test`, `dotnet test`).

## Linting and Formatting

- Follow the repository’s linting and formatting rules. Use config files located at the repo root.
- Consider adding pre-commit hooks to enforce formatting and linting locally.

## Environment Variables and Secret Management

- Never commit secrets into the repo. Provide examples in `.env.example` or `env.example` files.
- When introducing new environment variables, add them to `.env.example` and document their purpose in README or service docs.
- Use secure stores for CI/CD (GitHub Secrets, Azure Key Vault, GCP Secret Manager, etc.).

## Local Development and Build

- This repository contains multiple services and languages (Node.js, .NET, Java/Gradle). Refer to each service's README for build and run instructions.
- Common build/run examples:
  - Node (chat-client): `npm install && npm run build && npm test`
  - .NET (chat-server): `dotnet build && dotnet test`
  - Gradle (queue components): `./gradlew build && ./gradlew test`
  - Docker Compose: `docker-compose up --build`

## Documentation

- Update `README.md`, `spec.md`, or service docs when changes affect public APIs, configuration, or developer experience.
- Include local run instructions, required env variables, and relevant test commands.

## PR Review and Merge Guide

- Reviewers should verify:
  - The change meets the intended purpose
  - Tests, lint, and builds pass
  - No secrets/credentials are leaked
  - Documentation is up to date
  - Any performance/resource impacts are considered
- Leave clear comments and requests for improvements, and call out module owners if necessary.
- After discussions are resolved and CI is green, merge the PR using the repository’s merge policy (e.g., Squash/Merge or Rebase/Merge).

## Security and Licensing

- Watch for security vulnerabilities in external dependencies and check licenses before adding new packages.
- Remove sensitive data from history if committed and rotate any affected secrets immediately.

## Communication and Escalation

- If a change is unclear or large in scope, open an issue and discuss before starting work.
- For urgent tasks, mention relevant team members in the PR or issue to request a quick review.

## Automated Agent Rules

- Automated agents (Dependabot, formatting tools, CI scripts) should open clear PRs with useful descriptions.
- If an automated PR causes problems, handle with manual review and rollback procedures.

## Other Best Practices

- Include simple reproducible steps or manual test procedures in a PR to show before/after behavior.
- Make large changes incrementally and add tests and verification at each step.
- Use conservative defaults for configuration changes and add migration guidance where applicable.

---

This document may change to reflect evolving team practices. If you have suggestions or improvements, open an issue to discuss.
