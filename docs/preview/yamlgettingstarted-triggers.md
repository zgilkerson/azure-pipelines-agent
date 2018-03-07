# YAML getting started - YAML triggers (coming in March)

## CI triggers

Continuous integration builds are on by default for all branches.

### Simple CI trigger syntax

A simple list of branches can be specified in the file, to control which branches trigger a CI build.

When updates are pushed to a branch, the YAML file in that branch is used to evaluate the branch filters.

For example, a simple list of inclusive branch filters may look like:

```yaml
trigger:
- master
- releases/*
```

### Full CI trigger syntax

For more control, an alternative trigger syntax is available:

```yaml
trigger:
  branches:
    include: [string] # todo: examples
    exclude: [string]
  paths:
    include: [string]
    exclude: [string]
```

Note, path filters are only supported for Git repositories in VSTS.

### CI is opt-out

Continuous integration builds can be turned off by specifying `trigger: none`

Optionally, the triggers can be managed from the web definition editor, on the Triggers tab.

## Pull request triggers

Pull request builds are on by default for all branches. Optionally an explicit list of branch filters can
be specified in the YAML file.

When a PR is updated, the YAML file from the target branch is used to evaluate the branch filters.

And the workflow defined in the YAML file from the merge branch, is used to execute the build.

### Simple PR trigger syntax

A simple list of branches can be specified in the file, to control which branches trigger a PR build.

For example, a simple list of inclusive branch filters may look like:

```yaml
trigger:
- master
- releases/*
```

### Full PR trigger syntax

For more control, an alternative PR syntax is available:

```yaml
pr:
  forks: true # whether to build PRs from forks
  branches:
    include: [string] # todo: examples
    exclude: [string]
```
