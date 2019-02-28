# BYOS: Bring Your Own Subscription Agent Pools

Hosted agents are extremely convenient: low/no cost, no infrastructure to maintain, and elastic with demand.
In return, customers must give up control over tools, capacity, and speed.
On the other end of the spectrum, private agents offer the exact opposite set of trade-offs: full control over everything, at the expense of maintaining and paying for infrastructure.
With private agents, elasticity is difficult to achieve.

Bring Your Own Subscription (BYOS) represents a middle ground:
it pairs the convenience and elastic capacity of the hosted pool with the control and flexibility of private agents.
**Azure Pipelines will manage a set of build/release agents to the customer's specification, completely automated, in the customer's Azure subscription.**

## State of this spec

This is in the early design phase and we are looking for feedback in the PR (or as issues in the repo).

## Customer scenarios

The theme throughout these scenarios is that the customer wants hosted elasticity but customization beyond what Hosted offers.

General themes are around:
- VM specs (memory, CPU, disk) and network environment
- Preinstalled software
- Customizing elasticity

### VM specs and environment

1. Customer wants more memory, more processor, or more IO than our native images.
2. [Customer](https://github.com/MicrosoftDocs/vsts-docs/issues/2985) wants an NCv2 VM with particular instruction sets for machine learning. (It's niche enough that we won't stand up dedicated hosted pools, but broad enough to be very interesting for our business.)
3. Customer wants additional storage attached to the VM. *(Real scenario from a medium-sized customer)*
4. Customer wants their own network topology or Active Directory configuration.

### Preinstalled software

1. Customer wants Windows 2016 with VS 2019 Preview. (We only offer certain combos like Win2016 + VS2017 and Win2019 + VS2019.)
2. Customer wants to pin a specific set of tools and dependencies, preconfigured on the image.
3. Customer wants extreme control over the exact OS build, settings, versions, and so on.

### Custom elasticity

1. Customer wants to run several consecutive jobs on an agent to take advantage of things like incremental source and machine-level package caches. But, they don't want to run unnecessary VMs overnight when there's no load. They want to specify minimum and maximum # of agents associated with time ranges.
2. Customer wants to run additional configuration or cache warmup before an agent beings accepting jobs. As additional agents are spun up, the customer has an opportunity to run a prep script that doesn't impact "pipeline runtime".

## Alternatives considered

### Custom / configurable SKU

Offering additional Azure SKUs in Hosted will meet some of the above scenarios.
Specifically, if all you need are additional resources (memory, compute, etc.), then a custom SKU works.

Pros:
- Easiest for the customer to understand
- Potential business model around premium SKUs

Cons:
- Capacity planning and buildout becomes a lot more complex
- If a customer wants a SKU we don't offer, they're out of luck until we add it
- Does not offer custom elasticity, network environment, or image

### Bring your own cloud (pool providers)

Putting the entire agent alloc/release cycle in the hands of the customer offers them ultimate flexibility.

Pros:
- Maximum flexibility
- Customer can select other infrastructure: on-prem, in a different hosting provider, etc.

Cons:
- Much heavier burden to understand, set up, and maintain
- Customer has to swallow full complexity load to get any flexibility

## "Clickstops" along the way

Of course, we won't want to do a huge multi-month effort and then dump it on the community at once.
We can deliver several intermediate levels of value on the way to a full solution.
At every point, we can re-evaluate if the decisions we're making match customer needs at the time.

**Stage 0**. Using a customer's Azure subscription, run pipelines against our native images using Hosted-like "throwaway" VMs.
The customer tells us exactly how many agents to keep around.
The benefits in this stage primarily go to us: we gain experience running a multi-tenant service against subscriptions we don't own.
This would be a private preview rather than a generally available feature.

**Stage 1**. Offer the ability to keep a Hosted-like agent alive for multiple consecutive runs.
The customer gives us a set of rules: minimum and maximum agents available, times of day where those change, and how long a given agent is allowed to live before being recycled.
This adds real value for customers, and from their perspective, is likely the minimum viable product.
Think public preview.

**Stage 2**. Customer can give us an ARM template describing the resources they want.
This will include at least VM SKUs, VM images, and network config.
There are likely to be lots of exclusions and gaps at this point, but we're clearly ready to GA the feature.

**Beyond**. Additional goodies like warmup scripts (run once when the agent is spun up, not counted towards the time of the first pipeline that happens to run on a fresh agent), more flexible rules, and relaxing any restrictions we placed on ARM templates.

<!--
## Goals

- **Fully automated dedicated agents with elasticity**: User configures contraints and we provision, start and stop the agents.
- **Customer control of image and toolsets**: Pick the image to use.  Stay on it until you change the configuration.  Use our published images that we release monthly.
- **Control machine configurations**: User can provide VM SKU and other configuration options (provide ARM).
- **Control agent lifetime**: Agents can be single use, or thrown away on a configured interval (nightly, etc).
- **Incremental sources and packages**: Even if you choose single use, we can warm up YAML run when bringing VM online. 
- **Cached container images on host**: Ensure a set of container images are cached on the host via warmup YAML.
- **Maintenance**: Schedule maintenance jobs for pruning repos, OS security updates, etc.
- **Elastic pools for VSTS and On-prem**:  Use elastic Azure compute as build resources for VSTS but also on-prem TFS.
- **Allow domain joined and on-prem file shares**: Leverage AAD and Express Routes for elastic on-prem scenarios.
- **Configure multiple pools of type BYOS**: Allows for budgeting of resources across larger enterprise teams.
- **Control costs**: Stop agents when not in use to control Azure charges

## Design

Pending on goals discussions.
-->
