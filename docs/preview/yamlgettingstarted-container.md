
# YAML getting started - YAML Group Step and Container (not yet available, for discussion only) 

## Group Step

Group step in yaml that represent a set of task steps.

### Syntax

The syntax to declare a group is:

```yaml
steps:
- group: string # The group name

  displayName: string

  enabled: true | false

  continueOnError: true | false

  condition: string # Defaults to succeeded(). https://go.microsoft.com/fwlink/?linkid=842996

  timeoutInMinutes: number # Whole numbers only. Zero indicates no timeout.
  
  container: string # The container the entire group will runs in.
  
  outputs: { string: string} # Output variables mapping for steps within the group, output variables from task in the group wouldn't flow automatically, declare is required. 

  steps: [ script | powershell | bash | task ] # A list of task that this group contains
```

### Example

A simple build definition with group may look like this:

```yml
steps:
- group: group1
  steps:
  - script: set
  - script: "echo ##vso[task.setvariable variable=var3;isoutput=true;]value3"
    name: groupscript2
  - script: set
  outputs: 
    groupvar1: groupscript2.var3
    groupvar2: groupscript2.varX
- script: set    
```

The group step should fill the gap of the task group concept we had today and make it even more powerful, since the DistributedTask system and agent doesn't understand task group, we have few features are not working as people expected today.
- Condition for group
- Output variable for group

The group step will also becomes to a reuseable point for yaml template, like you can define group step in different files, and pull all of them in your main yaml file.

## Container

Container resource in yaml that allow a task step or group step declare at runtime which container instance the step will use.

### Syntax

```yaml
resources:
  containers:
  - name: string # The container name, step will reference container by name.
    
    type: string # The container type, like docker and kubernetes. We only support docker today.
    
    { string: string } # Any container data used by the container type.
```

Docker container syntax
```yaml
resources:
  containers:
  - name: string # The container name, step will reference container by name.    
    
    type: docker 
    
    image: string # Docker image name

    registry: string # The private docker registry endpoint's name defined in VSTS

    options: string # Any extra options you want to add for container startup.
    
    localimage: true | false # Whether the image is locally built and don't pull from docker registry
```

### Example

A simple container resource declaration may look like this:

```yaml
resources:
  containers:
  - name: dev1
    type: docker
    image: ubuntu:16.04
  - name: dev2
    type: docker
    image: private:ubuntu
    registry: privatedockerhub
  - name: dev3
    type: docker
    image: ubuntu:17.10
    options: --cpu-count 4
  - name: dev4
    type: docker
    image: ubuntu:17.10
    options: --hostname container-test --env test=foo --ip 192.168.0.1
    localimage: true
```

A simple build definition with step using container may look like this:

```yaml
resources:
  containers:
  - name: dev1
    type: docker
    image: ubuntu:16.04
  - name: dev2
    type: docker
    image: private:ubuntu
    registry: privatedockerhub
phases:
- phase: phase1
  steps:
  - script: printenv
    container: dev1
  - group: group1
    container: dev2  
    steps:
    - script: printenv
```

Base on the container type, the agent will handle when and how to create the container.  

**Docker container:**  
All docker containers used in the job will be created just in time, and the agent will stop each container after the last step that needs the container finish, so we can save resource on the host machine.
