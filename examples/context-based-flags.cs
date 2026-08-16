using Zenmanage;
using Zenmanage.Contexting;

var zenmanage = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .Build());

var userContext = new Context(
    "user",
    name: "Jane Doe",
    identifier: "user-123",
    attributes: new[]
    {
        new Attribute("country", new[] { "US" }),
        new Attribute("plan", new[] { "premium" })
    });

var organizationContext = new Context(
    "organization",
    name: "Acme Corp",
    identifier: "org-123",
    attributes: new[]
    {
        new Attribute("tier", new[] { "enterprise" })
    });

var betaFlag = await zenmanage.Flags()
    .WithContext(userContext)
    .SingleAsync("beta-program", false);

var analyticsFlag = await zenmanage.Flags()
    .WithContext(organizationContext)
    .SingleAsync("advanced-analytics", false);

Console.WriteLine($"beta-program: {betaFlag.IsEnabled()}");
Console.WriteLine($"advanced-analytics: {analyticsFlag.IsEnabled()}");