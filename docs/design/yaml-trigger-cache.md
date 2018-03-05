# YAML Trigger Caching

## Why cache?

When a ref is updated, the YAML file from the new commit is used to evaluate whether to trigger a build.

With GitHub, we need to stay under the throttling limits of 1,000 API calls per hour, per user.
Since we use service endpoints to talk to GitHub, this means throttling is tied back to the
user associated with the service endpoint.

Unauthenticated API calls to GitHub are not an option. Throttling rates for unauthenticated calls are
even lower, and are per source IP address.

In general, caching will also be helpful anytime the source provider is hosted in a separate service.
Currently Build is hosted in TFS with source control, so we do not leverage trigger caching for VSTS-Git.
However when RM implements triggers, the problem becomes more significant. And furthermore when Build
moves to a separate service, the problem also becomes more significant.

## Strategy overview

This strategy optimizes for eliminating calls to the source provider, for long chains of ref updates.

If the triggers are cached from the previous commit, and the YAML file has not changed, then we can reuse
the previous commit. We can extend this idea to apply to chains of commits.

A further optimization can be made to minimize the amount of data that needs to be retrieved, when
calls to the source provider are required. This approach would involve caching the GitHub tree objects,
and is discussed further in the appendix.

## Strategy details

### Push notification

When we receive a ref update, we want to avoid calling GitHub to retrieve the YAML file when possible.

We know the following relevant pieces of information from the push event:

```yaml
beforeSha: string
afterSha: string
is-force-push: bool
commits:
- files-added: [ string ] # file paths
  files-added: [ string ]
  files-removed: [ string ]
- ...
```

If the push notificaiton 


The problem we have is 

On push, based on the 

Cache the triggers section from the YAML file

## Only cache objects under 10k

## Appendix

Only cache objects under ___ kb
