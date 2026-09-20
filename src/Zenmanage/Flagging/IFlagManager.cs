using Zenmanage.Contexting;
using Zenmanage.Defaults;

namespace Zenmanage.Flagging;

/// <summary>
/// Public contract for flag evaluation.
/// </summary>
public interface IFlagManager
{
    Task<IReadOnlyList<Flag>> AllAsync(CancellationToken cancellationToken = default);

    Task<Flag> SingleAsync(string key, object? defaultValue = null, CancellationToken cancellationToken = default);

    IFlagManager WithContext(Context context);

    IFlagManager WithDefaults(DefaultsCollection defaults);

    Task RefreshRulesAsync(CancellationToken cancellationToken = default);
}