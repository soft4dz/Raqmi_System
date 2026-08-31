using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Tests;

/// <summary>
/// Domain coverage of the kitchen module: the recipe sheet and its ingredient lines, the HACCP
/// checkpoint and its compliance range, and - the rule this module exists for - the compliance
/// verdict FROZEN on a temperature reading, which a later threshold change must never rewrite.
/// </summary>
public sealed class KitchenTests
{
    // ============================== Recipe sheets ==============================

    [Fact]
    public void Recipe_sheet_normalizes_its_code_and_trims_its_free_text_fields()
    {
        var recipe = new RecipeSheet(
            "  ft-couscous  ",
            "  Couscous royal  ",
            RecipeCategory.Plat,
            yieldPortions: 8,
            allergens: "  gluten, céleri  ",
            instructions: "  Cuire la semoule à la vapeur.  ");

        Assert.Equal("FT-COUSCOUS", recipe.Code);
        Assert.Equal("Couscous royal", recipe.Name);
        Assert.Equal("gluten, céleri", recipe.Allergens);
        Assert.Equal("Cuire la semoule à la vapeur.", recipe.Instructions);
        Assert.True(recipe.IsActive);
    }

    /// <summary>
    /// The yield divides the material cost: a recipe yielding zero (or a negative number of)
    /// portions would make the cost per portion either undefined or absurd, so it is refused at
    /// the door rather than producing a wrong figure downstream.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-12)]
    public void Recipe_sheet_requires_a_strictly_positive_yield(int yieldPortions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecipeSheet("FT-1", "Fiche", RecipeCategory.Plat, yieldPortions));

        var recipe = NewRecipe();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            recipe.UpdateDetails("Fiche", RecipeCategory.Plat, yieldPortions, null, null));
    }

    [Fact]
    public void Recipe_sheet_accepts_a_single_portion_yield()
    {
        var recipe = new RecipeSheet("FT-1", "Fiche", RecipeCategory.SousPreparation, yieldPortions: 1);

        Assert.Equal(1, recipe.YieldPortions);
    }

    /// <summary>
    /// Allergens are free text on purpose: this repository does not invent a regulatory
    /// nomenclature, it only bounds the field to the column width.
    /// </summary>
    [Fact]
    public void Recipe_sheet_accepts_any_allergen_wording_up_to_the_column_width()
    {
        var recipe = new RecipeSheet(
            "FT-1",
            "Fiche",
            RecipeCategory.Entree,
            yieldPortions: 4,
            allergens: new string('a', 300));

        Assert.Equal(300, recipe.Allergens!.Length);

        Assert.Throws<ArgumentException>(() =>
            new RecipeSheet("FT-2", "Fiche", RecipeCategory.Entree, 4, allergens: new string('a', 301)));
    }

    [Fact]
    public void Recipe_sheet_requires_at_least_one_ingredient()
    {
        var recipe = NewRecipe();

        Assert.Throws<ArgumentException>(() => recipe.ReplaceIngredients([]));
    }

    [Fact]
    public void Recipe_sheet_refuses_the_same_item_twice()
    {
        var recipe = NewRecipe();

        var duplicated = new[]
        {
            new RecipeIngredient("SEM-01", 1.500m),
            new RecipeIngredient("sem-01", 0.250m)
        };

        Assert.Throws<ArgumentException>(() => recipe.ReplaceIngredients(duplicated));
    }

    [Fact]
    public void Replacing_the_ingredients_renumbers_the_lines_from_one()
    {
        var recipe = NewRecipe();

        recipe.ReplaceIngredients(
        [
            new RecipeIngredient("SEM-01", 1.500m),
            new RecipeIngredient("AGN-01", 2m),
            new RecipeIngredient("POI-01", 0.750m)
        ]);

        Assert.Equal(
            new[] { 1, 2, 3 },
            recipe.Ingredients.Select(ingredient => ingredient.LineNumber).Order().ToArray());

        // Replace-all semantics: the previous lines are gone, not merged.
        recipe.ReplaceIngredients([new RecipeIngredient("SEM-01", 1m)]);

        var only = Assert.Single(recipe.Ingredients);
        Assert.Equal("SEM-01", only.ItemCode);
        Assert.Equal(1, only.LineNumber);
    }

    [Fact]
    public void Recipe_ingredient_normalizes_its_item_code_and_bounds_its_quantity()
    {
        var ingredient = new RecipeIngredient("  sem-01  ", 1.250m, "  semoule moyenne  ");

        Assert.Equal("SEM-01", ingredient.ItemCode);
        Assert.Equal(1.250m, ingredient.Quantity);
        Assert.Equal("semoule moyenne", ingredient.Notes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RecipeIngredient("SEM-01", 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecipeIngredient("SEM-01", -1m));

        // numeric(18,3): a finer quantity would be silently truncated at persistence time.
        Assert.Throws<ArgumentException>(() => new RecipeIngredient("SEM-01", 1.2345m));
    }

    // ============================== HACCP checkpoints ==============================

    [Fact]
    public void Checkpoint_requires_a_minimum_strictly_below_its_maximum()
    {
        Assert.Throws<ArgumentException>(() => new TemperatureCheckpoint("CF-01", "Chambre froide", 4m, 4m));
        Assert.Throws<ArgumentException>(() => new TemperatureCheckpoint("CF-01", "Chambre froide", 6m, 4m));
    }

    [Fact]
    public void Checkpoint_refuses_a_temperature_finer_than_one_decimal()
    {
        // numeric(6,1): the precision of a kitchen probe thermometer.
        Assert.Throws<ArgumentException>(() => new TemperatureCheckpoint("CF-01", "Chambre froide", 0.05m, 4m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemperatureCheckpoint("CF-01", "Chambre froide", -200m, 4m));
    }

    [Fact]
    public void Compliance_range_includes_both_bounds()
    {
        var checkpoint = new TemperatureCheckpoint("CF-01", "Chambre froide", 0m, 4m);

        Assert.True(checkpoint.IsWithinRange(0m));
        Assert.True(checkpoint.IsWithinRange(4m));
        Assert.True(checkpoint.IsWithinRange(2.5m));
        Assert.False(checkpoint.IsWithinRange(-0.1m));
        Assert.False(checkpoint.IsWithinRange(4.1m));

        // The static overload the desktop screen uses to show the verdict live is the very same
        // rule, not a second definition of compliance.
        Assert.True(TemperatureCheckpoint.IsWithinRange(4m, 0m, 4m));
        Assert.False(TemperatureCheckpoint.IsWithinRange(4.1m, 0m, 4m));
    }

    // ============================== HACCP readings ==============================

    [Fact]
    public void Reading_freezes_the_thresholds_it_was_judged_against()
    {
        var checkpoint = new TemperatureCheckpoint("CF-01", "Chambre froide", 0m, 4m);

        var reading = new TemperatureReading(checkpoint, 3.5m, "chef", DateTimeOffset.UtcNow);

        Assert.True(reading.IsCompliant);
        Assert.Equal(0m, reading.MinTempSnapshot);
        Assert.Equal(4m, reading.MaxTempSnapshot);
        Assert.Equal("CF-01", reading.CheckpointCode);
        Assert.Equal("chef", reading.RecordedBy);
    }

    /// <summary>
    /// The rule this module exists for: tightening (or loosening) a checkpoint's range must never
    /// rewrite the compliance history. A reading judged compliant against yesterday's thresholds
    /// stays compliant, with yesterday's thresholds attached, whatever the checkpoint becomes.
    /// </summary>
    [Fact]
    public void Editing_the_checkpoint_range_afterwards_never_rewrites_a_past_reading()
    {
        var checkpoint = new TemperatureCheckpoint("CF-01", "Chambre froide", 0m, 4m);

        var reading = new TemperatureReading(checkpoint, 3.5m, "chef", DateTimeOffset.UtcNow);

        Assert.True(reading.IsCompliant);

        // The range is tightened: 3.5 would now be out of range.
        checkpoint.Update("Chambre froide", 0m, 2m);

        Assert.False(checkpoint.IsWithinRange(3.5m));

        // The reading is untouched: same verdict, same frozen thresholds.
        Assert.True(reading.IsCompliant);
        Assert.Equal(0m, reading.MinTempSnapshot);
        Assert.Equal(4m, reading.MaxTempSnapshot);

        // And the reverse direction, so the test is not passing by accident on one side only.
        var breach = new TemperatureReading(checkpoint, 5m, "chef", DateTimeOffset.UtcNow, "Produits transférés");

        Assert.False(breach.IsCompliant);

        checkpoint.Update("Chambre froide", 0m, 8m);

        Assert.True(checkpoint.IsWithinRange(5m));
        Assert.False(breach.IsCompliant);
        Assert.Equal(2m, breach.MaxTempSnapshot);
    }

    [Fact]
    public void Non_compliant_reading_requires_a_corrective_action()
    {
        var checkpoint = new TemperatureCheckpoint("CF-01", "Chambre froide", 0m, 4m);
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            new TemperatureReading(checkpoint, 9m, "chef", now));

        Assert.Throws<ArgumentException>(() =>
            new TemperatureReading(checkpoint, 9m, "chef", now, "   "));

        var recorded = new TemperatureReading(checkpoint, 9m, "chef", now, "  Produits transférés en chambre 2  ");

        Assert.False(recorded.IsCompliant);
        Assert.Equal("Produits transférés en chambre 2", recorded.CorrectiveAction);
    }

    [Fact]
    public void Compliant_reading_may_carry_a_note_but_does_not_require_one()
    {
        var checkpoint = new TemperatureCheckpoint("CF-01", "Chambre froide", 0m, 4m);

        var withoutNote = new TemperatureReading(checkpoint, 2m, "chef", DateTimeOffset.UtcNow);
        var withNote = new TemperatureReading(checkpoint, 2m, "chef", DateTimeOffset.UtcNow, "Contrôle de routine");

        Assert.Null(withoutNote.CorrectiveAction);
        Assert.Equal("Contrôle de routine", withNote.CorrectiveAction);
    }

    [Fact]
    public void Reading_refuses_a_temperature_finer_than_one_decimal()
    {
        var checkpoint = new TemperatureCheckpoint("CF-01", "Chambre froide", 0m, 4m);

        Assert.Throws<ArgumentException>(() =>
            new TemperatureReading(checkpoint, 2.55m, "chef", DateTimeOffset.UtcNow));
    }

    private static RecipeSheet NewRecipe()
    {
        return new RecipeSheet("FT-1", "Fiche", RecipeCategory.Plat, yieldPortions: 4);
    }
}
