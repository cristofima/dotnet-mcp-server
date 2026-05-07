using McpServer.BackendApi.Data.Entities;

namespace McpServer.BackendApi.Data;

/// <summary>
/// Seeds the database with initial demo data.
/// All timestamps are fixed to ensure deterministic, reproducible data across runs.
/// Safe to call on every startup — each method checks whether data already exists.
/// </summary>
public static class DbSeeder
{
    private const string DEMO_USER_ID = "demo-user";

    // Fixed reference point for all seeded timestamps — deterministic across every startup.
    private static readonly DateTimeOffset SeedDate = new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);

    public static void SeedData(MockApiDbContext context)
    {
        SeedTasks(context);
        SeedProjects(context);
        SeedBalances(context);
        context.SaveChanges();
    }

    private static void SeedTasks(MockApiDbContext context)
    {
        if (context.Tasks.Any())
        {
            return;
        }

        var tasks = new[]
        {
            new TaskEntity
            {
                Id = "task-001",
                UserId = DEMO_USER_ID,
                Title = "Review MCP Server implementation",
                Description = "Review the architecture and security of the MCP Server",
                Priority = "High",
                Status = "In Progress",
                CreatedAt = SeedDate.AddDays(-3),
            },
            new TaskEntity
            {
                Id = "task-002",
                UserId = DEMO_USER_ID,
                Title = "Write documentation",
                Description = "Create comprehensive documentation for the MCP Baseline project",
                Priority = "Medium",
                Status = "Pending",
                CreatedAt = SeedDate.AddDays(-1),
            },
            new TaskEntity
            {
                Id = "task-003",
                UserId = DEMO_USER_ID,
                Title = "Set up CI/CD pipeline",
                Description = "Configure GitHub Actions for automated testing and deployment",
                Priority = "High",
                Status = "Completed",
                CreatedAt = SeedDate.AddDays(-7),
                CompletedAt = SeedDate.AddDays(-2),
            },
        };

        context.Tasks.AddRange(tasks);
    }

    private static void SeedProjects(MockApiDbContext context)
    {
        if (context.Projects.Any())
        {
            return;
        }

        var projects = new[]
        {
            new ProjectEntity
            {
                Id = "PRJ001",
                Name = "Project Alpha",
                Status = "Active",
                Budget = 150000,
                Manager = "John Doe",
                StartDate = SeedDate.AddMonths(-3),
                TeamMembers = "Alice,Bob,Charlie",
            },
            new ProjectEntity
            {
                Id = "PRJ002",
                Name = "Project Beta",
                Status = "Planning",
                Budget = 75000,
                Manager = "Jane Smith",
                StartDate = SeedDate.AddMonths(-6),
                TeamMembers = "Diana,Eve",
            },
            new ProjectEntity
            {
                Id = "PRJ003",
                Name = "Project Gamma",
                Status = "Completed",
                Budget = 200000,
                Manager = "Bob Wilson",
                StartDate = SeedDate.AddYears(-1),
                TeamMembers = "Frank,Grace,Henry",
            },
        };

        context.Projects.AddRange(projects);
    }

    private static void SeedBalances(MockApiDbContext context)
    {
        if (context.Balances.Any())
        {
            return;
        }

        var balances = new[]
        {
            new BalanceEntity
            {
                ProjectNumber = "PRJ001",
                Allocated = 150000.00m,
                Spent = 92500.75m,
                Remaining = 57499.25m,
                Committed = 18000.00m,
                Available = 39499.25m,
                Currency = "USD",
                LastUpdated = SeedDate.AddHours(-2),
            },
            new BalanceEntity
            {
                ProjectNumber = "PRJ002",
                Allocated = 75000.00m,
                Spent = 12000.00m,
                Remaining = 63000.00m,
                Committed = 5000.00m,
                Available = 58000.00m,
                Currency = "USD",
                LastUpdated = SeedDate.AddHours(-5),
            },
            new BalanceEntity
            {
                ProjectNumber = "PRJ003",
                Allocated = 200000.00m,
                Spent = 198450.50m,
                Remaining = 1549.50m,
                Committed = 0.00m,
                Available = 1549.50m,
                Currency = "USD",
                LastUpdated = SeedDate.AddDays(-30),
            },
        };

        context.Balances.AddRange(balances);
    }
}

