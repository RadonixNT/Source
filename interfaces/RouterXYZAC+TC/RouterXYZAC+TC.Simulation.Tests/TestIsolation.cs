using Xunit;

// LinkManager's process-wide UI registry and authorization state are UI-thread
// owned. Non-form fixtures also create Links, so a form-only collection does not
// isolate them from each other. Each test supplies its own STA thread as needed.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
