namespace EPFOptimizerPro.Services;

public static class AuditDeadCodeInfoProvider
{
    public static string Build()
    {
        return
            "CODE MORT - FONCTION EXPERIMENTALE" + Environment.NewLine +
            Environment.NewLine +
            "Aucune suppression automatique n'est effectuee." + Environment.NewLine +
            Environment.NewLine +
            "Une future analyse pourra signaler des elements potentiellement inutilises :" + Environment.NewLine +
            "- methodes privees non referencees ;" + Environment.NewLine +
            "- classes non referencees ;" + Environment.NewLine +
            "- handlers XAML potentiellement inutilises ;" + Environment.NewLine +
            "- services crees mais jamais appeles ;" + Environment.NewLine +
            "- fichiers .bak et temporaires ;" + Environment.NewLine +
            "- marqueurs TODO et FIXME." + Environment.NewLine +
            Environment.NewLine +
            "Attention : les appels via XAML, reflection ou serialisation peuvent produire de faux positifs.";
    }
}