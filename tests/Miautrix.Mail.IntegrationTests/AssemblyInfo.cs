using Xunit;

// The integration tests share a single real PostgreSQL instance (never mocked, per
// project rules). Parallel test classes would race each other's seeded tenants and
// queue rows — an outbound dispatch pass from one test can sweep up another test's
// Pending row. Serialize the suite so each test owns the database state it seeds.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
