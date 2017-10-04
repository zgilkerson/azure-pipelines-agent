# Task input aliases

## Goal

Provide a way for task authors to rename inputs

## Motivation

1. Task authors want to clean up their input names to improve readability

2. YAML exposes the input names to end users. The readability concern is no longer limited to task authors.

## Task.json changes

### Supported in new TFS

```json
  "inputs": [
      {
          "name": "goodName",
          "aliases": [ "ridiculouslyBadOldName" ],
          "type": "string"
      }
  ]
```

YAML intellisense would be driven off of `"name"` and not show aliases.

### Limitations

The proposed task.json changes would work for future servers only.

We have a problem since the task author can only communicate min-agent-demand, and not min-server-demand.

An alternative, congruent with today's min-demand model, would be to have a more complicated task.json schema and limit the change to agents.

```json
  "inputs": [
      {
          "name": "ridiculouslyBadOldName",
          "aliases": [
              "goodName",
              "anotherBadOldName"
          ],
          "type": "string"
      }
  ]
```

...and drive YAML intellisense based on the first alias.

The two big problems with approach are:

1. VSTS users are impacted by bumping min-agent-demand

2. Min-agent-demand should not be bumped within a major task version.

## Possible strategies for supporting the new property

Note for future consideration: Checked-in tasks will require agent changes.

### Modify REST API and plan construction only

Modify: REST API (create definition, read definition, update definition) and logic that constructs plan.

Advantages: Does not require min agent demand.

Disadvantages: Additional server load. Requires read definition to resolve the task instance.

### Modify designer UI and plan construction only

Advantages: Does not require min agent demand. Additional server load is trivial.

## Ruled-out strategies for supporting the new property

### Servicing

This would require that whenever a task definition is updated, all definitions are serviced.
