using Xunit;

// Both integration test classes truncate the same mkn_test database in
// their IAsyncLifetime.InitializeAsync. Running the classes in parallel
// causes one class's TRUNCATE to land between another class's parent
// insert and child insert, producing FK violations and nondeterministic
// assertion failures that are not defects in the code under test.
//
// Disabling parallelisation at the assembly level forces xUnit to run
// the test classes sequentially. Each class then sees a clean mkn_test
// from its own InitializeAsync, which is the arrangement the tests
// assume.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
