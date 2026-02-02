using Microsoft.EntityFrameworkCore;
using SwipeService.Data;

namespace SwipeService.Tests.TestHelpers;

/// <summary>
/// Helper for managing test database lifecycle and ensuring clean state
/// </summary>
public class TestDatabaseHelper : IDisposable
{
    private readonly SwipeContext _context;
    private readonly string _databaseName;
    
    public SwipeContext Context => _context;

    public TestDatabaseHelper(string? testName = null)
    {
        _databaseName = $"SwipeTests_{testName ?? Guid.NewGuid().ToString()}";
        
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;

        _context = new SwipeContext(options);
    }

    /// <summary>
    /// Ensure clean database state before test
    /// </summary>
    public async Task EnsureCleanStateAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Get current row counts for debugging
    /// </summary>
    public async Task<Dictionary<string, int>> GetRowCountsAsync()
    {
        return new Dictionary<string, int>
        {
            ["Swipes"] = await _context.Swipes.CountAsync(),
            ["Matches"] = await _context.Matches.CountAsync()
        };
    }

    /// <summary>
    /// Verify database is empty (useful for cleanup validation)
    /// </summary>
    public async Task<bool> IsEmptyAsync()
    {
        var counts = await GetRowCountsAsync();
        return counts.Values.All(c => c == 0);
    }

    /// <summary>
    /// Reset database to clean state
    /// </summary>
    public async Task ResetAsync()
    {
        await EnsureCleanStateAsync();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
