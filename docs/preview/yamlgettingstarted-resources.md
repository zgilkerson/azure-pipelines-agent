# YAML getting started - Resources (coming in April)

Resources enable the definition of various items which are to be produced or consumed
by the defining pipeline. 

## Repositories

The basic definition of a repository is something similar to below. The `alias` and 
`type` are the only properties shared among all repository types. 

```yaml
# File: .vsts-ci.yml

resources:
  repositories:
    # Required: Specifies the alias by which this resource is known within the pipeline
  - repository: string
  
    # Required: Specifies the type of repository
    type: (tfsgit|github)
```

### Git Repositories

Most git repositories share a common set of properties as shown below.

#### TfsGit

Initially only repositories located within the same project as the entry file are allowed. 

```yaml
resources:
  repositories:
  - repository: alias
    type: tfsgit

    # Required: Specifies the name of the repository in the project
    name: string
    
    # Optional: Specifies the default ref used to resolve the version 
    # Default: refs/heads/master
    ref: string    
```

#### GitHub

```yaml
resources:
  repositories:
  - repository: alias
    type: github
    
    # Required: Specifies the name of the service endpoint used to connect to github
    endpoint: string

    # Required: Specifies the name of the repository. For example, user/repo or organization/repo.
    name: string
    
    # Optional: Specifies the default ref used to resolve the version
    # Default: refs/heads/master
    ref: string    
```

## Containers

The basic definition of a container is something similar to below. 

```yaml
resources:
  containers:
  # Required. Specifies the alias by which this resource is known within the pipeline
  - container: string 
    
    # Required. Specifies the docker image name
    image: string 

    # Optional. Specifies the name of the service endpoint used to connect to the docker registry
    endpoint: string 

    # Optional. Specifies any extra options you want to add for container startup
    options: string 
    
    # Optional. Specifies whether the image is locally built (not pulled from the docker registry)
    localImage: true | false 
    
    # Optional. Specifies a dictionary of environment variables added during container creation
    env:
      { string: string } 
```
