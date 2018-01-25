# YAML getting started - YAML triggers (not yet available, for discussion only)

## Simple CI trigger syntax

On push, the YAML file from the branch that was updated, is used to determine whether
to queue a CI build.

For example, if master is updated, the trigger defined in master will be used. If a different
branch is updated, the YAML file from that branch will be used to determine whether to queue a
build.

```yaml
trigger:
- master
- releases/*
```

## CI is opt-out

When not specified, the default trigger for build would be all branches.

For build this behavior makes sense. Especially when we start supporting on-push defintion creation from any branch. And on-push resource authorization from any branch.

For release, CI trigger would likely be opt-in? However with YAML, release will be tightly coupled with a repository. So opt-out CI behavior might also make sense for release? Or opt-out, but the default for RM only includes master?

```yaml
trigger: false | none # Turn off CI
```

## Full CI trigger syntax

```yaml
trigger:
  batch: bool | number
  branches:
    include: []
    exclude: []
  paths:
    include: []
    exclude: []
```

## Schedules (future; needs cleanup)

```yaml
schedules:
- schedule: name
  whenUnchanged: bool
  days: # Also supports a string instead of an array: "<DAY>" or "All" or "Weekdays"
  - sun
  - mon
  - tues
  - wed
  - thurs
  - fri
  - sat
  timeZone: EST # Defaults to account time zone? Do we know that info? We might need to maintain a mapping for this abbrev, it is not in system.timeZoneInfo.
  times:
  - 03:00 am
  - 03:00 pm
  branches:
    include: []
    exclude: []
```

## Triggers on resources (future)

```yaml
resources:
  repositories:
  - name: tools
    trigger: # Need a property to hang trigger info on
      branches:
        include: []
        exclude: []
      # ...polling, etc

  builds:
  - name: otherDefinition
    trigger: true # Simple build trigger

  - name: otherDefinition2
    trigger: # Build trigger with branch/tag filters
      branches:
        include:
        - releases/*
        exclude: []
      tags:
        include: []
        exclude: []

  packages:
  - name: somePackage
    feed: someFeed
    trigger: true # Packages too
```
