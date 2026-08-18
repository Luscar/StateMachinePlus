using System.Text.Json;

namespace StateMachinePlus.Execution;

/// <summary>
/// Sac de variables associe a une instance de processus. Serialise en JSON pour la persistance
/// et accessible de maniere typee pendant l'execution.
/// </summary>
public sealed class VariablesProcessus
{
    private readonly Dictionary<string, object?> _valeurs;

    public VariablesProcessus() : this(new Dictionary<string, object?>())
    {
    }

    private VariablesProcessus(Dictionary<string, object?> valeurs)
    {
        _valeurs = valeurs;
    }

    public T? Obtenir<T>(string cle)
    {
        if (!_valeurs.TryGetValue(cle, out var valeur) || valeur is null)
        {
            return default;
        }

        if (valeur is T typee)
        {
            return typee;
        }

        if (valeur is JsonElement element)
        {
            return element.Deserialize<T>(SerialiseurJson.Options);
        }

        return (T)Convert.ChangeType(valeur, typeof(T));
    }

    public T ObtenirRequis<T>(string cle)
    {
        return Obtenir<T>(cle) ?? throw new InvalidOperationException(
            $"La variable de processus '{cle}' est absente ou nulle.");
    }

    public bool Contient(string cle) => _valeurs.ContainsKey(cle);

    public void Definir(string cle, object? valeur) => _valeurs[cle] = valeur;

    /// <summary>Fusionne les proprietes publiques (ou le dictionnaire) d'un objet dans les variables.</summary>
    public void FusionnerDepuis(object? donnees)
    {
        if (donnees is null)
        {
            return;
        }

        if (donnees is IDictionary<string, object?> dictionnaire)
        {
            foreach (var (cle, valeur) in dictionnaire)
            {
                _valeurs[cle] = valeur;
            }

            return;
        }

        var element = JsonSerializer.SerializeToElement(donnees, SerialiseurJson.Options);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var propriete in element.EnumerateObject())
        {
            _valeurs[propriete.Name] = propriete.Value.Clone();
        }
    }

    public string VersJson() => JsonSerializer.Serialize(_valeurs, SerialiseurJson.Options);

    public static VariablesProcessus DepuisJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new VariablesProcessus();
        }

        var valeurs = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, SerialiseurJson.Options)
                      ?? new Dictionary<string, object?>();
        return new VariablesProcessus(valeurs);
    }

    public static VariablesProcessus DepuisObjet(object? valeursInitiales)
    {
        var variables = new VariablesProcessus();
        variables.FusionnerDepuis(valeursInitiales);
        return variables;
    }
}

internal static class SerialiseurJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true,
    };
}
