# Toggl Focus (2.0) replaces Toggl Track as the only source

Time tracking moved to Toggl Focus, and the Track Reports API v2 this tool depended on is deprecated, so we replaced the Track integration outright instead of keeping both behind a config switch; one source means one auth path and no dead code. Consequence: existing Track users must stay on v2.x of this tool, and v3.0.0 reads raw Time Entries from Focus (`time-entries/stream`) and aggregates them locally, because the Focus reports query API has no documented grouping properties.

## Consequences

- An API key cannot discover its organization, so `t2v credentials` asks for the organization ID and stores it together with the API key.
- The Focus API has no user filter; the tool aborts when entries from more than one user are returned rather than risk writing someone else's time into Vertec.
