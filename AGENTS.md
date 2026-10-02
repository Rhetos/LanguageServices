# Instructions for coding agents and contributors

Project documentation, project structure and build instructions: [README.md](README.md).

## Implementation plans

The `plans/` folder holds implementation plans that are not yet executed.
Plans are transient: a plan file is deleted when its work is implemented, so the folder can be empty or missing.
Plans identify their work items with internal codes, for example `A1` or `B6`. These codes have a meaning only inside the plans.

Rules:

1. Outside `plans/`, do not reference the plans. This applies to source code and code comments, tests, `README.md`, `CHANGELOG.md`, `docs/`, and git commit messages. Do not write:
   * links or paths to `plans/` or to a plan file;
   * plan item codes such as `A3` or `B10`, and phrases such as "plan item A3", "listed in the plan" or "before A3".
2. Describe the change or the open issue itself instead, for example:

   | Do not write | Write |
   | --- | --- |
   | Commit message `A3: document URIs round-trip in canonical form.` | `Document URIs round-trip in canonical form.` |
   | "Fixed in plan A3." / "Before A3, ..." | "Fixed in version 6.1.0." / "Before version 6.1.0, ..." |
   | "Listed in plan B10." / "Plan item A5 covers this." | "Not fixed yet (2026-10-02)." and a description of the missing behaviour |
   | Code comment "The A3 acceptance criterion is ..." | "The server must ..." |

3. When you implement a plan item, move the information that stays valid after the plan is deleted (behaviour, design decisions, measured facts, limitations) into `README.md`, `CHANGELOG.md`, `docs/` or code comments.
4. Inside `plans/`, plan files can reference each other, their item codes, and any repository file.
