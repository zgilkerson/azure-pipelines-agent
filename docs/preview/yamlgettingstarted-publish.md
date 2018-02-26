# YAML getting started - Publish artifact

## Publish artifact step

The `publishArtifact` step can be used to upload a directory, as an artifact associated with the build.

## Example: Publish the artifact staging directory

In the following example, the first step creates a file under the artifact staging directory.

The `publishArtifact` step uploads the artifact staging directory. When the build completes, the artifact named `drop` can be downloaded from the artfacts tab on the build summary page.

```yaml
steps:
- script: echo hello > $(build.artifactStagingDirectory)/hello.txt

- publishArtifact: drop
```

## Example: Publish a custom folder

In the following example, a custom folder is used to stage the artifact.

```yaml
steps:
- script: |
    mkdir layoutDir
    echo hello > layoutDir/hello.txt
    echo world > layoutDir/world.txt

- publishArtifact: layout
  sourceFolder: layoutDir # or $(system.defaultWorkingDirectory)/layoutDir
```

## More publish options

For more options, you can use the [Publish Build Artifacts task](https://docs.microsoft.com/en-us/vsts/build-release/tasks/utility/publish-build-artifacts)

## Remaining questions

- Artifact type?
  - Today this will use the server artifact type. Will the artifact type change with the new infrastructure?