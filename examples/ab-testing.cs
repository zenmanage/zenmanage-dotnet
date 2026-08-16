using Zenmanage;
using Zenmanage.Contexting;

var zenmanage = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .Build());

var context = new Context(
    "user",
    name: "Jane Doe",
    identifier: "user-123",
    attributes: new[]
    {
        new Attribute("country", new[] { "US" }),
        new Attribute("ab_bucket", new[] { "42" })
    });

var variant = await zenmanage.Flags()
    .WithContext(context)
    .SingleAsync("checkout-flow", "control");

if (variant.AsString() == "one-page")
{
    Console.WriteLine("Render the one-page checkout flow.");
}
else
{
    Console.WriteLine("Render the control experience.");
}