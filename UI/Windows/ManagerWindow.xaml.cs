using Data.Context;
using Data.Models;
using Service.Interfaces;
using Service.Services;
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

        private List<OutgoingRequest> _orders;
        private List<Product> _products;
        public ManagerWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _logService = new Service<ActionLog>(context);
            _requestService = new Service<OutgoingRequest>(context);
            _productService = new Service<Product>(context);
            _dataContext = context;
        }

        private async void LoadOrders()
        {
            try
            {
                // page 2

                // page 3
                _orders = await _requestService.GetAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка загрузки заяв: {ex.Message}", "Помилка",
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
                    _orders.Add(ow._or);
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

        }

        private void OnStockFilterChanged(object sender, RoutedEventArgs e)
        {

        }

        private void RefreshStock_Click(object sender, RoutedEventArgs e)
        {

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
                            _orders.Remove(selectedOrder);
                            _orders.Add(ow._or);
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
                        _orders.Remove(selectedOrder);
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
            if (cb_FilterStatus == null || dp_FilterDate == null || chb_MyOrdersOnly == null || _orders == null)
                return;

            ApplyOrderFilters();
        }

        private void ApplyOrderFilters()
        {
            string selectedStatus = (cb_FilterStatus.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Всі статуси";
            DateTime? selectedDate = dp_FilterDate.SelectedDate;
            bool myOrdersOnly = chb_MyOrdersOnly.IsChecked ?? false;

            var filtered = _orders.Where(order =>
            {
                bool matchesStatus = selectedStatus == "Всі статуси" || order.Status == selectedStatus;
                bool matchesDate = !selectedDate.HasValue || order.CreatedAt.Date == selectedDate.Value.Date;
                bool matchesOwner = !myOrdersOnly || (order.CreatedBy != null && order.CreatedBy.Id == _currentUser.Id);

                return matchesStatus && matchesDate && matchesOwner;
            }).ToList();

            dg_Orders.ItemsSource = filtered;
        }
    }
}
