using Zenmanage;
using Zenmanage.Contexting;

var zenmanage = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .Build());

var context = Context.Single("user", "user-123", "Jane Doe");

var rolloutFlag = await zenmanage.Flags()
    .WithContext(context)
    .SingleAsync("new-checkout-flow", false);

Console.WriteLine($"Is user in rollout: {rolloutFlag.IsEnabled()}");

var variantFlag = await zenmanage.Flags()
    .WithContext(context)
    .SingleAsync("checkout-flow-variant", "control");

Console.WriteLine($"Assigned rollout variant: {variantFlag.AsString()}");