using Xunit;

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestCollection
{
    // Collection definition to disable parallelization for integration tests that
    // exercise global app configuration and authentication state.
}
