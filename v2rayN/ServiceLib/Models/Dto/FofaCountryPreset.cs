namespace ServiceLib.Models.Dto;

public class FofaCountryPreset
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Priority { get; set; }

    public string DisplayName => Priority ? $"★ {Name} ({Code})" : $"{Name} ({Code})";

    public override string ToString() => DisplayName;
}

public static class FofaCountryPresets
{
    // Priority countries requested by the user, kept in this exact order and starred; the rest follow alphabetically by name.
    private static readonly (string Code, string Name)[] _priority =
    [
        ("IR", "Iran"),
        ("US", "United States"),
        ("TR", "Turkey"),
        ("DE", "Germany"),
        ("GB", "United Kingdom"),
        ("CN", "China"),
    ];

    private static readonly (string Code, string Name)[] _others =
    [
        ("FR", "France"),
        ("NL", "Netherlands"),
        ("CA", "Canada"),
        ("AE", "United Arab Emirates"),
        ("RU", "Russia"),
        ("JP", "Japan"),
        ("KR", "South Korea"),
        ("IN", "India"),
        ("BR", "Brazil"),
        ("AU", "Australia"),
        ("IT", "Italy"),
        ("ES", "Spain"),
        ("SE", "Sweden"),
        ("CH", "Switzerland"),
        ("SG", "Singapore"),
        ("HK", "Hong Kong"),
        ("TW", "Taiwan"),
        ("PL", "Poland"),
        ("FI", "Finland"),
        ("NO", "Norway"),
        ("DK", "Denmark"),
        ("BE", "Belgium"),
        ("AT", "Austria"),
        ("IE", "Ireland"),
        ("PT", "Portugal"),
        ("GR", "Greece"),
        ("CZ", "Czechia"),
        ("RO", "Romania"),
        ("UA", "Ukraine"),
        ("IL", "Israel"),
        ("SA", "Saudi Arabia"),
        ("EG", "Egypt"),
        ("ZA", "South Africa"),
        ("MX", "Mexico"),
        ("AR", "Argentina"),
        ("ID", "Indonesia"),
        ("MY", "Malaysia"),
        ("TH", "Thailand"),
        ("VN", "Vietnam"),
        ("PH", "Philippines"),
        ("PK", "Pakistan"),
        ("IQ", "Iraq"),
        ("KW", "Kuwait"),
        ("QA", "Qatar"),
        ("BH", "Bahrain"),
        ("OM", "Oman"),
        ("JO", "Jordan"),
        ("AZ", "Azerbaijan"),
        ("GE", "Georgia"),
        ("AM", "Armenia"),
        ("KZ", "Kazakhstan"),
    ];

    public static List<FofaCountryPreset> All { get; } =
        [
            .. _priority.Select(c => new FofaCountryPreset { Code = c.Code, Name = c.Name, Priority = true }),
            .. _others.OrderBy(c => c.Name).Select(c => new FofaCountryPreset { Code = c.Code, Name = c.Name, Priority = false }),
        ];

    public static FofaCountryPreset? FindByCode(string? code)
    {
        if (code.IsNullOrEmpty())
        {
            return null;
        }
        return All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}
