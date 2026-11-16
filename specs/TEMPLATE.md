# Spec: <feature name>

> Rules for writing specs:
> - Describe **behavior only** — no technical implementation details (no frameworks,
>   libraries, storage, algorithms). The spec must still hold if the stack changes.
> - Every acceptance criterion must be machine-verifiable: a test, a command, or an
>   automated check — never "looks right".
> - Every boundary item must be explicit. "视情况而定" (it depends) is not allowed;
>   ask and record the answer instead.

## Goal

<One or two sentences: what this feature does for the user and why.>

## Boundary (explicitly not doing)

- <Thing this feature will not do>
- <Adjacent thing deliberately left out>

## Acceptance criteria

- Given <initial state>, When <action>, Then <observable result>.
- Given <edge state>, When <action>, Then <result>.
- Given <failure state>, When <action>, Then <result>.

## Verification commands

```bash
<test / command that proves the acceptance criteria>
```

## Out of scope

- <Deferred ideas recorded so they are not re-litigated>
