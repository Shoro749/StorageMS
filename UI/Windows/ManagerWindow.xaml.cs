using Data.Context;
using Data.Models;
using Service.Interfaces;
using Service.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace UI.Windows
{
    /// <summary>
    /// Interaction logic for ManagerWindow.xaml
    /// </summary>
    public partial class ManagerWindow : Window
    {
        private readonly User _currentUser;
        private readonly IService<ActionLog> _logService;
        private readonly IService<OutgoingRequest> _requestService;
        private readonly IService<Product> _productService;
        private readonly DataContext _dataContext;

        private ObservableCollection<OutgoingRequest> _requests;
        private List<Product> _products;
        public ManagerWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _logService = new Service<ActionLog>(context);
            _requestService = new Service<OutgoingRequest>(context);
            _productService = new Service<Product>(context);
            _dataContext = context;

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
                var requestsList = await _requestService.GetAllAsync();
                _requests = new ObservableCollection<OutgoingRequest>(requestsList);

                dg_Orders.ItemsSource = _requests;
                dg_ProductsStock.ItemsSource = _products;

                // усього товарів
                txt_TotalItems.Text = _products.Count.ToString("#,##0");

                // критичний залишок
                var lowStockProducts = _products.Where(p => p.Stock <= p.MinimumStock).ToList();
                txt_LowStock.Text = lowStockProducts.Count.ToString();

                // заявки сьогодні
                var today = DateTime.Today;
                var completedToday = _requests.Count(r =>
                    r.Status == "Completed" &&
                    r.CreatedAt.Date == today);
                txt_CompletedToday.Text = completedToday.ToString();

                // pending заявки
                var pendingOrders = _requests
                    .Where(r => r.Status == "Pending")
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        Id = r.Id,
                        Client = r.CreatedBy?.Name ?? "Unknown",
                        Date = r.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                        RequestObject = r
                    })
                    .ToList();

                dg_PendingOrders.ItemsSource = pendingOrders;

                // список товарів з 10 найменшим залишком
                var lowStockList = lowStockProducts
                    .OrderBy(p => p.Stock)
                    .Take(10)
                    .Select(p => new
                    {
                        Name = p.Name,
                        Quantity = $"{p.Stock}/{p.MinimumStock} {p.Unit}"
                    })
                    .ToList();

                ic_LowStockList.ItemsSource = lowStockList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження даних: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CreateOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OrderWindow ow = new OrderWindow(_currentUser, _dataContext, null);
                bool? result = ow.ShowDialog();

                if (result == true)
                {
                    _requests.Add(ow._or);
                    dg_Orders.Items.Refresh();
                    await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has created order {ow._or.Id}.", User = _currentUser });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка",
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

        private async void EditOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OutgoingRequest selectedOrder)
            {
                try
                {
                    if (selectedOrder.Status == "Pending")
                    {
                        OrderWindow ow = new OrderWindow(_currentUser, _dataContext, selectedOrder);
                        bool? result = ow.ShowDialog();

                        if (result == true)
                        {
                            _requests.Remove(selectedOrder);
                            _requests.Add(ow._or);
                            dg_Orders.Items.Refresh();

                            await _logService.CreateAsync(new ActionLog
                            {
                                Action = $"{_currentUser.Name} has edited order {selectedOrder.Id}.",
                                User = _currentUser
                            });
                        }
                    }
                    else MessageBox.Show($"Заявку уже не можна змінити!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ViewOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OutgoingRequest selectedOrder)
            {
                tb_detailsOrderId.Text = $"ЗАЯВКА №{selectedOrder.Id}";
                tb_detailsStatus.Text = selectedOrder.Status;
                tb_detailsAuthor.Text = selectedOrder.CreatedBy?.Name ?? "Невідомо";
                tb_detailsDate.Text = selectedOrder.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                tb_detailsComment.Text = string.IsNullOrWhiteSpace(selectedOrder.Comment)
                                         ? "Коментар відсутній"
                                         : selectedOrder.Comment;

                ic_detailsItems.ItemsSource = selectedOrder.Items;

                b_orderDetailsPanel.Visibility = Visibility.Visible;
            }
        }

        private void CloseOrderDetails_Click(object sender, RoutedEventArgs e)
        {
            b_orderDetailsPanel.Visibility = Visibility.Collapsed;
        }

        private async void CancelOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.DataContext is OutgoingRequest selectedOrder)
                {
                    var result = MessageBox.Show($"Ви точно хочете видалити цю заявук?", "Помилка", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        _requests.Remove(selectedOrder);
                        await _requestService.DeleteAsync(selectedOrder.Id);
                        await _logService.CreateAsync(new ActionLog
                        {
                            Action = $"{_currentUser.Name} has deleted order {selectedOrder.Id}.",
                            User = _currentUser
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка видалення: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOrderFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cb_FilterStatus == null || dp_FilterDate == null || chb_MyOrdersOnly == null || _requests == null)
                return;

            ApplyOrderFilters();
        }

        private void OnOrderFilterChanged(object sender, RoutedEventArgs e)
        {
            if (cb_FilterStatus == null || dp_FilterDate == null || chb_MyOrdersOnly == null || _requests == null)
                return;

            ApplyOrderFilters();
        }

        private void ApplyOrderFilters()
        {
            string selectedStatus = (cb_FilterStatus.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Всі статуси";
            DateTime? selectedDate = dp_FilterDate.SelectedDate;
            bool myOrdersOnly = chb_MyOrdersOnly.IsChecked ?? false;

            var filtered = _requests.Where(order =>
            {
                bool matchesStatus = selectedStatus == "Всі статуси" || order.Status == selectedStatus;
                bool matchesDate = !selectedDate.HasValue || order.CreatedAt.Date == selectedDate.Value.Date;
                bool matchesOwner = !myOrdersOnly || (order.CreatedBy != null && order.CreatedBy.Id == _currentUser.Id);

                return matchesStatus && matchesDate && matchesOwner;
            }).ToList();

            dg_Orders.ItemsSource = filtered;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            this.Close();
        }
    }
}
