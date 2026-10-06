using CitadelIQ.Application.Options;
using CitadelIQ.Infrastructure;
using CitadelIQ.Infrastructure.Storage;
using CitadelIQ.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CitadelIQ.Tests.Unit;

public class StorageOptionsTests
{
    private static bool Valid(StorageOptions o) => new StorageOptionsValidator().Validate(null, o).Succeeded;

    private static StorageOptions Azure(Action<AzureBlobOptions> configure)
    {
        var o = new StorageOptions { Provider = StorageOptions.AzureBlobProvider, AzureBlob = { AccountName = "acct", ContainerName = "documents" } };
        configure(o.AzureBlob);
        return o;
    }

    [Fact]
    public void Defaults_and_valid_azure_configurations_pass()
    {
        Assert.True(Valid(new StorageOptions()));
        Assert.True(Valid(Azure(_ => { })));
        Assert.True(Valid(Azure(a => { a.AccountName = ""; a.ConnectionString = "UseDevelopmentStorage=true"; })));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Documents")]
    [InlineData("my_docs")]
    [InlineData("-docs")]
    [InlineData("docs-")]
    [InlineData("do--cs")]
    [InlineData("")]
    public void Invalid_container_names_fail(string name) => Assert.False(Valid(Azure(a => a.ContainerName = name)));

    [Fact]
    public void Azure_without_account_or_connection_string_and_unknown_providers_fail()
    {
        Assert.False(Valid(Azure(a => a.AccountName = "")));
        Assert.False(Valid(new StorageOptions { Provider = "S3" }));
    }

    [Fact]
    public void Provider_selection_registers_the_matching_storage_and_unknown_fails_fast()
    {
        IServiceCollection Build(string provider)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CitadelIQ"] = "Host=x", ["Storage:Provider"] = provider
            }).Build();
            return new ServiceCollection().AddInfrastructure(config);
        }

        Assert.Equal(typeof(LocalDiskDocumentStorage), Build("LocalDisk").Single(d => d.ServiceType == typeof(IDocumentStorage)).ImplementationType);
        Assert.Contains(Build("AzureBlob"), d => d.ServiceType == typeof(IStorageProbe));
        Assert.Throws<InvalidOperationException>(() => Build("S3"));
    }
}
