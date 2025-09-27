using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using MauiAppBrownianMotion.Data;
using MauiAppBrownianMotion.Models;
using Xunit;

namespace MauiAppBrownianMotion.Tests.Repositories;

public class ClienteRepositoryTests
{
    //public ClienteRepositoryTests()
    //{

    //}

    [Fact]
    public async Task SaveAndRetrieveCliente()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        var repo = new ClienteRepository(
            NullLogger<ClienteRepository>.Instance,
            $"Data Source={tempDbPath}");

        await repo.DropTableAsync();

        var cliente = new Cliente
        {
            Name = "John",
            LastName = "Doe",
            Age = 30,
            Address = "123 Street"
        };

        var id = await repo.SaveItemAsync(cliente);
        Assert.True(id > 0);

        var list = await repo.ListAsync();
        Assert.Single(list);
        Assert.Equal("John", list[0].Name);

        ClearFile(tempDbPath);
    }

    [Fact]
    public async Task GetClienteByIdReturnsCorrectCliente()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        var repo = new ClienteRepository(
            NullLogger<ClienteRepository>.Instance,
            $"Data Source={tempDbPath}");

        await repo.DropTableAsync();

        var cliente = new Cliente
        {
            Name = "Jane",
            LastName = "Doe",
            Age = 25,
            Address = "456 Avenue"
        };

        var id = await repo.SaveItemAsync(cliente);
        var retrieved = await repo.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal("Jane", retrieved!.Name);

        ClearFile(tempDbPath);
    }

    [Fact]
    public async Task UpdateClientePersistsChanges()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        var repo = new ClienteRepository(
            NullLogger<ClienteRepository>.Instance,
            $"Data Source={tempDbPath}");

        await repo.DropTableAsync();

        var cliente = new Cliente
        {
            Name = "John",
            LastName = "Doe",
            Age = 30,
            Address = "123 Street"
        };

        await repo.SaveItemAsync(cliente);

        cliente.Name = "Johnny";
        await repo.SaveItemAsync(cliente);

        var updated = await repo.GetAsync(cliente.ID);

        Assert.NotNull(updated);
        Assert.Equal("Johnny", updated!.Name);

        ClearFile(tempDbPath);
    }

    [Fact]
    public async Task DeleteClienteRemovesRecord()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        var repo = new ClienteRepository(
            NullLogger<ClienteRepository>.Instance,
            $"Data Source={tempDbPath}");

        await repo.DropTableAsync();

        var cliente = new Cliente
        {
            Name = "John",
            LastName = "Doe",
            Age = 30,
            Address = "123 Street"
        };

        await repo.SaveItemAsync(cliente);

        var rows = await repo.DeleteItemAsync(cliente);
        Assert.Equal(1, rows);

        var list = await repo.ListAsync();
        Assert.Empty(list);

        ClearFile(tempDbPath);
    }

    private static void ClearFile(string tempDbPath)
    {
        // Ensure all connections are closed before deleting the file
        SqliteConnection.ClearAllPools();

        if (File.Exists(tempDbPath))
        {
            File.Delete(tempDbPath);
        }
    }
}