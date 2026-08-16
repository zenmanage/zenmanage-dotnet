using Zenmanage;
using Zenmanage.Defaults;

var defaults = new DefaultsCollection()
    .Add("new-ui", true)
    .Add("api-version", "v2")
    .Add("max-items", 100);

var zenmanage = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .Build());

var defaultedFlag = await zenmanage.Flags()
    .WithDefaults(defaults)
    .SingleAsync("new-ui");

var inlineDefault = await zenmanage.Flags()
    .SingleAsync("missing-flag", "fallback-value");

Console.WriteLine($"new-ui: {defaultedFlag.IsEnabled()}");
Console.WriteLine($"missing-flag: {inlineDefault.AsString()}");