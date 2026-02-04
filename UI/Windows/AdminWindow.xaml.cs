using Data.Context;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Service.Interfaces;
using Service.Services;
using System.Windows;
using System.Windows.Controls;
using static System.Reflection.Metadata.BlobBuilder;

namespace UI.Windows
{
    /// <summary>
    /// Interaction logic for AdminWindow.xaml
    /// </summary>
    public partial class AdminWindow : Window
    {
        private readonly User _currentUser;
        private User _chosenUser;
        private readonly IUserService _userService;
        private readonly IService<Role> _roleService;
        private readonly IService<ActionLog> _logService;
        private readonly IService<Product> _productService;
        
        private List<User> _users;
        private List<Product> _products = new List<Product>();
        private Product _chosenProduct = null;
        private List<ActionLog> _logs = new List<ActionLog>();
        private ActionLog _chosenLog = null;
        public AdminWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _userService = new UserService(context);
            _roleService = new Service<Role>(context);
            _logService = new Service<ActionLog>(context);
            _productService = new Service<Product>(context);

            this.Loaded += AdminWindow_Loaded;
        }

        private async void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.WhenAll(LoadRoles(), LoadUsers());
            await LoadProducts();
            await LoadLogs();
        }

        private async Task LoadUsers()
        {
            try
            {
                _users = await _userService.GetAllAsync();
                UpdateUserList(_users);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження користувачів: {ex.Message}");
            }
        }

        private async Task LoadLogs()
        {
            try
            {
                _logs = await _logService.GetAllAsync();
                _logs = _logs.OrderByDescending(l => l.CreatedAt).ToList();
                UpdateLogList(_logs);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження логів: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadProducts()
        {
            try
            {
                _products = await _productService.GetAllAsync();
                UpdateProductList(_products);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження товарів: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadRoles()
        {
            try
            {
                var roles = await _roleService.GetAllAsync();

                cb_userRole.ItemsSource = roles;
                cb_userRole.DisplayMemberPath = "Name";

                cb_createRole.ItemsSource = roles;
                cb_createRole.DisplayMemberPath = "Name";

                var filterList = new List<object>();
                filterList.Add(new { Id = 0, Name = "Всі ролі" });
                filterList.AddRange(roles);

                cb_filterRoles.ItemsSource = filterList;
                cb_filterRoles.DisplayMemberPath = "Name";
                cb_filterRoles.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження ролей: {ex.Message}");
            }
        }

        private void UpdateUserList(List<User> users)
        {
            dg_userList.ItemsSource = users;
            dg_userList.Items.Refresh();
        }

        private void CloseDetails_Click(object sender, RoutedEventArgs e)
        {
            _chosenUser = null;
            b_detailsPanel.Visibility = Visibility.Collapsed;
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;

            var selectedUser = button?.DataContext as User;

            if (selectedUser != null)
            {
                tb_name.Text = selectedUser.Name;
                cb_userRole.SelectedIndex = selectedUser.Role.Id - 1;
                _chosenUser = selectedUser;
                b_detailsPanel.Visibility = Visibility.Visible;
            }
            else MessageBox.Show("Невдалося переглянути користувача!");
        }

        private async void EditUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = tb_name.Text.Trim();
                string password = pb_userPassword.Password.Trim();
                var role = cb_userRole.SelectedItem as Role;

                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("The username or password cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingUser = await _userService.GetByUsername(name);

                if (existingUser != null && existingUser.Id != _chosenUser.Id)
                {
                    MessageBox.Show($"The user with name {name} is already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                User newUser;
                if (string.IsNullOrEmpty(password))
                {
                    newUser = new User
                    {
                        Id = _chosenUser.Id,
                        Name = name,
                        PasswordHash = PasswordHasher.HashPassword(password),
                        Role = role,
                    };
                }
                else
                {
                    newUser = new User
                    {
                        Id = _chosenUser.Id,
                        Name = name,
                        PasswordHash = _chosenUser.PasswordHash,
                        Role = role,
                    };
                }  

                var updatedUser = await _userService.UpdateAsync(newUser.Id, newUser);
                await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has updated user {name} -> {updatedUser.Name}.", User = _currentUser });

                _users.Remove(_chosenUser);
                _users.Add(updatedUser);

                b_detailsPanel.Visibility = Visibility.Collapsed;
                _chosenUser = null;

                UpdateUserList(_users);

                MessageBox.Show("User was successfully updated!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex )
            {
                MessageBox.Show($"Error with editting user: {ex.Message}");
                return;
            }
        }

        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _userService.DeleteAsync(_chosenUser.Id);
                await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has deleted user {_chosenUser.Name}.", User = _currentUser });

                _users.Remove(_chosenUser);
                _users.Remove(_chosenUser);
                _chosenUser = null;

                UpdateUserList(_users);

                b_detailsPanel.Visibility = Visibility.Collapsed;
                
                MessageBox.Show("User was successfully deleted!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error with deleting: " + ex.Message);
            }
        }

        private void CreateUser_Click(object sender, RoutedEventArgs e)
        {
            b_createUserPanel.Visibility = Visibility.Visible;
            cb_createRole.SelectedIndex = 2;
        }

        private async void ConfirmCreateUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = tb_createName.Text.Trim();
                string password = pb_createPassword.Password.Trim();
                string confPassword = pb_confirmPassword.Password.Trim();
                var role = cb_createRole.SelectedItem as Role;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("The username or password cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (await _userService.GetByUsername(name) != null)
                {
                    MessageBox.Show($"The user with name {name} is already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (password != confPassword)
                {
                    MessageBox.Show($"Please confirm password.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newUser = new User
                {
                    Name = name,
                    PasswordHash = PasswordHasher.HashPassword(password),
                    Role = role,
                };

                var createdUser = await _userService.CreateAsync(newUser);
                await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has created new user {name}.", User = _currentUser });
                _users.Add(createdUser);
                b_createUserPanel.Visibility = Visibility.Collapsed;
                
                UpdateUserList(_users);

                MessageBox.Show("User was successfully created!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"Невдалося створити користувача: {ex.Message}");
            //}
            catch (DbUpdateException ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"Помилка бази даних: {message}");
            }
        }

        private void CloseCreatePanel_Click(object sender, RoutedEventArgs e)
        {
            b_createUserPanel.Visibility = Visibility.Collapsed;
        }

        private void OnFilterChanged(object sender, EventArgs e)
        {
            if (tb_userNameSearch == null || cb_filterRoles == null || _users == null) return;

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            string searchText = tb_userNameSearch.Text.ToLower().Trim();

            var selected = cb_filterRoles.SelectedItem;
            string selectedRoleName = selected?.GetType().GetProperty("Name")?.GetValue(selected)?.ToString() ?? "Всі ролі";

            var filtered = _users.Where(u => { bool matchesName = string.IsNullOrEmpty(searchText) ||
                                   (u.Name != null && u.Name.ToLower().Contains(searchText));

                bool matchesRole = selectedRoleName == "Всі ролі" || (u.Role != null && u.Role.Name == selectedRoleName);

                return matchesName && matchesRole;
            }).ToList();

            dg_userList.ItemsSource = filtered;
        }

        // logs tab

        private void UpdateLogList(List<ActionLog> logs)
        {
            dg_logList.ItemsSource = logs;
            dg_logList.Items.Refresh();
        }

        private void OnLogFilterChanged(object sender, EventArgs e)
        {
            if (tb_logSearch == null || dp_logDate == null || _logs == null) return;

            ApplyLogFilters();
        }

        private void ApplyLogFilters()
        {
            string searchText = tb_logSearch.Text.ToLower().Trim();
            DateTime? selectedDate = dp_logDate.SelectedDate;

            var filtered = _logs.Where(log =>
            {
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                     (log.Action != null && log.Action.ToLower().Contains(searchText));

                bool matchesDate = !selectedDate.HasValue ||
                                   log.CreatedAt.Date == selectedDate.Value.Date;

                return matchesSearch && matchesDate;
            })
            .OrderByDescending(l => l.CreatedAt)
            .ToList();

            UpdateLogList(filtered);
        }

        private void LogDetails_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var selectedLog = button?.DataContext as ActionLog;

            if (selectedLog != null)
            {
                tb_logDetailTime.Text = selectedLog.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss");
                tb_logDetailUser.Text = selectedLog.User?.Name ?? "Невідомий користувач";
                tb_logDetailAction.Text = selectedLog.Action ?? "Без опису";

                _chosenLog = selectedLog;
                b_logDetailsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Невдалося відкрити деталі лога!", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseLogDetails_Click(object sender, RoutedEventArgs e)
        {
            _chosenLog = null;
            b_logDetailsPanel.Visibility = Visibility.Collapsed;
        }

        private async void DeleteLog_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var selectedLog = button?.DataContext as ActionLog;

            if (selectedLog == null)
            {
                MessageBox.Show("Не вдалося визначити лог для видалення!", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = MessageBox.Show(
                    "Ви впевнені, що хочете видалити цей лог?",
                    "Підтвердження видалення",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _logService.DeleteAsync(selectedLog.Id);

                    _logs.Remove(selectedLog);
                    UpdateLogList(_logs);
                    ApplyLogFilters();

                    if (_chosenLog?.Id == selectedLog.Id)
                    {
                        b_logDetailsPanel.Visibility = Visibility.Collapsed;
                        _chosenLog = null;
                    }

                    MessageBox.Show("Лог успішно видалено!", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка видалення лога: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ClearAllLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_logs.Count == 0)
                {
                    MessageBox.Show("Логи відсутні!", "Інформація",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"УВАГА! Ви впевнені, що хочете видалити ВСІ логи ({_logs.Count} записів)?\n\nЦю дію неможливо скасувати!",
                    "Підтвердження очищення",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var doubleConfirm = MessageBox.Show(
                        "Остаточне підтвердження. Видалити всі логи?",
                        "Остаточне підтвердження",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Stop);

                    if (doubleConfirm == MessageBoxResult.Yes)
                    {
                        foreach (var log in _logs.ToList())
                        {
                            await _logService.DeleteAsync(log.Id);
                        }

                        var clearLog = new ActionLog
                        {
                            Action = $"{_currentUser.Name} очистив всі логи системи ({_logs.Count} записів)",
                            User = _currentUser,
                            CreatedAt = DateTime.Now
                        };
                        await _logService.CreateAsync(clearLog);

                        _logs.Clear();
                        _logs.Add(clearLog);
                        UpdateLogList(_logs);

                        b_logDetailsPanel.Visibility = Visibility.Collapsed;
                        _chosenLog = null;

                        MessageBox.Show("Всі логи успішно видалено!", "Успіх",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка очищення логів: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // products tab

        private void UpdateProductList(List<Product> products)
        {
            dg_productList.ItemsSource = products;
            dg_productList.Items.Refresh();
        }

        private void OnProductFilterChanged(object sender, EventArgs e)
        {
            if (tb_productNameSearch == null || cb_filterStockStatus == null || _products == null)
                return;

            ApplyProductFilters();
        }

        private void ApplyProductFilters()
        {
            string searchText = tb_productNameSearch.Text.ToLower().Trim();

            var selectedItem = cb_filterStockStatus.SelectedItem as ComboBoxItem;
            string selectedStatus = selectedItem?.Content?.ToString() ?? "All products";

            var filtered = _products.Where(p =>
            {
                bool matchesName = string.IsNullOrEmpty(searchText) ||
                                   (p.Name != null && p.Name.ToLower().Contains(searchText));

                bool matchesStatus = selectedStatus == "All products" ||
                                     p.Status == selectedStatus;

                return matchesName && matchesStatus;
            }).ToList();

            UpdateProductList(filtered);
        }

        private void ProductDetails_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var selectedProduct = button?.DataContext as Product;

            if (selectedProduct != null)
            {
                tb_productEditName.Text = selectedProduct.Name;
                tb_productEditUnit.Text = selectedProduct.Unit;
                tb_productEditMinStock.Text = selectedProduct.MinimumStock.ToString();

                _chosenProduct = selectedProduct;
                b_productDetailsPanel.Visibility = Visibility.Visible;
                b_createProductPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Невдалося відкрити товар!", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseProductDetails_Click(object sender, RoutedEventArgs e)
        {
            _chosenProduct = null;
            b_productDetailsPanel.Visibility = Visibility.Collapsed;
        }

        private async void EditProductSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = tb_productEditName.Text.Trim();
                string unit = tb_productEditUnit.Text.Trim();

                if (!decimal.TryParse(tb_productEditMinStock.Text, out decimal minStock) || minStock < 0)
                {
                    MessageBox.Show("Введіть коректний мінімальний поріг!", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit))
                {
                    MessageBox.Show("Назва та одиниця виміру не можуть бути пустими!", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var updatedProduct = new Product
                {
                    Id = _chosenProduct.Id,
                    Name = name,
                    Description = _chosenProduct.Description,
                    Unit = unit,
                    Stock = _chosenProduct.Stock,
                    MinimumStock = minStock,
                    CreatedAt = _chosenProduct.CreatedAt
                };

                var result = await _productService.UpdateAsync(updatedProduct.Id, updatedProduct);

                _products.Remove(_chosenProduct);
                _products.Add(result);

                UpdateProductList(_products);
                ApplyProductFilters();

                b_productDetailsPanel.Visibility = Visibility.Collapsed;
                _chosenProduct = null;

                MessageBox.Show("Товар успішно оновлено!", "Успіх",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка редагування товару: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    $"Ви впевнені, що хочете видалити товар '{_chosenProduct.Name}'?",
                    "Підтвердження видалення",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _productService.DeleteAsync(_chosenProduct.Id);

                    _products.Remove(_chosenProduct);
                    UpdateProductList(_products);
                    ApplyProductFilters();

                    b_productDetailsPanel.Visibility = Visibility.Collapsed;
                    _chosenProduct = null;

                    MessageBox.Show("Товар успішно видалено!", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка видалення товару: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateProduct_Click(object sender, RoutedEventArgs e)
        {
            tb_createProductName.Clear();
            tb_createProductUnit.Clear();
            tb_createProductStock.Text = "0";

            b_createProductPanel.Visibility = Visibility.Visible;
            b_productDetailsPanel.Visibility = Visibility.Collapsed;
        }

        private void CloseCreateProduct_Click(object sender, RoutedEventArgs e)
        {
            b_createProductPanel.Visibility = Visibility.Collapsed;
        }

        private async void ConfirmCreateProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = tb_createProductName.Text.Trim();
                string unit = tb_createProductUnit.Text.Trim();

                if (!decimal.TryParse(tb_createProductStock.Text, out decimal stock) || stock < 0)
                {
                    MessageBox.Show("Введіть коректний початковий залишок!", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit))
                {
                    MessageBox.Show("Назва та одиниця виміру не можуть бути пустими!", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingProduct = _products.FirstOrDefault(p =>
                    p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (existingProduct != null)
                {
                    MessageBox.Show($"Товар з назвою '{name}' вже існує!", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newProduct = new Product
                {
                    Name = name,
                    Description = string.Empty,
                    Unit = unit,
                    Stock = stock,
                    MinimumStock = 0,
                    CreatedAt = DateTime.Now
                };

                var createdProduct = await _productService.CreateAsync(newProduct);

                _products.Add(createdProduct);
                UpdateProductList(_products);
                ApplyProductFilters();

                b_createProductPanel.Visibility = Visibility.Collapsed;

                MessageBox.Show("Товар успішно створено!", "Успіх",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка створення товару: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
