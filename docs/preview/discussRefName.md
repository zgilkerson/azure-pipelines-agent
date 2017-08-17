# YAML display name and ref-name proposal

Two goals of this document:

1. Overcome phase display name limitation
2. Consider whether \"display\" significantly more intuitive than \"name\"

## Phase name limitation

With the existing plan, phase ref-names will be modeled as:

```yaml
phases:
- name: PhaseA # Actually the ref name, no way to specify the display name
  steps:
  - step: echo hello world
- name: PhaseB
  dependsOn: PhaseA
  steps:
  - step: echo hello 2
```

Whereas \"name\" on a task means the display name. For example:

```yaml
steps:
- script: echo hello 1
  name: My fancy display name
  refName: myFirstScript
- script: echo hello 2
  name: My second fancy display name
  refName: mySecondScript
```

In summary, there are two problems:

1. There is no way to specify the display name on a phase
2. Phases use a different refName convention from tasks. Called \"name\" on a phase, \"refName\" on a task.

## Eliminate limitation

Suggestion for phases:

Use `- phase: <NAME>` instead of `- name: <REFNAME>`. This free's up \"name\", so it can be used to indicate the display name. For example:

```yaml
phases:
- phase: PhaseA
  name: My phase display name
  steps:
  - step: echo hello world
- phase: PhaseB
  name: My phase B display name
  dependsOn: PhaseA
  steps:
  - step: echo hello 2
```

## Additional consideration

Furthermore, to eliminate potential confusion by users, we might want to change \"name\" to \"display\" (especially since phases have a \"dependsOn\" which expects the refName).

I'm personally on the fence here. Although my gut tells me \"display\" might head off questions.

For example, phase display name would look like:

```yaml
resources:
- repo: Name

phases:
- phase: PhaseA
  displayName: My phase display name
  steps:
  - step: echo hello world
- phase: PhaseB
  displayName: My phase B display name
  dependsOn: PhaseA
  steps:
  - step: echo hello 2
```

And task refName example:

```yaml
steps:
- script: echo hello 1
  displayName: My fancy display name
  name: myFirstScript
- script: echo hello 2
  displayName: My second fancy display name
  name: mySecondScript
- task: CmdLine@2
  displayName: My fancy name
  name: MyStableRefName
```

Ref name rules:
startsWith `_A-Za-z` followed by `_A-Za-z0-9`
