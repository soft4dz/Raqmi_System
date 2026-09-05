using RaqmiSystem.Domain.Catalog;

namespace RaqmiSystem.Tests;

/// <summary>
/// Les regles de l'article vendable : code normalise, taux de TVA pris au domaine Facturation,
/// prix a deux decimales, et lien vers le stock tout ou rien.
/// </summary>
public sealed class CatalogTests
{
    [Fact]
    public void Un_article_normalise_son_code_et_prend_ses_valeurs_par_defaut()
    {
        var article = new Article(" nuit-dbl ", " Nuitee chambre double ", "nuit", 9m, 12_500.00m, " Hebergement ");

        Assert.Equal("NUIT-DBL", article.Code);
        Assert.Equal("Nuitee chambre double", article.Designation);
        Assert.Equal("Hebergement", article.Family);
        Assert.Equal(9m, article.VatRate);
        Assert.Equal(12_500.00m, article.UnitPriceExclVat);
        Assert.False(article.TracksStock);
        Assert.Null(article.StockItemCode);
        Assert.True(article.IsActive);
    }

    [Fact]
    public void Le_taux_de_tva_est_celui_du_domaine_facturation()
    {
        // 7 % n'est pas un taux algerien : la ligne de facture le refuserait, l'article aussi.
        Assert.Throws<ArgumentException>(() => new Article("ART", "Article", "piece", 7m, 100m));

        Assert.Equal(0m, new Article("ART", "Article", "piece", 0m, 100m).VatRate);
        Assert.Equal(19m, new Article("ART", "Article", "piece", 19m, 100m).VatRate);
    }

    [Fact]
    public void Le_prix_de_vente_est_positif_ou_nul_a_deux_decimales()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Article("ART", "Article", "piece", 19m, -1m));
        Assert.Throws<ArgumentException>(() => new Article("ART", "Article", "piece", 19m, 10.555m));

        // Un article gratuit (offert, inclus) est legitime.
        Assert.Equal(0m, new Article("ART", "Article", "piece", 19m, 0m).UnitPriceExclVat);
    }

    [Fact]
    public void Le_lien_vers_le_stock_est_tout_ou_rien()
    {
        // Suivi sans article de stock : la vente ne saurait pas d'ou sortir.
        Assert.Throws<ArgumentException>(() =>
            new Article("COCA", "Coca-Cola 33cl", "piece", 19m, 250m, tracksStock: true));

        // Non suivi mais pointant vers un stock : une sortie qui n'aura jamais lieu.
        Assert.Throws<ArgumentException>(() =>
            new Article("COCA", "Coca-Cola 33cl", "piece", 19m, 250m, tracksStock: false, stockItemCode: "boi-coca"));

        var tracked = new Article("COCA", "Coca-Cola 33cl", "piece", 19m, 250m, tracksStock: true, stockItemCode: " boi-coca ");

        Assert.True(tracked.TracksStock);
        Assert.Equal("BOI-COCA", tracked.StockItemCode);
    }

    [Fact]
    public void La_mise_a_jour_reapplique_toutes_les_regles()
    {
        var article = new Article("SPA", "Acces spa", "heure", 19m, 3_000m);

        article.UpdateDetails("Acces spa 2h", "forfait", 9m, 5_000m, "Bien-etre", tracksStock: false, stockItemCode: null);

        Assert.Equal("Acces spa 2h", article.Designation);
        Assert.Equal(9m, article.VatRate);
        Assert.Equal(5_000m, article.UnitPriceExclVat);
        Assert.Equal("Bien-etre", article.Family);

        Assert.Throws<ArgumentException>(() =>
            article.UpdateDetails("Acces spa 2h", "forfait", 12m, 5_000m, null, tracksStock: false, stockItemCode: null));

        article.Deactivate();
        Assert.False(article.IsActive);

        article.Activate();
        Assert.True(article.IsActive);
    }
}
