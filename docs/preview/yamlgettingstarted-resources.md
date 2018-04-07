# YAML getting started - Resources (coming in April)

Resources enable the definition of various items which are to be produced or consumed
by the defining pipeline. 

## Repositories

Repositories may be defined in the resources section. The basic definition of a repository
is something similar to below, with different repository types requiring potentially 
different inputs.

```yaml
# File: .vsts-ci.yml

resources:
  repositories:
  - repository: myalias
    type: tfsgit
    name: myproduct
```

## Supported Repository Types

### TfsGit

Initially only repositories located within the same project as the entry file are allowed.

```yaml
# Required Properties

repository: Specifies the alias of this repository within the pipeline
type: tfsgit
name: Specifies the name of the repository in TFS
```
```yaml
# Optional Properties

id: Specifies the repository identifier
ref: Specifices a ref which should be 
```
