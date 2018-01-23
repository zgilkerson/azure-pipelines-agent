# YAML getting started - YAML triggers (not yet available, for discussion only)

## Schema

```yaml
ci:           # Simple syntax is an array of include-branches. We had previously discussed
- master      # "branches" but in this proposal "ci" is used more heavily:
- releases/*  # 1) "ci: false"
              # 2) full ci syntax (batch, branch include/exclude, path include/exclude
              # 3) on each artifact type
              #
              # So for consistency, "ci" might make more sense. Note, for TFVC "branches"
              # is not accurate terminology.
```

```yaml
ci: none # Turn off CI.
```

```yaml
ci:
  batch: bool | number
  branches:
    include: []
    exclude: []
  paths:
    include: []
    exclude: []

schedules:
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

resources:
  repositories:
  - name: tools
    ci: # Need a property to hang trigger info on
      branches:
        include: []
        exclude: []

  builds:
  - name: otherDefinition
    ci: true # Simple build trigger

  - name: otherDefinition2
    ci: # Build trigger with branch/tag filters
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
    ci: true # Packages too
```
