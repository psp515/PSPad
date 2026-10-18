namespace PSPad.Module.Identity.Provisioning;

public static class ProvisioningPlan
{
    static readonly IReadOnlyList<string> English = ["Personal", "Work", "Home"];
    static readonly IReadOnlyList<string> Polish = ["Prywatne", "Praca", "Dom"];

    public static IReadOnlyList<string> SeedAreaNames(string language) =>
        language == "pl" ? Polish : English;
}
