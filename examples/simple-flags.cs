using Zenmanage;

var zenmanage = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .Build());

var enabled = await zenmanage.Flags().SingleAsync("new-dashboard", false);
var welcomeText = await zenmanage.Flags().SingleAsync("welcome-text", "Welcome!");
var maxUploadSize = await zenmanage.Flags().SingleAsync("max-upload-mb", 10);

Console.WriteLine($"new-dashboard enabled: {enabled.IsEnabled()}");
Console.WriteLine($"welcome-text: {welcomeText.AsString()}");
Console.WriteLine($"max-upload-mb: {maxUploadSize.AsNumber()}");

var allFlags = await zenmanage.Flags().AllAsync();
foreach (var flag in allFlags)
{
    Console.WriteLine($"{flag.Key}: {flag.GetValue()}");
}