using Data.Context;
using Data.Models;
using Service.Interfaces;
using Service.Services;
using System.Windows;
using System.Windows.Controls;

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

        private List<Product> _products;
        private Product _selectedProduct;

        public StorekeeperWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _productService = new Service<Product>(context);
            _logService = new Service<ActionLog>(context);
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
                dg_ProductsStock.ItemsSource = _products;
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

                    await _productService.UpdateAsync(_selectedProduct.Id, _selectedProduct);

                    await _logService.CreateAsync(new ActionLog
                    {
                        Action = $"{_currentUser.Name} added {addedAmount} of {_selectedProduct.Name} to stock.",
                        User = _currentUser
                    });

                    dg_ProductsStock.Items.Refresh();
                    b_addStockPanel.Visibility = Visibility.Collapsed;

                    MessageBox.Show("Залишки оновлено успішно!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
