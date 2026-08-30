using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Clients : recherche dans le fichier clients, creation et modification
/// d'une fiche (identifiants fiscaux algeriens compris), activation et
/// desactivation. Vue autonome : elle ne connait que le ModuleViewContext que la
/// fenetre lui prete, jamais MainWindow ni une autre vue.
/// </summary>
public partial class CustomersView : UserControl
{
    // Libelles francais des valeurs de l'enum CustomerType : seul l'affichage est
    // traduit, la valeur envoyee a l'API reste celle du domaine.
    private static readonly CustomerTypeOption[] CustomerTypeOptions =
    [
        new(CustomerType.Company, "Entreprise"),
        new(CustomerType.Individual, "Particulier"),
        new(CustomerType.PublicEntity, "Organisme public")
    ];

    private const string WritePermissionHint = "Permission customers.write requise : votre profil ne peut que consulter le fichier clients.";

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons d'ecriture, capturees avant toute
    // substitution par le message de permission : l'affectation doit rester
    // symetrique (voir ApplyPermissionHint).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Null en mode creation, code du client edite en mode modification.
    private string? editingCustomerCode;

    // Le profil connecte peut-il ecrire dans le fichier clients ? Memorise a
    // l'ouverture de la session : les boutons d'ecriture sont grises sinon,
    // plutot que de laisser l'utilisateur decouvrir un 403 apres avoir saisi
    // toute la fiche. Le serveur reste la seule autorite en matiere de droits.
    private bool canWriteCustomers = true;

    public CustomersView()
    {
        InitializeComponent();

        CustomerTypeComboBox.ItemsSource = CustomerTypeOptions;
        ResetForm();
        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canWriteCustomers = context.HasPermission(PermissionCatalog.CustomersWrite);

        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge le fichier clients. Appelee a la premiere ouverture de l'onglet et
    /// par le bouton Actualiser. Sort silencieusement tant qu'aucun contexte n'est
    /// disponible ou qu'aucune session n'est ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(ReloadCustomersAsync);
    }

    /// <summary>Vide la grille et le formulaire (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        CustomersDataGrid.ItemsSource = null;
        SearchTextBox.Text = string.Empty;
        IncludeInactiveCustomersCheckBox.IsChecked = false;
        CustomerCountTextBlock.Text = string.Empty;
        ResetForm();
        UpdateActionButtons();
    }

    private async void RefreshCustomersButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadWithStatusAsync();
    }

    private async void IncludeInactiveCustomersCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await ReloadWithStatusAsync();
    }

    private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ReloadWithStatusAsync();
    }

    private async Task ReloadWithStatusAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadCustomersAsync();
            moduleContext.SetStatus("Fichier clients actualisé.");
        });
    }

    private async Task ReloadCustomersAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var search = string.IsNullOrWhiteSpace(SearchTextBox.Text) ? null : SearchTextBox.Text.Trim();

        // La ligne selectionnee est identifiee par son code pour etre restauree
        // apres le rechargement : sans cela, activer ou desactiver un client fait
        // perdre la selection et l'operateur doit retrouver sa ligne a la main.
        var selectedCode = (CustomersDataGrid.SelectedItem as CustomerRowView)?.Code;

        var customers = await moduleContext.ApiClient.GetCustomersAsync(
            moduleContext.ApiBaseUrl,
            search,
            IncludeInactiveCustomersCheckBox.IsChecked == true);

        var rows = customers.Select(ToRowView).ToArray();
        CustomersDataGrid.ItemsSource = rows;

        CustomerCountTextBlock.Text = customers.Count == 1
            ? "1 client"
            : $"{customers.Count.ToString(CultureInfo.CurrentCulture)} clients";

        RestoreSelection(rows, selectedCode);

        UpdateActionButtons();
    }

    // Le code du client est la cle stable d'une ligne a l'autre : la selection est
    // rendue sur ce code, ou abandonnee quand la ligne n'est plus dans la liste
    // (recherche restreinte, client sorti du filtre "inactifs").
    private void RestoreSelection(IReadOnlyList<CustomerRowView> rows, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var restored = rows.FirstOrDefault(row => string.Equals(row.Code, code, StringComparison.OrdinalIgnoreCase));

        if (restored is null)
        {
            return;
        }

        CustomersDataGrid.SelectedItem = restored;
        CustomersDataGrid.ScrollIntoView(restored);
    }

    private void CustomersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();

        if (CustomersDataGrid.SelectedItem is not CustomerRowView selected)
        {
            return;
        }

        // Selectionner une ligne bascule le formulaire en mode modification : le code
        // identifie la fiche cote API, il n'est donc plus modifiable.
        var customer = selected.Source;

        editingCustomerCode = customer.Code;
        FormTitleTextBlock.Text = $"Modifier {customer.Code}";
        CustomerCodeTextBox.Text = customer.Code;
        CustomerCodeTextBox.IsEnabled = false;
        CustomerNameTextBox.Text = customer.Name;
        CustomerTypeComboBox.SelectedValue = customer.CustomerType;
        CustomerNifTextBox.Text = customer.Nif ?? string.Empty;
        CustomerRcTextBox.Text = customer.Rc ?? string.Empty;
        CustomerAiTextBox.Text = customer.Ai ?? string.Empty;
        CustomerNisTextBox.Text = customer.Nis ?? string.Empty;
        CustomerAddressTextBox.Text = customer.Address ?? string.Empty;
        CustomerCityTextBox.Text = customer.City ?? string.Empty;
        CustomerPhoneTextBox.Text = customer.Phone ?? string.Empty;
        CustomerEmailTextBox.Text = customer.Email ?? string.Empty;
        SaveCustomerButton.Content = "Enregistrer les modifications";
    }

    private void NewCustomerButton_Click(object sender, RoutedEventArgs e)
    {
        ResetForm();
        UpdateActionButtons();
    }

    private void CustomerTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyCustomerTypeToForm();
    }

    // NIF, RC, AI et NIS identifient une entite immatriculee : ils n'ont pas de
    // sens pour un particulier. Le bloc entier disparait dans ce cas (avec son
    // espacement, pour ne pas laisser un trou), et les champs sont vides afin
    // qu'aucune valeur saisie avant le changement de type ne subsiste a l'ecran.
    private void ApplyCustomerTypeToForm()
    {
        // Le bloc fiscal est declare apres la liste deroulante dans le XAML : si
        // l'evenement se declenchait pendant le chargement de la vue, les champs
        // ne seraient pas encore construits.
        if (FiscalIdentifiersGrid is null)
        {
            return;
        }

        var isIndividual = CustomerTypeComboBox.SelectedValue is CustomerType.Individual;

        FiscalIdentifiersGrid.Visibility = isIndividual ? Visibility.Collapsed : Visibility.Visible;
        FiscalSpacerRow.Height = isIndividual ? new GridLength(0) : new GridLength(12);
        IndividualHintTextBlock.Visibility = isIndividual ? Visibility.Visible : Visibility.Collapsed;

        if (isIndividual)
        {
            CustomerNifTextBox.Text = string.Empty;
            CustomerRcTextBox.Text = string.Empty;
            CustomerAiTextBox.Text = string.Empty;
            CustomerNisTextBox.Text = string.Empty;
        }

        CustomerNameTextBox.Tag = isIndividual ? "Nom et prénom du client" : "Raison sociale";
    }

    private void ResetForm()
    {
        editingCustomerCode = null;
        FormTitleTextBlock.Text = "Nouveau client";
        CustomerCodeTextBox.Text = string.Empty;
        CustomerCodeTextBox.IsEnabled = true;
        CustomerNameTextBox.Text = string.Empty;
        CustomerTypeComboBox.SelectedValue = CustomerType.Company;
        CustomerNifTextBox.Text = string.Empty;
        CustomerRcTextBox.Text = string.Empty;
        CustomerAiTextBox.Text = string.Empty;
        CustomerNisTextBox.Text = string.Empty;
        CustomerAddressTextBox.Text = string.Empty;
        CustomerCityTextBox.Text = string.Empty;
        CustomerPhoneTextBox.Text = string.Empty;
        CustomerEmailTextBox.Text = string.Empty;
        SaveCustomerButton.Content = "Créer le client";
        CustomersDataGrid.SelectedItem = null;
    }

    private async void SaveCustomerButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var code = CustomerCodeTextBox.Text.Trim();
            var name = CustomerNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                moduleContext.SetStatus("Le code et le nom du client sont requis.", isError: true);
                return;
            }

            if (CustomerTypeComboBox.SelectedValue is not CustomerType customerType)
            {
                moduleContext.SetStatus("Sélectionnez un type de client.", isError: true);
                return;
            }

            // Les identifiants fiscaux (NIF, RC, AI, NIS) identifient une entite
            // immatriculee : ils n'existent pas pour un particulier. Le formulaire
            // les masque dans ce cas, et rien n'est envoye au serveur - y compris
            // d'anciennes valeurs saisies avant un changement de type.
            var isIndividual = customerType == CustomerType.Individual;

            string? nif = null;

            // Regle metier du domaine : le NIF, s'il est renseigne, fait exactement
            // 15 chiffres. Verifie ici pour eviter un aller-retour API previsible.
            if (!isIndividual && !TryReadNif(out nif))
            {
                moduleContext.SetStatus("Le NIF doit comporter exactement 15 chiffres.", isError: true);
                return;
            }

            if (!TryReadEmail(out var email))
            {
                moduleContext.SetStatus("L'adresse de courriel est invalide.", isError: true);
                return;
            }

            var rc = isIndividual ? null : ReadOptional(CustomerRcTextBox);
            var ai = isIndividual ? null : ReadOptional(CustomerAiTextBox);
            var nis = isIndividual ? null : ReadOptional(CustomerNisTextBox);
            var address = ReadOptional(CustomerAddressTextBox);
            var city = ReadOptional(CustomerCityTextBox);
            var phone = ReadOptional(CustomerPhoneTextBox);

            var existingCode = editingCustomerCode;

            // Le serveur normalise le code (majuscules) : c'est la valeur qu'il
            // renvoie qui est affichee et reutilisee, jamais la saisie brute.
            if (existingCode is null)
            {
                var created = await moduleContext.ApiClient.CreateCustomerAsync(
                    moduleContext.ApiBaseUrl,
                    new CreateCustomerRequest(code, name, customerType, nif, rc, ai, nis, address, city, phone, email));

                moduleContext.SetStatus($"Client {created.Code} créé.");
            }
            else
            {
                var updated = await moduleContext.ApiClient.UpdateCustomerAsync(
                    moduleContext.ApiBaseUrl,
                    existingCode,
                    new UpdateCustomerRequest(name, customerType, nif, rc, ai, nis, address, city, phone, email));

                moduleContext.SetStatus($"Client {updated.Code} mis à jour.");
            }

            ResetForm();
            await ReloadCustomersAsync();
        });
    }

    private async void ActivateCustomerButton_Click(object sender, RoutedEventArgs e)
    {
        await SetCustomerActiveAsync(isActive: true);
    }

    private async void DeactivateCustomerButton_Click(object sender, RoutedEventArgs e)
    {
        await SetCustomerActiveAsync(isActive: false);
    }

    private async Task SetCustomerActiveAsync(bool isActive)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (CustomersDataGrid.SelectedItem is not CustomerRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez un client.", isError: true);
            return;
        }

        // Action engageante sur le referentiel : confirmation explicite avant l'appel.
        var question = isActive
            ? $"Réactiver le client {selected.Code} ({selected.Name}) ?\nIl sera de nouveau proposé à la facturation."
            : $"Désactiver le client {selected.Code} ({selected.Name}) ?\nIl ne sera plus proposé à la facturation.";

        var confirmed = Confirm(question, isActive ? "Activer le client" : "Désactiver le client");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            // Le code retenu pour le message est celui renvoye par le serveur, seule
            // forme normalisee qui fait foi.
            var changed = await moduleContext.ApiClient.SetCustomerActiveAsync(
                moduleContext.ApiBaseUrl,
                selected.Code,
                isActive);

            await ReloadCustomersAsync();

            moduleContext.SetStatus(isActive
                ? $"Client {changed.Code} activé."
                : $"Client {changed.Code} désactivé.");
        });
    }

    // Activer / Desactiver n'ont de sens que sur une ligne selectionnee dont le
    // statut est l'inverse de l'action : sinon les boutons restent grises. Les
    // trois actions d'ecriture sont en outre conditionnees a customers.write.
    private void UpdateActionButtons()
    {
        var selected = CustomersDataGrid.SelectedItem as CustomerRowView;

        SaveCustomerButton.IsEnabled = canWriteCustomers;
        ActivateCustomerButton.IsEnabled = canWriteCustomers && selected is { IsActive: false };
        DeactivateCustomerButton.IsEnabled = canWriteCustomers && selected is { IsActive: true };

        ApplyPermissionHint(SaveCustomerButton, canWriteCustomers, WritePermissionHint);
        ApplyPermissionHint(ActivateCustomerButton, canWriteCustomers, WritePermissionHint);
        ApplyPermissionHint(DeactivateCustomerButton, canWriteCustomers, WritePermissionHint);
    }

    // Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    // d'origine du bouton quand il est present : l'affectation doit etre symetrique,
    // sinon un message pose pour un profil restreint survit a la reconnexion d'un
    // profil qui, lui, a le droit (les vues survivent a la deconnexion).
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    // Gabarit de confirmation des actes engageants : fenetre proprietaire, icone
    // d'avertissement, defaut sur Non - la touche Entree ne suffit jamais a
    // engager l'action.
    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private bool TryReadNif(out string? nif)
    {
        nif = ReadOptional(CustomerNifTextBox);

        return nif is null || (nif.Length == 15 && nif.All(char.IsAsciiDigit));
    }

    private bool TryReadEmail(out string? email)
    {
        email = ReadOptional(CustomerEmailTextBox);

        if (email is null)
        {
            return true;
        }

        var atIndex = email.IndexOf('@');

        return atIndex > 0 && atIndex < email.Length - 1;
    }

    private static string? ReadOptional(TextBox textBox)
    {
        var value = textBox.Text.Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // Projection d'affichage : le type de client est traduit une seule fois ici, et
    // la reponse d'origine reste portee par la ligne pour remplir le formulaire.
    private static CustomerRowView ToRowView(CustomerResponse customer)
    {
        return new CustomerRowView(
            customer,
            customer.Code,
            customer.Name,
            DescribeCustomerType(customer.CustomerType),
            customer.Nif,
            customer.City,
            customer.Phone,
            customer.IsActive);
    }

    private static string DescribeCustomerType(CustomerType customerType)
    {
        var option = CustomerTypeOptions.FirstOrDefault(item => item.Value == customerType);

        return option?.Label ?? customerType.ToString();
    }

    private sealed record CustomerTypeOption(CustomerType Value, string Label);

    private sealed record CustomerRowView(
        CustomerResponse Source,
        string Code,
        string Name,
        string CustomerTypeLabel,
        string? Nif,
        string? City,
        string? Phone,
        bool IsActive);
}
