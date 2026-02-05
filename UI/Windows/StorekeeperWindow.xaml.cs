using Azure.Core;
using Data.Context;
using Data.Models;
using Microsoft.EntityFrameworkCore;
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
        private readonly IService<OutgoingRequest> _requestService;

        private OutgoingRequest _chosenRequest = null;
        private Product _selectedProduct = null;
        private List<Product> _products = new List<Product>();
        private List<Incoming> _incomings = new List<Incoming>();
        private List<OutgoingRequest> _requests = new List<OutgoingRequest>();

        public StorekeeperWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _productService = new Service<Product>(context);
            _logService = new Service<ActionLog>(context);
            _incomingService = new Service<Incoming>(context);
            _requestService = new Service<OutgoingRequest>(context);

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
                _requests = await _requestService.GetAllAsync();
                _requests = _requests.OrderByDescending(r => r.CreatedAt).ToList();

                dg_ProductsStock.ItemsSource = _products;
                dg_incomingList.ItemsSource = _incomings;

                UpdateRequestList(_requests);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження даних: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateRequestList(List<OutgoingRequest> requests)
        {
            dg_Requests.ItemsSource = requests;
            dg_Requests.Items.Refresh();
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

        private async void DeleteIncoming_Click(object sender, RoutedEventArgs e)
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

        private void OnRequestFilterChanged(object sender, EventArgs e)
        {
            if (cb_FilterRequestStatus == null || dp_RequestDate == null || _requests == null)
                return;

            ApplyRequestFilters();
        }

        private void ApplyRequestFilters()
        {
            var selectedItem = cb_FilterRequestStatus.SelectedItem as ComboBoxItem;
            string selectedStatus = selectedItem?.Content?.ToString() ?? "Всі статуси";

            DateTime? selectedDate = dp_RequestDate.SelectedDate;

            var filtered = _requests.Where(request =>
            {
                bool matchesStatus = selectedStatus == "Всі статуси";

                if (!matchesStatus)
                {
                    string dbStatus = selectedStatus switch
                    {
                        "Очікує" => "Pending",
                        "Завершено" => "Completed",
                        "Відхилено" => "Rejected",
                        _ => request.Status
                    };
                    matchesStatus = request.Status == dbStatus;
                }

                bool matchesDate = !selectedDate.HasValue ||
                                   request.CreatedAt.Date == selectedDate.Value.Date;

                return matchesStatus && matchesDate;
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

            UpdateRequestList(filtered);
        }

        private void ViewRequestDetails_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var selectedRequest = button?.DataContext as OutgoingRequest;

            if (selectedRequest != null)
            {
                _chosenRequest = selectedRequest;
                DisplayRequestDetails(selectedRequest);
            }
            else
            {
                MessageBox.Show("Невдалося відкрити заявку!", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DisplayRequestDetails(OutgoingRequest request)
        {
            lbl_RequestTitle.Text = $"ЗАЯВКА №{request.Id}";
            lbl_RequestAuthor.Text = $"Автор: {request.CreatedBy?.Name ?? "Невідомо"} | Дата: {request.CreatedAt:dd.MM.yyyy HH:mm}";
            lbl_RequestComment.Text = string.IsNullOrWhiteSpace(request.Comment)
                ? "Коментар відсутній"
                : request.Comment;

            dg_RequestItems.ItemsSource = request.Items?.ToList() ?? new List<OutgoingItem>();

            if (request.Status == "Pending")
            {
                ug_RequestActions.Visibility = Visibility.Visible;
            }
            else
            {
                ug_RequestActions.Visibility = Visibility.Collapsed;
            }

            b_RequestDetailsPanel.Visibility = Visibility.Visible;
        }

        private void CloseRequestDetails_Click(object sender, RoutedEventArgs e)
        {
            _chosenRequest = null;
            b_RequestDetailsPanel.Visibility = Visibility.Collapsed;
        }

        private async void ApproveRequest_Click(object sender, RoutedEventArgs e)
        {
            if (_chosenRequest == null) return;

            try
            {
                var insufficientItems = new List<string>();

                foreach (var item in _chosenRequest.Items)
                {
                    var product = await _productService.GetByIdAsync(item.Product.Id);

                    if (product == null)
                    {
                        insufficientItems.Add($"{item.Product.Name} - товар не знайдено");
                        continue;
                    }

                    if (product.Stock < item.Quantity)
                    {
                        insufficientItems.Add($"{product.Name} - недостатньо на складі (є: {product.Stock}, потрібно: {item.Quantity})");
                    }
                }

                if (insufficientItems.Any())
                {
                    string message = "Неможливо затвердити заявку через недостатність товарів:\n\n" +
                                   string.Join("\n", insufficientItems);

                    MessageBox.Show(message, "Недостатньо товару",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Затвердити заявку №{_chosenRequest.Id}?\n\nТовари будуть списані зі складу.",
                    "Підтвердження",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var item in _chosenRequest.Items)
                    {
                        var product = await _productService.GetByIdAsync(item.Product.Id);
                        product.Stock -= item.Quantity;
                        await _productService.UpdateAsync(product.Id, product);
                    }

 
                    _chosenRequest.Status = "Completed";
                    await _requestService.UpdateAsync(_chosenRequest.Id, _chosenRequest);

                    var itemsList = string.Join(", ", _chosenRequest.Items.Select(i =>
                        $"{i.Product.Name} ({i.Quantity} {i.Product.Unit})"));

                    await _logService.CreateAsync(new ActionLog
                    {
                        Action = $"{_currentUser.Name} затвердив заявку №{_chosenRequest.Id}. Товари: {itemsList}",
                        User = _currentUser,
                    });

                    await LoadData();
                    ApplyRequestFilters();

                    b_RequestDetailsPanel.Visibility = Visibility.Collapsed;
                    _chosenRequest = null;

                    MessageBox.Show("Заявку успішно затверджено!\nТовари списано зі складу.", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка затвердження заявки: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            if (_chosenRequest == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"Відхилити заявку №{_chosenRequest.Id}?",
                    "Підтвердження",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _chosenRequest.Status = "Rejected";
                    await _requestService.UpdateAsync(_chosenRequest.Id, _chosenRequest);

                    await _logService.CreateAsync(new ActionLog
                    {
                        Action = $"{_currentUser.Name} відхилив заявку №{_chosenRequest.Id}",
                        User = _currentUser,
                    });

                    await LoadData();
                    ApplyRequestFilters();

                    b_RequestDetailsPanel.Visibility = Visibility.Collapsed;
                    _chosenRequest = null;

                    MessageBox.Show("Заявку відхилено!", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка відхилення заявки: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshIncoming_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
