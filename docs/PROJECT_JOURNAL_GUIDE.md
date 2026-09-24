# Project Journal Guide

This file defines how the English Center Platform build journal must be maintained from M1 through M10. Apply it after every completed task, meaningful fix, architectural decision, or milestone.

The journal is written for readers who may not yet have strong software, data, or infrastructure experience. It must let them understand what was built, repeat the work, verify the result, and diagnose the known problems.

## Required structure for every journal entry

1. **Context and objective** — explain what task is being performed and why it is needed in the project.
2. **Prerequisites** — list required tools, versions, files, services, permissions, and the working directory.
3. **Steps** — record the exact actions in execution order. For a command, include:
   - the exact command that was used;
   - where the command must be run;
   - a short explanation of the command and its important parameters;
   - the expected result.
4. **Verification** — record the actual checks performed and their confirmed results. Clearly distinguish an expected result from a verified result.
5. **Problems and fixes** — include the exact symptom, confirmed cause, applied fix, and verification after the fix.
6. **Changed files and decisions** — list important files and architecture or implementation decisions.
7. **Security and data notes** — never include passwords, tokens, private keys, connection-string credentials, or real sensitive data.
8. **Completion status** — state which acceptance criteria passed, what remains, and the next task.
9. **Project synchronization** — update `02_Progress`, `03_Log`, `04_Resources`, and the Overview formulas/lookup results when the task affects them.
10. **Git checkpoint** — after the task reaches a coherent and tested state, review the diff, commit it, and push when a remote and authentication are available.

## Writing rules

- Record only facts confirmed from the project, executed commands, tool output, or an approved design decision.
- Use short explanations and plain language. Explain an acronym the first time it appears.
- Keep paths, ports, versions, file names, and commands exact.
- State when a value is an example, a placeholder, or not yet configured.
- Do not claim that a service works until its build, health check, test, or equivalent verification succeeds.
- Preserve the document format defined by `Huong-dan-dinh-dang-tai-lieu-du-an.docx`.
- Keep the build journal as one project-wide document. Add each milestone and task in chronological order instead of creating an isolated M1 journal.
- Do not remove historical decisions. Record later changes as new CHANGE or DECISION entries.

## Git checkpoint rules

Create a commit when one of these conditions is met:

- a task or acceptance criterion is complete and verified;
- a coherent bug fix is complete and verified;
- an architecture or configuration change has reached a stable reviewable state;
- work is about to move to another milestone or a riskier change.

Before committing:

```powershell
git status
git diff --check
dotnet build
npm.cmd run lint --prefix .\apps\web
npm.cmd run build --prefix .\apps\web
```

Use a short commit message that describes the result, for example:

```text
chore: initialize project workspace
fix: run api without generated apphost
docs: update project build journal
```

Push after the commit when `git remote -v` shows the intended repository and authentication is configured. If either is missing, record the blocker and give the user the exact setup commands.

## Task completion checklist

- [ ] The implementation or configuration is complete.
- [ ] Relevant build, lint, test, health check, or data validation passed.
- [ ] No secret was added to the repository, document, or spreadsheet.
- [ ] The project-wide journal contains the steps, explanations, results, and fixes.
- [ ] The project management Sheet reflects the current status and resources.
- [ ] The Git diff was reviewed.
- [ ] A commit was created when the work formed a stable checkpoint.
- [ ] The commit was pushed when a remote and authentication were available.

