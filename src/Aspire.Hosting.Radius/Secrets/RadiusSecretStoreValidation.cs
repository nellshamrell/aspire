// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS006 // Secret-store types are experimental; consumed internally by the integration.
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Publishing;

namespace Aspire.Hosting.Radius.Secrets;

/// <summary>
/// Config-time (pre-publish/deploy) validation of Radius secret stores. Runs as a
/// fail-fast gate that is <c>RequiredBy</c> both publish and deploy, so type/mode/key/
/// encoding/duplicate-name failures surface before any Bicep is emitted or
/// <c>kubectl</c>/<c>rad</c> is contacted. Store-name grammar and empty/whitespace keys
/// are rejected earlier, at the builder call site (FR-005a). No-op when the model
/// declares no secret stores (the feature is inactive; the default path is unchanged).
/// </summary>
internal static class RadiusSecretStoreValidation
{
    /// <summary>Pipeline-step entry point. No-ops in Run mode.</summary>
    internal static Task ValidateAsync(PipelineStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ExecutionContext.IsRunMode)
        {
            return Task.CompletedTask;
        }

        Validate(context.Model);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates every declared secret store over the whole application model.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A required key is missing (<c>ASPIRERADIUS040</c>), the population mode count is not
    /// exactly one (<c>ASPIRERADIUS041</c>), an inline key binds a non-secret parameter
    /// (<c>ASPIRERADIUS042</c>), a duplicate key is declared (<c>ASPIRERADIUS043</c>), an
    /// invalid encoding is set for the type (<c>ASPIRERADIUS047</c>), or two stores share a
    /// name within the same scope (<c>ASPIRERADIUS048</c>).
    /// </exception>
    internal static void Validate(DistributedApplicationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var stores = model.Resources.OfType<RadiusSecretStoreResource>().ToList();
        if (stores.Count == 0)
        {
            return;
        }

        foreach (var store in stores)
        {
            ValidateStore(store);
        }

        ValidateNoDuplicateNames(stores);
    }

    private static void ValidateStore(RadiusSecretStoreResource store)
    {
        var population = store.Population;

        // ASPIRERADIUS041 — exactly one population mode.
        if (population.DeclaredModeCount != 1)
        {
            throw new InvalidOperationException(
                $"Secret store '{store.Name}' must declare exactly one population mode " +
                "(WithData, FromExistingSecret, or FromSealedSecret); it declares " +
                $"{population.DeclaredModeCount}. Diagnostic: ASPIRERADIUS041.");
        }

        var declaredKeys = population.HasInlineData
            ? population.Data.Keys.ToList()
            : population.Keys;

        // ASPIRERADIUS043 — duplicate keys (only possible for the existing/sealed key list;
        // inline keys are a dictionary and cannot collide).
        if (population.IsSecretReference)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in population.Keys)
            {
                if (!seen.Add(key))
                {
                    throw new InvalidOperationException(
                        $"Secret store '{store.Name}' declares the key '{key}' more than once. " +
                        "Diagnostic: ASPIRERADIUS043.");
                }
            }
        }

        // ASPIRERADIUS040 — type-aware required keys.
        foreach (var required in store.Type.RequiredKeys())
        {
            if (!declaredKeys.Contains(required, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Secret store '{store.Name}' of type '{store.Type.ToRadiusTypeString()}' is missing " +
                    $"the required key '{required}'. Diagnostic: ASPIRERADIUS040.");
            }
        }

        // ASPIRERADIUS042 / ASPIRERADIUS047 — inline bindings must be secret and use valid encoding.
        if (population.HasInlineData)
        {
            foreach (var (key, binding) in population.Data)
            {
                if (!binding.Parameter.Secret)
                {
                    throw new InvalidOperationException(
                        $"Secret store '{store.Name}' binds key '{key}' to the non-secret parameter " +
                        $"'{binding.Parameter.Name}'. Bind a parameter created with secret: true. " +
                        "Diagnostic: ASPIRERADIUS042.");
                }

                if (binding.Encoding is not null && !store.Type.IsValidEncoding(binding.Encoding))
                {
                    throw new InvalidOperationException(
                        $"Secret store '{store.Name}' sets encoding '{binding.Encoding}' on key '{key}', which is " +
                        $"invalid for a '{store.Type.ToRadiusTypeString()}' store. Diagnostic: ASPIRERADIUS047.");
                }
            }
        }
    }

    private static void ValidateNoDuplicateNames(IReadOnlyList<RadiusSecretStoreResource> stores)
    {
        // Aspire already enforces unique resource names, so exact-name collisions cannot occur.
        // Two distinct store names can still sanitize to the same Bicep identifier (e.g. 'db-creds'
        // and 'db.creds' both become 'db_creds'), which would emit duplicate resource symbols.
        // Detect that within a scope (mirrors ASPIRERADIUS028 for recipe parameters).
        var seen = new Dictionary<string, RadiusSecretStoreResource>(StringComparer.Ordinal);
        foreach (var store in stores)
        {
            var identifier = BicepPostProcessor.SanitizeIdentifier(store.Name);
            var scopeKey = store.Scope == RadiusSecretStoreScope.Environment
                ? $"env:{store.OwningEnvironment?.Name}:{identifier}"
                : $"app:{identifier}";

            if (seen.TryGetValue(scopeKey, out var existing))
            {
                throw new InvalidOperationException(
                    $"Secret stores '{existing.Name}' and '{store.Name}' map to the same Bicep identifier " +
                    $"'{identifier}' within the same scope. Rename one so they produce distinct identifiers. " +
                    "Diagnostic: ASPIRERADIUS048.");
            }

            seen[scopeKey] = store;
        }
    }
}
