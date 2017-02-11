# Pipelines

#### Note to readers: This is still in flight so some concepts appear that are not fully flushed out

## Goals
- **Define constructs which provide a more powerful and flexible execution engine for RM/Build/Deployment**: Allow pipeline execution with minimal intervention points required from consumers
- **Provide a simple yet powerful config as code model**: Easily scale from very simple processes to more complex processes without requiring cumbersome hierarchies and concepts
- **Provide data flow constructs for simple variables and complex resources**: Provide semantic constructs for describing how data flows through the system

## Non-Goals
- **Provide a full replacement for all existing application-level constructs**: This is not meant to encompass all application semantics in the Build and RM systems

## Terms
- **Pipeline**: A construct which defines the inputs and outputs necessary to complete a set of work, including how the data flows through the system and in what order the steps are executed
- **Job**: A container for task execution which supports different execution targets such as server, queue, or deploymentGroup
- **Condition**: An [expression language](conditions.md) supporting rich evaluation of context for conditional execution
- **Task**: A smallest unit of work in the system, allowing consumers to plug custom behaviors into jobs
- **Variable**: A name/value pair, similar to environment variables, for passing simple data values
- **Resource**: An object which defines complex data and semantics for import and export using a pluggable provider model. See [resources](resources.md) for a more in-depth look at the resource extensibility model.

## Semantic concepts for resources
### Import
A keyword which conveys the intent to utilize an external resource in the current job. The resource which is imported will be placed in the job's working directory in a folder of the same name. References to contents within the resource may simply use relative paths  starting with the resource name. For instance, if you import a resource named `vso`, then the file `foo.txt` may be referenced within  the job simply as `vso/foo.txt`.

### Export
A keyword which conveys the intent to publish a resource for potential consumption in a downstream job. The inputs provided to the `export` item are dependent upon the type of resource which is being exported. 

### How it works
Under the covers `import` and `export` are simply semantic mappings to the resource provider tasks. When the system reads an `import` the statement is replaced with the resource-specific import task as specified by the resource provider. Likewise in place of an `export` the system injects the resource-specific export task as specified by the resource provider. While we could simply document and inform consumers to utlize the tasks directly, this provides a more loosely coupled and easy to read mechanism for performing the same purpose. The keywords also allow the system to infer dependencies between jobs in the system automatically, which further reduces the verbosity of the document.

## Simple pipeline
The pipeline process may be defined completely in the repository using YAML as the definition format. A very simple definition may look like the following:
```yaml
resources:
  - name: vso
    type: self

jobs:
  - name: simple build
    target:
      type: queue
      name: default
    steps:
      - import: vso
      - task: msbuild@1.*
        name: Build solution 
        inputs:
          project: vso/src/project.sln
          arguments: /m /v:minimal
      - export: artifact
        name: drop
        inputs:
          include: ['bin/**/*.dll']
          exclude: ['bin/**/*Test*.dll']
```
This defines a pipeline with a single job which acts on the current source repository. Since all file paths are relative to a resource within the working directory, there is a resource defined with the type `self` which indicates the current repository. This allows the pipeline author to alias the current repository like other repositories, and allows separation of process and source if that model is desired as there is no implicit mapping of the current repository. After selecting an available agent from a queue named `default`, the agent runs the msbuild task from the server locked to the latest version within the 1.0 major milestone. Once the project has been built successfully the system will run an automatically injected  task for the `artifact` resource provider to publish the specified data to the server at the name `drop`.

## Resources
While the previous examples only show a single repository resource, it is entirely possible in this model to provide multiple repositories or any number of resources for that matter in a job. For instance, you could have a job that pulls a `TfsGit` repository in addition to a `GitHub` repository or multiple repositories of the same type. For this particular instance the repository which contains the pipeline definition does not contain code itself, and as such there is no self referenced resource defined or needed.
```yaml
resources:
  - name: vsts-agent
    type: git
    endpoint: git-hub-endpoint # TBD on how to reference endpoints from this format
    data:
      url: https://github.com/Microsoft/vsts-agent.git
      ref: master

  - name: vsts-tasks
    type: git
    endpoint: git-hub-endpoint # TBD on how to reference endpoints from this format
    data:
      url: https://github.com/Microsoft/vsts-tasks.git
      ref: master

jobs:
  - name: job1
    target:
      type: queue
      name: default
    steps:
      - import: vsts-agent
      - import: vsts-tasks
      - task: msbuild@1.*
        name: Compile vsts-agent
        inputs:
          project: vsts-agent/src/build.proj
      - task: gulp@0.*
        name: Compile vsts-tasks
        inputs:
          gulpfile: vsts-tasks/src/gulpfile.js
```
## Job dependencies
For a slightly more complex model, here is the definition of two jobs which depend on each other, propagating the outputs of the first job including environment and artifacts into the second job.
```yaml
resources:
  - name: vso
    type: self

jobs:
  - name: job1
    target: 
      type: queue
      name: default
    steps:
      - import: vso
      - task: msbuild@1.*
        name: Build solution 
        inputs:
          project: vso/src/project.sln
          arguments: /m /v:minimal
      - export: artifact 
        name: drop
        inputs:
          include: ['/bin/**/*.dll']
          exclude: ['/bin/**/*Test*.dll']
      - export: environment
        name: outputs
        inputs:
          var1: myvalue1
          var2: myvalue2

  - name: job2
    target: 
      type: queue
      name: default
    steps:
      - import: jobs('job1').exports('drop')
      - import: jobs('job1').exports('outputs')
      - task: powershell@1.*
        name: Run dostuff script
        inputs:
          script: drop/scripts/dostuff.ps1
          arguments: /a:$(job1.var1) $(job1.var2)
```
This is significant in a few of ways. First, we have defined an implicit ordering dependency between the first and second job which informs the system of execution order without explicit definition. Second, we have declared a flow of data through our system using the `export` and `import` verbs to constitute state within the actively running job. In addition we have illustrated that the behavior for the propagation of outputs across jobs which will be well-understood by the system; the importing of an external environment will automatically create a namespace for the variable names based on the source which generated them. In this example, the source of the environment was named `job1` so the variables are prefixed accordingly as `job1.var1` and `job1.var2`.

## Conditional job execution
By default a job dependency requires successful execution of all previous dependent jobs. Job dependencies are discovered by looking at the `condition` and `import` statements for a job to determine usages of the `jobs(<job name>)` function. All referenced jobs from these statements are considered dependencies and if no custom condition is present a default expression is provided by the system requiring successful execution of all dependencies. This default behavior may be modified by specifying a custom job execution [condition](conditions.md). For instance, we can modify the second job from above as follows to provide different execution behaviors:

### Always run
```yaml
- name: job2
  target: 
    type: queue
    name: default
  condition: "in(jobs('job1').result, 'succeeded', 'failed', 'canceled', 'skipped')"
  ....
```
The condition above places an implicit ordering dependency on the completion of `job1`. Since all result conditions are mentioned `job2` will always run after the completion of `job1`. The presence of the custom condition completely overrides the default behavior of success, configuring this job to run for any result.

### Run based on outputs
```yaml
- name: job2
  target: 
    type: queue
    name: default
  condition: "and(eq(jobs('job1').result, 'succeeded'), eq(jobs('job1').exports.outputs.var1, 'myvalue'))"
  ....
```
The condition above places both a success requirement and the comparison of an output from `job1` which may be dynamically determined during execution. The ability to include output variables from a previous job execution to provide control flow decisions later opens up all sorts of conditional execution policies not available in the current system. Again, as in the previous example, the presence of a custom condition overrides the default behavior.

### Run if a previous job failed
```yaml
jobs:
  - name: job1
    target: 
      type: queue
      name: default
    steps:
      .....
    
  - name: job1-error
    target: 
      type: server
    condition: "eq(jobs('job1').result, 'failed')"
    steps:
      .....
```
In the above example the expression depends on an output of the `job1`. This will place an implicit execution dependency on the completion of `job1` in order to evaluate the execution condition of `job1-error`. Since we only execute this job on failure of a previous job, under normal circumstances it will be skipped. This is useful for performing cleanup or notification handling when a critical step in the pipeline fails.

## Job Toolset Plugins
The default language for a job will be the presented thus far which, while powerful and quite simple, still requires rigid knowledge of the available tasks and system to accomplish even the simplest of tasks. Individual project types, like those which build and test node projects, may find the learning curve for getting started higher than it needs to be. One important tenant of our system is that it is not only powerful but also approachable for newcomers alike. In order to satisfy the on-boarding of more simple projects, we will allow for the job definition language to be extended via `toolset` plug-ins. The general idea behind toolsets would be that for certain tools, such as node, there are common actions which need to occur in most, if not all, jobs which build/test using that specific tool. The plug-in would simply authoring of the job contents by providing custom pluggable points that make sense for that particular job type. Additionally certain things would *just happen*, such as installing the toolset and placing it in the path automatically.
           
For an example of how the internals of a custom language may look, see the [following document](https://github.com/Microsoft/vsts-tasks/blob/master/docs/yaml.md).

## Task Templates
Tasks are another construct which may be templated. On the server these are known as `TaskGroups`, and this provides a mechanism for performing the same style of reuse without requiring interaction with the server model. 
```yaml
inputs:
  - name: projectFile
    type: string
  - name: msbuildArgs
    type: string
    defaultValue: 
  - name: testAssemblies
    type: string

- task: msbuild@1.*
  name: Build $(projectFile)
  inputs:
    project: $(projectFile)
    arguments: $(msbuildArgs) 
- task: vstest@1.*
  name: Test $(testAssemblies)
  inputs: 
    assemblies: $(testAssemblies)
```
If the above file were located in a folder `src/tasks/buildandtest.yml`, a job may include this group with the following syntax:
```yaml
jobs:
  - name: build
    target:
      type: queue
      name: default
    steps:
      - import: code
      - include: code/src/tasks/buildandtest.yml
        inputs:
          projectFile: code/src/dirs.proj
          testAssemblies: code/bin/**/*Test*.dll
```
This provides the ability to build up libararies of useful functionality by aggregating individual tasks into larger pieces of logic. 

## Job Templates
Much like a task can  be templated, jobs may also be templated using a very similar mechanism as shown below. In the following example, the job template is located at the location `src/jobs/buildandtest.yml` and later referenced from a pipeline. 
```yaml
inputs:
  - name: queueName
    type: string
    defaultValue: default
  - name: repo
    type: git
  - name: project
    type: string
  - name: platform
    type: string
  - name: configuration
    type: string
  - name: testAssemblies
    type: string
    
name: Build $(platform)-$(configuration)
target:
  type: queue
  name: $(queueName)
steps:
  - import: $(repo)
  - task: msbuild@1.*
    name: Build $(projectFile)
    inputs:
      project: $(projectFile)
      arguments: /p:Platform=$(platform) /p:Configuration=$(configuration)
  - task: vstest@1.*
    name: Test $(testAssemblies)
    inputs: 
      assemblies: $(testAssemblies)
```
Alternatively we could choose to use the task template within the job template. Templates themselves may be composed of other templates, futher expanding reusability and illustrating the power of proper componentization.
```yaml
name: Build $(project) for $(platform)-$(configuration)
target:
  type: queue
  name: $(queueName)
steps:
  - import: $(repo)
  - include: $(repo)/src/tasks/buildandtest.yml
    inputs:
      project: $(project)
      testAssemblies: $(testAssemblies)
```
Below we reference the same job template multiple times in order to run the same set of tasks for different input sets. When including a job from a template the name should be provided to allow for local referencing within a pipeline which is not susceptible to changes in the base template. In addition, a condition should be supplied at the inclusion point if desired. 
```yaml
resources:
  - name: code
    type: git
    data:
      url: https://github.com/Microsoft/vsts-agent.git
      ref: master

jobs:
  - include: code/src/jobs/buildandtest.yml
    name: x86-release
    inputs:
      repo: resources('code')
      project: code/src/dirs.proj
      platform: x86
      configuration: release
      testAssemblies: code/bin/**Test*.dll

  - include: code/src/jobs/buildandtest.yml
    name: x64-release
    inputs:
      repo: resources('code')
      project: code/src/dirs.proj
      platform: x64
      configuration: release
      testAssemblies: code/bin/**Test*.dll

  - name: finalize
    target: server
    condition: and(succeeded('x86-release'), succeeded('x64-release'))
    steps:
      ....
```
## Variables
TODO: Discuss variables, variable groups, and concepts for iterating over arrays for dynamic job expansion

## Pipeline Templates
### This is not well thought out at this point. Not clear what is overridable, if anything, when including an entire pipeline. Also not clear if we want to support (the answer is likely yes) including multiple pipelines into a larger pipeline for larger orchestrations built up from smaller pieces.
Pipelines may be authored as stand-alone definitions or as templates to be inherited. The advantage of providing a model for process inheritance is it provides the ability to enforce policy on a set of pipeline definitions by providing a master process with configurable overrides. There are concepts which may be used in a template that might not show up 

The definition for a template from which other pipelines inherit, in the most simple case, looks similar to the following pipeline.

```yaml
inputs:
  - name: queueName
    type: string
    default: default
  - name: repo
    type: git
  - name: projectFile
    type: string

jobs:
  - name: build
    target: 
      type: queue
      name: $(queueName)
    steps:
      - import: $(repo)
      - task: msbuild@1.*
        name: Build the project
        inputs:
          project: $(projectFile)
      - export: artifact
        name: drop
        inputs:
          include: ['bin/**/*.dll']
```
A usage of this template from a separate repository is shown below. The first step is to `include` the template file which will be utliized. Next any local `resources` which need to be provided to the template are defined and provided their own definition specific names. Last, the template is invoked using the name given to it within the file which includes it. 
```yaml
includes: 
  - name: core
    source:
      type: git
      url: https://github.com/Microsoft/pipeline-templates.git
      ref: refs/tags/lkg

resources:
  - name: code
    type: self

# Override the required input with the proper value
pipeline: core/pipelines/core.yml
  inputs:
    project: code/src/dirs.proj
    repo: resources('code')
    
pipeline: core/pipelines/core2.yml
  inputs: 
    repo: resources('code')
    drop: pipelines('core').exports('drop')
```
Templates are very much macro replacements, in that the template is simply copied inline and replaces the reference at the time the pipeline is compiled. It is important to point out that while entire pipelines may be templated and reused, other constructs within the system may also be templated and reused such as tasks and jobs.
