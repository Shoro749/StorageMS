using Data.Context;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Service.Interfaces;
using Service.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace UI.Windows
{
    /// <summary>
    /// Interaction logic for OrderWindow.xaml
    /// </summary>
    public partial class OrderWindow : Window
    {
        private readonly User _user;
        private readonly IService<Product> _productService;
        private readonly IService<OutgoingRequest> _requestService;
        private readonly IService<OutgoingItem> _itemService;

        public OutgoingRequest _or;
        private ObservableCollection<OutgoingItem> _items = new ObservableCollection<OutgoingItem>();

        public OrderWindow(User user, DataContext context, OutgoingRequest or)
        {
            InitializeComponent();
            _user = user;
            _productService = new Service<Product>(context);
            _or = or;

            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadWindow();
        }

        private async Task LoadWindow()
        {
            try
            {
                cb_ProductPicker.ItemsSource = await _productService.GetAllAsync();
                cb_ProductPicker.DisplayMemberPath = "Name";
                cb_ProductPicker.Items.Refresh();

                if (_or != null)
                {
                    _items = new ObservableCollection<OutgoingItem>(_or.Items ?? new List<OutgoingItem>());
                    dg_OrderItems.ItemsSource = _items;
                    tb_Comment.Text = _or.Comment;
                }
                else tb_windowTitle.Text = "СТВОРЕННЯ СКЛАДУ ЗАЯВКИ";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка загрузки: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_items.Count == 0)
                {
                    MessageBox.Show("Додайте хоча б один товар до заявки!", "Увага",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var item in _items)
                {
                    if (item.Quantity <= 0)
                    {
                        MessageBox.Show($"Кількість товару '{item.Product.Name}' повинна бути більше 0!",
                            "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (item.Quantity > item.Product.Stock)
                    {
                        MessageBox.Show($"Кількість товару '{item.Product.Name}' перевищує наявність на складі!",
                            "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (_or == null)
                {
                    var newRequest = new OutgoingRequest
                    {
                        Status = "Pending",
                        Comment = tb_Comment.Text?.Trim() ?? string.Empty,
                        CreatedBy = _user,
                        Items = new List<OutgoingItem>()
                    };

                    _or = await _requestService.CreateAsync(newRequest);

                    MessageBox.Show("Заявку успішно створено!", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _or.Comment = tb_Comment.Text?.Trim() ?? string.Empty;

                    var oldItems = _or.Items.ToList();
                    foreach (var oldItem in oldItems)
                    {
                        await _itemService.DeleteAsync(oldItem.Id);
                    }

                    _or.Items.Clear();

                    foreach (var item in _items)
                    {
                        var newItem = new OutgoingItem
                        {
                            Quantity = item.Quantity,
                            Request = _or,
                            Product = item.Product
                        };

                        await _itemService.CreateAsync(newItem);
                    }

                    _or = await _requestService.UpdateAsync(_or.Id, _or);

                    MessageBox.Show("Заявку успішно оновлено!", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (cb_ProductPicker.SelectedItem is not Product selectedProduct)
            {
                MessageBox.Show("Будь ласка, оберіть товар!", "Увага",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txt_InputQuantity.Text, out decimal quantity) || quantity <= 0)
            {
                MessageBox.Show("Введіть коректну кількість (більше 0)!", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (quantity > selectedProduct.Stock)
            {
                MessageBox.Show($"Недостатньо товару на складі! Доступно: {selectedProduct.Stock} {selectedProduct.Unit}",
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var existingItem = _items.FirstOrDefault(item => item.Product.Id == selectedProduct.Id);

            if (existingItem != null)
            {
                decimal newQuantity = existingItem.Quantity + quantity;

                if (newQuantity > selectedProduct.Stock)
                {
                    MessageBox.Show($"Загальна кількість перевищує доступну! Доступно: {selectedProduct.Stock} {selectedProduct.Unit}",
                        "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                existingItem.Quantity = newQuantity;
            }
            else
            {
                var orderItem = new OutgoingItem
                {
                    Product = selectedProduct,
                    Quantity = quantity,
                };

                _items.Add(orderItem);
            }

            cb_ProductPicker.SelectedIndex = -1;
            txt_InputQuantity.Text = "1";
            dg_OrderItems.Items.Refresh();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OutgoingItem item)
            {
                _items.Remove(item);
            }
        }

        private void cb_ProductPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cb_ProductPicker.SelectedItem is Product selectedProduct)
            {
                lbl_Limit.Text = $"К-сть (max: {selectedProduct.Stock})";
                txt_InputQuantity.Text = "1";
            }
        }
    }
}
