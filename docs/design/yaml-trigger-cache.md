# YAML Trigger Caching

## Why cache?

When a ref is updated, the YAML file from the new commit is used to evaluate whether to trigger a build.

With GitHub, we need to stay under the throttling limits of 5,000 API calls per hour, per user.
Since we use service endpoints to talk to GitHub, this means throttling is tied back to the
user associated with the service endpoint.

Unauthenticated API calls to GitHub are not an option. Throttling rates for unauthenticated calls are
60 requests per hour, per originating IP address.

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

When we receive a ref update notification, we want to avoid calling GitHub to retrieve the
YAML file when possible.

The push event contains the following relevant information:

```yaml
ref-name: string
before-sha: string
after-sha: string
is-force-push: bool
commits:
- files-added: [ string ] # file paths
  files-removed: [ string ]
  files-changed: [ string ]
- ...
- ...
```

Based on the information in the push event, we can determine whether the triggers changed.

The triggers from the before sha can be reused when:

- The before sha is not empty
- is-force-push == false
- No .yml files were added, removed, or changed

### What to cache

The triggers section of the file will be cached using the following information:

```yaml
lookup-key:
  repo-url: string
  commit: string
  file-path: string

value: "<TRIGGER_OBJECT>"
```

When a push notification can reuse the triggers from the before sha, cache a
redirect mapping from the after sha, to the before sha. Caching the redirect
enables chains of redirects. The redirect would leverage the following information:

```yaml
lookup-key:
  repo-url: string
  commit: string # this is the after-sha from the push event
  file-path: string

value:
  commit: string # this is the before-sha from the push event
```

When a push notification can reuse the triggers from the before sha, which
in turn reuses the triggers from it's before sha, then cache a redirect mapping
from the after sha, to the final before sha. The cached information would use
the following:

```yaml
lookup-key:
  repo-url: string
  commit: string # this is the after-sha from the push event
  file-path: string

value:
  commit: string # this is the before-sha's before-sha
```

### Example walkthrough

Consider the following push event:

```yaml
refName: refs/heads/master
beforeSha: ""
afterSha: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
```

After processing the first push event, the state of the caches would look like:

```yaml
triggers:
  "<REPO_URL>/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": "<TRIGGER_OBJECT>"

redirects: []
```

Second push event:

```yaml
refName: refs/heads/master
beforeSha: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
afterSha: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
```

After processing the second push event, the state of the caches would look like:

```yaml
triggers:
  "<REPO_URL>/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": "<TRIGGER_OBJECT>"

redirects:
- "<REPO_URL>/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/my.yml": aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
```

Third push event:

```yaml
refName: refs/heads/master
beforeSha: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
afterSha: cccccccccccccccccccccccccccccccccccccccc
```

After processing the third push event, the state of the caches would look like:

```yaml
triggers:
  "<REPO_URL>/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": "<TRIGGER_OBJECT>"

redirects:
- "<REPO_URL>/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/my.yml": aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa # both point directly to aaaaaaaa
- "<REPO_URL>/cccccccccccccccccccccccccccccccccccccccc/my.yml": aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
```

### Further optimization - cache the before sha

When we receive a push event for an uncached sha, a further optimization would be to
cache the triggers for the before sha, and cache a redirect for the after sha.

For example, consider a scenario where the caches are initially empty, and the following
push event is received:

```yaml
refName: refs/heads/users/johndoe/bugfix
beforeSha: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa # matches current refs/heads/master
afterSha: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
```

After processing the first push event, the state of the caches would look like:

```yaml
triggers:
  "<REPO_URL>/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": "<TRIGGER_OBJECT>"

redirects:
- "<REPO_URL>/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/my.yml": aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
```

Second push event:

```yaml
refName: refs/heads/users/janedoe/feature
beforeSha: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa # matches current refs/heads/master
afterSha: cccccccccccccccccccccccccccccccccccccccc
```

```yaml
triggers:
  "<REPO_URL>/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": "<TRIGGER_OBJECT>"

redirects:
- "<REPO_URL>/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/my.yml": aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa # both point directly to aaaaaaaa
- "<REPO_URL>/cccccccccccccccccccccccccccccccccccccccc/my.yml": aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
```

## Appendix

### Notes about caching

Only cache objects under ___ kb

### Caching the git trees