# YAML changes

The goal of this document is to plan for immediate YAML structural changes.

The YAML structure should be optimized for the simple case. As scenario complexity increases,
increased structural complexity is OK. The scale that is used to measure complexity is:

| Scenario                    | Complexity        |
| --------------------------- | ----------------- |
| single phase, single config | keep it simple    |
| multi phase, single config  | relatively simple |
| single phase, multi config  | relatively simple |
| multi phase, multi config   | complex           |

## Execution options

In the spirit of simplification, `jobs` and `job` will be eliminated. However, the concept
of jobs will still exist within the system. An `executionOptions` property will be added
on a phase, to enable a job matrix and other multi-job scenarios.

### Matrix

Example matrix:

```yml
phases:
  - phase: foo
    execution:
      maxConcurrency: 2 # default is 1 (sequential)
      matrix:
        x64_debug:
          arch: x64
          config: debug
        x64_release:
          arch: x64
          config: release
        x86_release:
          arch: x86
          config: release
    steps:
      - script: echo hello
```

### Slicing

Example slicing:

```yml
  - phase: baz
    execution:
      nameFormat: my_job_$(system.sliceNumber)
      maxConcurrency: 4
```

### Job properties

A further implication of eliminating `job` is that all job-level properties must go in the execution
section. For example:

```yml
  - phase: foo
    continueOnError: false # when false: if a job fails, cancel other jobs and phaseResult = failed
                           # when true: if a job fails, continue executing other jobs and phaseResult = succeededWithIssues (planResult=succeeded)
    execution:
      nameFormat: job_$(system.sliceNumber)
      continueOnError: true # when true: job failed -> partially succeeded
      maxConcurrency: 4 # limited by available agents
    steps:
      - script: echo hello
```

### Explicit approach to matrix?

A more explicit approach to a matrix would allow for individual job demands, and individual continueOnError settings.

The downside is, it complicate the structure significantly. It might be better to push the user to multiple phases instead. (Also, an option for macro replacement within target-level demands discussed below)

```yml
  - phase: foo
    execution:
      maxConcurrency: 4
      jobs:
        - name: job_windows_x86_debug
          demands:
            - agent.os -eq Windows_NT
          variables:
            arch: x86
            config: debug
          continueOnError: false
        - name: job_linux_x64_release
          demands:
            - agent.os -eq Linux
          variables:
            arch: x64
            config: release
          continueOnError: true
    steps:
      - script: echo hello
```

### Alternate slicing knobs?

```yml
  - phase: foo
    execution:
      nameFormat: job_$(system.sliceNumber)
      slices: 4
      maxConcurrency: 2
      minConcurrency: 2
    steps:
     - script: echo hello
```

## Targets

### Simple case

For the simple case, the target does not need to be specified. When no, target is specified, a "queue"
target is assumed. If the definition web UI only has one queue, then that queue is assumed. Otherwise, error.

### Deployment group

```yml
target:
  deploymentGroup: myDeploymentGroup
  tags:
    - myTag1
    - myTag2
  healthOption: healthPercentage
  percentage: 75
steps:
  - script: echo hello
```

### Queue

```yml
target:
  queue: myQueue
  demands:
    - agent.os -eq Windows_NT
steps:
  - script: echo hello
```

### Server

```yml
target: server
steps:
  - labelSources: self
```

### Queue authorization (manual scenario, not push scenario)

In the definition web UI, the user will need to maintain a list of authorized queues. For example,
see the ascii representation below of the proposed web UI (text boxes, combo boxes, and "+" button).

```
Queues:

    Alias                Queue
    --------------    ------------------
    | buildPhase |    | Hosted 2017  v |
    --------------    ------------------
    --------------    ------------------
    | testPhase  |    | MyTestQuee   v |
    --------------    ------------------
    -----
    | + |
    -----
```

Having an alias allows the queue to be changed at queue-time. The yml file refers to the alias,
which does not have to match the actual queue name.

For the manually created new definition scenario, the user must choose the queue:

```
Queues:

    Alias                Queue
    --------------    ------------------
    | main       |    | <CHOOSE>     v |
    --------------    ------------------
    -----
    | + |
    -----
```

### Variables in demands?

Should demands allow macro replacement (scenario is for multiplier variables)?

```yml
target:
  queue: myQueue
  demands:
    - agent.os -eq $(os)
execution:
  matrix:
    - os: Windows_NT
    - os: Darwin
steps:
  - script: echo hello
```

### Demands as expressions?

Should we go ahead and model the demands as expressions, but convert into old demand format?

```yml
target:
  queue: myQueue
  demands:
    - eq(capabilities['agent.os'], 'Windows_NT')
steps:
  - script: echo hello
```

This would enable us to add helper functions now. For example:

```yml
target:
  queue: myQueue
  demands:
    - windows() # equivalent to eq(capabilities['agent.os'], 'Windows_NT')
```

## Single repo (today)

### Checkout simple case

To accommodate the simple case, sync'ing the repo is implied. Therefore:

```yml
steps:
  - script: echo hello
```

is equivalent to:

```yml
steps:
  - checkout: self # "self" is a reserved repo alias
  - script: echo hello
```

Not sync'ing sources is relatively uncommon. Explicitly opting out is required:

```yml
steps:
  - checkout: none # "none" is a reserved repo alias
  - script: echo hello
```

### Checkout options

To optimize the experience for simple builds (single phase, single repo), checkout options should be
allowed on the checkout step.

```yml
steps:
  - checkout: self
    clean: true # Should true be the default?
    checkoutSubmodules: true
    checkoutNestedSubmodules: true
    fetchDepth: 10
    lfsSupport: true
```

### Defined as resource

For advanced scenarios (multi-phase or templates), defining the repositories, and their default checkout
options, up-front in the yml file should be allowed.

```yml
resources:
  - repo: self
    clean: true
    checkoutSubmodules: true

phases:
  - phase: Windows
    steps:
      checkout: self # Inherits checkout options defined in "resources" section.
      script: echo hello

  - phase: macOS
    steps:
      checkout: self
      script: echo hello
```

## Multiple repos (future)

The goal today is single repos. However, developing some idea of how multiple repos
will be modeled in the future is important (to the extent it influences how we model
single repos).

The following is merely thoughts about how multiple can be modeled in the future.

### Defining multiple repos

```yml
resources:
  # Secondary repo in the same Team Project
  - repo: my-other-repo
    type: vstsGit # or simply "vsts"?

  # Secondary repo in the same Project Collection, different Team Project
  - repo: my-other-repo
    type: vstsGit
    teamProject: MyOtherTeamProject

  # There is value in specifying the type as GitHub since special APIs can be leveraged (e.g. retrieve
  # a file at a commit)... need to verify whether GitHub API can be called anonymously.
  - repo: Microsoft/vsts-tasks
    type: gitHub
    clean: true

  # Example: alias does not match the GitHub organization-qualified repo name
  - repo: tasks
    type: gitHub
    organization: Microsoft
    repoName: vsts-tasks

  # Public Git
  - repo: vsts-task-lib
    type: git
    url: https://github.com/Microsoft/vsts-task-lib.git

  # Authenticated GitHub
  - repo: vsts-task-tool-lib
    type: gitHub
    endpoint: myGitHubEndpoint
    embedCredential: true

  # TFVC
  - repo: externals
    type: tfvc
    mappings:
      - map: $/myProject/externals
      - cloak: $/myProject/externals/legacy
      - map: $/myProject/externals/legacy/oldCompiler
        localPath: legacy/oldCompiler
```

### Checking out multiple repos

```yml
# [...]
phases:
  - phase: Debug
    steps:
      - checkout: self
        localPath: mainRepo
      - checkout: otherRepo
        localPath: otherFolder/otherRepo # relative path under $(build.sourcesDirectory)
```

### Future problems

* Synchronizing on the same commit across multiple phases (for secondary repos)
* How is label sources represented in yml? And is it associated at the phase or process level?
