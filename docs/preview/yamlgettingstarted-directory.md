# YAML getting started - YAML directory (not available, for discussion only)

## Summary

We plan to support a convention-based directory structure, with a couple goals in mind:

1. Today we only create a build definition on-push for the root file `.vsts-ci.yml`. And resource authorization on-push is only handled for that one file. With a convention based
directory structure, we can watch for files under a specific directory.

2. For template scenarios, a convention-based directory structure would enable referencing templates by name, instead of by relative path.

## Directory structure

```
.vsts-pipelines/
  builds/
    build.yml
  releases/
    release.yml
  steps/
    msbuild.yml
    node.yml
  phases/
  stages/
```

## Definition creation on-push

On push, builds or releases would be created for yml files under the `builds` and `releases` directories.
The file names represent the definition name.

Subdirectories under `builds` and `releases` are supported as well, and create the same directory
structure in the web.

## Template references

Using the `steps`, `phases`, and `stages` folders enables referring to templates by name.

Consider the directory structure:

```
.vsts-pipelines/
  builds/
    foo/
      foo-ci.yml
      foo-pr.yml
      foo-scheduled.yml
  steps/
    foo/
      build.yml
```

Given the above directory structure, the steps-template can be referenced from the definition YAML file
by specifying the template's pathy-name. For example:

```yaml
steps:
- template: foo/build
```

Without relying on a convention-based layout, the relative path would need to be specified from the
definition YAML file. For example:

```yaml
steps:
- template: ../../steps/foo/build.yml
```