# Pipelines
## Goals
- **Define constructs which provide a more powerful and flexible execution engine for RM/Build/Deployment**: Allow pipeline execution with minimal intervention points required from consumers
- **Provide a simple yet powerful config as code model**: Easily scale from very simple processes to more complex processes without requiring cumbersome hierarchies and concepts
- **Provide data flow constructs for simple variables and complex resources**: Provide semantic constructs for describing how data flows through the system

## Non-Goals
- **Provide a full replacement for all existing application-level constructs**: This is not meant to encompass all application semantics in the Build and RM systems

## Terms
- **Pipeline**: A construct which defines the inputs and outputs necessary to complete a set of work, including how the data flows through the system and in what order the steps are executed
- **Job**: A container for task execution which supports different execution targets such as Server, Agent, or DeploymentGroup
- **Condition**: An [expression language](preview/conditions.md) supporting rich evaluation of context for conditional execution
- **Policy**: A generic construct for defining wait points in the system which indicates pass or fail
- **Task**: A smallest unit of work in the system 
- **Variable**: A name and value pair, similar to environment variables, for passing simple data values
- **Resource**: An [object](preview/resources.md) which defines complex data and semantics for upload and download using a pluggable provider model

## Pipeline Process Definition
The pipeline process may be defined completely in the repository using YAML as the definition format. A very simple definition may look like the following:
```yaml
pipeline: 
  resources:
    - name: repo
      type: git
      
  jobs:
    - name: simple-build
      target:
        type: pool
        queue: default
      tasks:
        - task: "msbuild@1.*"
          displayName: Build solution 
          inputs:
            project: "/src/project.sln"
            additionalArguments: "/m /v:minimal"
        - upload: 
            name: drop
            type: artifact
            parameters:
              include:
                - /bin/**/*.dll
              exclude:
                - /bin/**/*Test*.dll
```
This defines a pipeline with a single job 
