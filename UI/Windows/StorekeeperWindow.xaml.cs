using Data.Context;
using Data.Models;
using Service.Interfaces;
using Service.Services;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static System.Reflection.Metadata.BlobBuilder;

namespace UI.Windows
{
    /// <summary>
    /// Interaction logic for StorekeeperWindow.xaml
    /// </summary>
    public partial class StorekeeperWindow : Window
    {
        private readonly User _currentUser;
        private readonly IService<Product> _productService;
        private readonly IService<ActionLog> _logService;
        private readonly IService<Incoming> _incomingService;

        private List<Product> _products = new List<Product>();
        private Product _selectedProduct;
        private List<Incoming> _incomings = new List<Incoming>();

        public StorekeeperWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _productService = new Service<Product>(context);
            _logService = new Service<ActionLog>(context);
            _incomingService = new Service<Incoming>(context);
            _selectedProduct = null;

            Loaded += AdminWindow_Loaded;
        }

        private async void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                _products = await _productService.GetAllAsync();
                _incomings = await _incomingService.GetAllAsync();

                dg_ProductsStock.ItemsSource = _products;
                dg_incomingList.ItemsSource = _incomings;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження даних: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnStockFilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (tb_ProductSearch == null || chb_LowStockOnly == null || _products == null)
                return;

            ApplyStockFilters();
        }

        private void OnStockFilterChanged(object sender, RoutedEventArgs e)
        {
            if (tb_ProductSearch == null || chb_LowStockOnly == null || _products == null)
                return;

            ApplyStockFilters();
        }

        private void ApplyStockFilters()
        {
            string searchText = tb_ProductSearch.Text.ToLower().Trim();
            bool lowStockOnly = chb_LowStockOnly.IsChecked ?? false;

            var filtered = _products.Where(p =>
            {
                bool matchesName = string.IsNullOrEmpty(searchText) || (p.Name != null && p.Name.ToLower().Contains(searchText));
                bool matchesLowStock = !lowStockOnly || p.Status == "Low" || p.Status == "Out of stock";

                return matchesName && matchesLowStock;
            }).ToList();

            dg_ProductsStock.ItemsSource = filtered;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void AddStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Product product)
            {
                _selectedProduct = product;
                tb_selectedProductName.Text = product.Name;
                tb_stockQuantity.Text = "";

                b_addStockPanel.Visibility = Visibility.Visible;
            }
        }

        private void CloseStockPanel_Click(object sender, RoutedEventArgs e)
        {
            b_addStockPanel.Visibility = Visibility.Collapsed;
        }

        private async void ConfirmAddStock_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(tb_stockQuantity.Text, out int addedAmount) && addedAmount > 0)
            {
                try
                {
                    _selectedProduct.Stock += addedAmount;

                    var incoming = new Incoming
                    {
                        Quantity = addedAmount,
                        Product = _selectedProduct,
                        ReceivedBy = _currentUser,
                    };

                    await _productService.UpdateAsync(_selectedProduct.Id, _selectedProduct);
                    var created = await _incomingService.CreateAsync(incoming);
                    await _logService.CreateAsync(new ActionLog
                    {
                        Action = $"{_currentUser.Name} added {addedAmount} of {_selectedProduct.Name} to stock.",
                        User = _currentUser
                    });

                    _incomings.Add(created);
                    dg_incomingList.Items.Refresh();
                    dg_ProductsStock.Items.Refresh();
                    b_addStockPanel.Visibility = Visibility.Collapsed;
                    _selectedProduct = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Будь ласка, введіть коректне число більше нуля.");
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            this.Close();
        }

        private async Task DeleteIncoming_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Incoming incomingItem)
            {
                var result = MessageBox.Show(
                    $"Ви впевнені, що хочете відмінити накладну №{incomingItem.Id}?\n",
                    "Підтвердження видалення",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    if (incomingItem.Product.Stock < incomingItem.Quantity)
                    {
                        MessageBox.Show("Неможливо відмінити накладну: на складі недостатньо товару для списання цієї кількості.",
                                        "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    incomingItem.Product.Stock -= incomingItem.Quantity;

                    await _incomingService.DeleteAsync(incomingItem.Id);
                    await _productService.UpdateAsync(incomingItem.Product.Id, incomingItem.Product);

                    await _logService.CreateAsync(new ActionLog
                    {
                        Action = $"Deleted incoming №{incomingItem.Id} ({incomingItem.Product.Name}, -{incomingItem.Quantity})",
                        User = _currentUser
                    });

                    _incomings.Remove(incomingItem);
                    ApplyIncomingFilters();

                    MessageBox.Show("Накладну видалено, залишки товару скориговано.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Сталася помилка при видаленні: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnIncomingFilterChanged(object sender, TextChangedEventArgs e)
        {
            if (tb_incomingSearch == null || dp_incomingDate == null || _incomings == null)
                return;

            ApplyIncomingFilters();
        }

        private void OnIncomingFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tb_incomingSearch == null || dp_incomingDate == null || _incomings == null)
                return;

            ApplyIncomingFilters();
        }

        private void ApplyIncomingFilters()
        {
            string searchText = tb_incomingSearch.Text.ToLower().Trim();
            DateTime? selectedDate = dp_incomingDate.SelectedDate;

            var filtered = _incomings.Where(item =>
            {
                bool matchesProduct = string.IsNullOrEmpty(searchText) ||
                                      (item.Product != null && item.Product.Name.ToLower().Contains(searchText));

                bool matchesDate = !selectedDate.HasValue || item.ReceivedAt.Date == selectedDate.Value.Date;

                return matchesProduct && matchesDate;
            }).ToList();

            dg_incomingList.ItemsSource = filtered;
        }
    }
}
