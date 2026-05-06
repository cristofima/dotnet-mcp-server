---
description: Create a feature branch with sequential or timestamp numbering
---


<!-- Extension: git -->
<!-- Config: .specify/extensions/git/ -->
# Create Feature Branch

Create and switch to a new git feature branch for the given specification. This command handles **branch creation only** — the spec directory and files are created by the core `/speckit.specify` workflow.

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Environment Variable Override

If the user explicitly provided `GIT_BRANCH_NAME` (e.g., via environment variable, argument, or in their request), pass it through to the script by setting the `GIT_BRANCH_NAME` environment variable before invoking the script. When `GIT_BRANCH_NAME` is set:
- The script uses the exact value as the branch name, bypassing all prefix/suffix generation
- `--short-name`, `--number`, and `--timestamp` flags are ignored
- `FEATURE_NUM` is extracted from the name if it starts with a numeric prefix, otherwise set to the full branch name

## Prerequisites

- Verify Git is available by running `git rev-parse --is-inside-work-tree 2>/dev/null`
- If Git is not available, warn the user and skip branch creation

## Branch Naming Convention

Before determining the numbering mode, check for a branch-convention configuration:

1. Read `.specify/branch-convention.yml` if it exists.
2. Extract `convention.branch_pattern`, `convention.type_prefix`, `convention.default_type`, and `convention.seq_padding`.
3. If `branch_pattern` is present, you will use it to construct the final branch name after the script determines the next sequence number (see **Applying the Convention** below).

## Branch Numbering Mode

Determine the branch numbering strategy by checking configuration in this order:

1. Check `.specify/extensions/git/git-config.yml` for `branch_numbering` value
2. Check `.specify/init-options.json` for `branch_numbering` value (backward compatibility)
3. Default to `sequential` if neither exists

## Execution

Generate a concise short name (2-4 words) for the branch:
- Analyze the feature description and extract the most meaningful keywords
- Use action-noun format when possible (e.g., "add-user-auth", "fix-payment-bug")
- Preserve technical terms and acronyms (OAuth2, API, JWT, etc.)

Run the appropriate script based on your platform to determine the next sequence number:

- **Bash**: `.specify/extensions/git/scripts/bash/create-new-feature.sh --json --dry-run --short-name "<short-name>" "<feature description>"`
- **PowerShell**: `.specify/extensions/git/scripts/powershell/create-new-feature.ps1 -Json -DryRun -ShortName "<short-name>" "<feature description>"`

Use the `--dry-run` / `-DryRun` flag so the script computes the next number without creating the branch yet. Parse `FEATURE_NUM` from the JSON output.

## Applying the Convention

If `.specify/branch-convention.yml` was found and `branch_pattern` is set:

1. Replace tokens in `branch_pattern` with their values:
   - `{seq}`: zero-padded sequence number from `FEATURE_NUM` (pad to `seq_padding` digits, default 3)
   - `{kebab}`: the short name generated above (already kebab-case)
   - `{type}`: the value from `type_prefix` for `default_type` (e.g., if `default_type: feature` and `type_prefix.feature: "feature"`, use `"feature"`)
2. Set `GIT_BRANCH_NAME` to the fully-expanded branch name (e.g., `feature/001-terraform-azure-workshop-infra`).
3. Run the script again **without** `-DryRun`, passing `GIT_BRANCH_NAME` as an environment variable so the script creates the branch with that exact name:
   - **Bash**: `GIT_BRANCH_NAME="<expanded-name>" .specify/extensions/git/scripts/bash/create-new-feature.sh --json --short-name "<short-name>" "<feature description>"`
   - **PowerShell**: `$env:GIT_BRANCH_NAME="<expanded-name>"; .specify/extensions/git/scripts/powershell/create-new-feature.ps1 -Json -ShortName "<short-name>" "<feature description>"`

If `.specify/branch-convention.yml` does not exist or has no `branch_pattern`, skip the dry-run step and run the script directly (original behavior):

- **Bash**: `.specify/extensions/git/scripts/bash/create-new-feature.sh --json --short-name "<short-name>" "<feature description>"`
- **Bash (timestamp)**: `.specify/extensions/git/scripts/bash/create-new-feature.sh --json --timestamp --short-name "<short-name>" "<feature description>"`
- **PowerShell**: `.specify/extensions/git/scripts/powershell/create-new-feature.ps1 -Json -ShortName "<short-name>" "<feature description>"`
- **PowerShell (timestamp)**: `.specify/extensions/git/scripts/powershell/create-new-feature.ps1 -Json -Timestamp -ShortName "<short-name>" "<feature description>"`

**IMPORTANT**:
- Do NOT pass `--number` — the script determines the correct next number automatically
- Always include the JSON flag (`--json` for Bash, `-Json` for PowerShell) so the output can be parsed reliably
- You must only ever run the branch-creating script call once per feature
- The JSON output will contain `BRANCH_NAME` and `FEATURE_NUM`

## Graceful Degradation

If Git is not installed or the current directory is not a Git repository:
- Branch creation is skipped with a warning: `[specify] Warning: Git repository not detected; skipped branch creation`
- The script still outputs `BRANCH_NAME` and `FEATURE_NUM` so the caller can reference them

## Output

The script outputs JSON with:
- `BRANCH_NAME`: The branch name (e.g., `003-user-auth` or `20260319-143022-user-auth`)
- `FEATURE_NUM`: The numeric or timestamp prefix used