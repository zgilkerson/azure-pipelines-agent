# YAML getting started - Templates (not available, for discussion only)

## Example: Simple input

The following example illustrates two things:

1. Front-matter is used to define the default values for inputs.

2. Inputs are expanded using template expressions. A value that starts and ends
   with `${{ }}` indicates a template expression.

Template:

```yaml
# Default values
inputs:
  solution: '**/*.sln'
  
steps:
- task: msbuild@1
  inputs:
    solution: ${{ inputs.solution }}
- task: vstest@2
  inputs:
    solution: ${{ inputs.solution }}
```

Consumer:

```yaml
steps:
- template: steps/msbuild.yml
  inputs:
    solution: my.sln
```

## Example: Insert into an array

Template:

```yaml
# Default values
inputs:
  preBuild: []
  postBuild: []

phases:
- phase: build
  steps:
  - ${{ inputs.preBuild }}
  - task: msbuild@1
  - task: vstest@2
  - ${{ inputs.postBuild }}
```

Consumer:

```yaml
phases:
- template: phases/build.yml
  inputs:
    preBuild:
    - script: echo hello from pre build
    postBuild:
    - script: echo hello from post build
```

Note, when an array is inserted into an array, the nested array is flattened.

## Example: Insert into a dictionary

Although YAML has a syntax for inserting into a dictionary, we need to invent
something because we are post-processing the file.

In the example below, the `${{ insert }}` property indicates the value that follows
is expected to be a mapping, and should be inserted into the outer mapping.

Template:

```yaml
# Default values
inputs:
  variables: {}

phases:
- phase: build
  variables:
    configuration: debug
    arch: x86
    ${{ insert }}: ${{ inputs.variables }}
  steps:
  - task: msbuild@1
  - task: vstest@2
```

Consumer:

```yaml
phases:
- template: phases/build.yml
  inputs:
    variables:
      TEST_SUITE: L0,L1
```

## Example: If, elseif, else

The below example illustrates `if`, `elseif`, and `else`.

Template:

```yaml
# Default values
inputs:
  toolset: msbuild

steps:
  # msbuild
  ${{ if eq(inputs.toolset, 'msbuild') }}:
  - task: msbuild@1
  - task: vstest@2

  # dotnet
  ${{ elseif eq(inputs.toolset, 'dotnet') }}:
  - task: dotnet@1
    inputs:
      command: build
  - task: dotnet@1
    inputs:
      command: test

  # error
  ${{ else }}:
  - script: echo Expected toolset 'dotnet' or 'msbuild' && exit 1
```

Consumer:

```yaml
phases:
- template: steps/build.yml
  inputs:
    toolset: msbuild
```

## Example: Optionally insert into an array

Template:

```yaml
# Default values
inputs:
  publish: true
  
phases:
  steps:
  - task: msbuild@1
  - task: vstest@2
  - ${{ if parseBool(inputs.publish) }}:
    - task: publishBuildArtifacts@1
```

Consumer:

```yaml
phases:
- template: phases/build.yml
  inputs:
    publish: false
```

## Example: Optionally insert into a dictionary

Template:

```yaml
# Default values
inputs:
  queue: ""

phases:
- phase: build
  # Only insert the queue if it was specified
  ${{ if inputs.queue }}:
    queue: ${{ inputs.queue }}
  steps:
  - task: msbuild@1
  - task: vstest@2
```

Consumer:

```yaml
phases:
- template: phases/build.yml
  inputs:
    queue: myQueue
```

## Example: Foreach (not now, future maybe)

What would foreach look like if we wanted to implement it?

In the below example, a foreach loop is used install, build, and test based on
an array of node versions.

```yaml
inputs:
  nodeVersions:
  - "8.x"
  
steps:
- ${{ foreach }}: ${{ inputs.nodeVersions }}
  ${{ yield }}:
  - task: nodeTool@0
    displayName: ${{ concat('Setup node ', item) }}
    inputs:
      versionSpec: ${{ item }}
  - script: npm test
    displayName: ${{ concat('Build and test with node ', item) }}
```

Note, in the above example, the array to iterate over is intentionally not part of the
`foreach` expression. This makes it easier to iterate over hardcoded values if desired.

For example,

```yaml
steps:
- ${{ foreach }}:
  - "8.x"
  - "6.x"
  - "4.x"
  ${{ yield }}:
  - task: nodeTool@0
    displayName: ${{ concat('Setup node ', item) }}
    inputs:
      versionSpec: ${{ item }}
  - script: npm test
    displayName: ${{ concat('Build and test with node ', item) }}
```

## Escaping

Use `$${{ }}` to escape a value that literally starts and ends with `${{ }}`.

## Appendix

### Goals

- Step reuse is likely the most common scenario
- Do not preclude the ability to cache sections of the file (e.g. triggers)
- The strategy we choose for reuse-templates, should work in the future for policy-templates
- Keep the power of static expansion relatively simple
  - Reserve complexity for runtime (more powerful)
  - Reduce number of ways of doing the same thing
  - Reduce number of things users need to learn
- Templates are an advanced scenario. Syntax falls outside of "keep it simple" scenarios.
- Template author can reorganize template without breaking consumer (pass inputs, versus override from outside)
- Make sense of output variables, coming from steps within step templates
  - Note, this is not dealt with in this document. But this is something we need to think about.

### Chosen strategy for input replacement: post-processing

The current strategy is to deserialize the YAML document first, and then process the template expressions.

- Avoids injection problems
- Enables caching sections of the file
- Avoids messy nature of preprocessing (see below section)
- Reduces things to learn. Reuses known concepts: yaml language, expressions.

### Why not text-templating?

- Messy. It will generate too much confusion, complaints, and support load
  - Line numbers problem for parse errors
  - Indentation problem
  - Another layer of syntax and escaping challenges
- Too many ways of doing the same thing.
  - Reserve complexity for runtime (more powerful)
- Increases number of things users needs to learn
- Enables injection attack (policy-template scenario)

### How to avoid implicit flattening when inserting a sequence into a sequence

In the future, we can add the syntax `${{{ }}}` to indicate not to flatten.

A workaround today would be to add an extra sequence layer.

We don't have the scenario today where the template author doesn't know whether it expects a sequence versus some other type.
