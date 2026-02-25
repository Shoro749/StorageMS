using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Data.Context;
using Data.Models;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.IO.Font;
using iText.Kernel.Font;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Service.Interfaces;
using Service.Services;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using Xceed.Words.NET;
using Document = iText.Layout.Document;
using PdfDocument = iText.Kernel.Pdf.PdfDocument;
using PdfParagraph = iText.Layout.Element.Paragraph;
using PdfTable = iText.Layout.Element.Table;
using PdfTextAlign = iText.Layout.Properties.TextAlignment;
using PdfWriter = iText.Kernel.Pdf.PdfWriter;
using TableDesign = Xceed.Document.NET.TableDesign;
using WordAlignment = Xceed.Document.NET.Alignment;

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
        private readonly IService<Incoming> _incomingService;
        private readonly IService<OutgoingItem> _itemService;
        private readonly IService<OutgoingRequest> _requestService;
        
        private List<User> _users;
        private List<Product> _products = new List<Product>();
        private Product _chosenProduct = null;
        private List<ActionLog> _logs = new List<ActionLog>();
        private ActionLog _chosenLog = null;
        private DateTime? _lastBackupTime = null;
        public AdminWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _userService = new UserService(context);
            _roleService = new Service<Role>(context);
            _logService = new Service<ActionLog>(context);
            _productService = new Service<Product>(context);
            _incomingService = new Service<Incoming>(context);
            _itemService = new Service<OutgoingItem>(context);
            _requestService = new Service<OutgoingRequest>(context);

            cb_ReportType.SelectionChanged += OnReportTypeChanged;

            this.Loaded += AdminWindow_Loaded;
        }

        private async void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var roles = await _roleService.GetAllAsync();
                _users = await _userService.GetAllAsync();
                _products = await _productService.GetAllAsync();
                _logs = await _logService.GetAllAsync();

                cb_userRole.ItemsSource = roles;
                cb_userRole.DisplayMemberPath = "Name";
                cb_createRole.ItemsSource = roles;
                cb_createRole.DisplayMemberPath = "Name";

                var filterList = new List<object> { new { Id = 0, Name = "Всі ролі" } };
                filterList.AddRange(roles);
                cb_filterRoles.ItemsSource = filterList;
                cb_filterRoles.DisplayMemberPath = "Name";
                cb_filterRoles.SelectedIndex = 0;

                UpdateUserList(_users);
                UpdateProductList(_products);

                _logs = _logs.OrderByDescending(l => l.CreatedAt).ToList();
                UpdateLogList(_logs);

                var backupLog = _logs
                    .Where(l => l.Action.Contains("created a backup"))
                    .OrderByDescending(l => l.CreatedAt)
                    .FirstOrDefault();

                if (backupLog != null)
                {
                    _lastBackupTime = backupLog.CreatedAt;
                    UpdateBackupLabel();
                }

                dp_ReportEnd.SelectedDate = DateTime.Today;
                dp_ReportStart.SelectedDate = DateTime.Today.AddMonths(-1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критична помилка ініціалізації: {ex.Message}");
            }
        }

        private void UpdateBackupLabel()
        {
            if (_lastBackupTime.HasValue)
            {
                var time = _lastBackupTime.Value;
                string timeText;

                if (time.Date == DateTime.Today)
                {
                    timeText = $"сьогодні о {time:HH:mm}";
                }
                else if (time.Date == DateTime.Today.AddDays(-1))
                {
                    timeText = $"вчора о {time:HH:mm}";
                }
                else
                {
                    timeText = time.ToString("dd.MM.yyyy о HH:mm");
                }

                tb_LastBackup.Text = $"Останнє резервування: {timeText}";
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

                string passwordHash;
                if (string.IsNullOrEmpty(password))
                {
                    passwordHash = _chosenUser.PasswordHash;
                }
                else
                {
                    passwordHash = PasswordHasher.HashPassword(password);
                }

                var newUser = new User
                {
                    Id = _chosenUser.Id,
                    Name = name,
                    PasswordHash = passwordHash,
                    Role = role
                };

                var updatedUser = await _userService.UpdateUserAsync(newUser.Id, newUser.Name, newUser.PasswordHash, newUser.Role);
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
            if (products.Count() == 0) return;
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

                var newLog = await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has edited product (id={result.Id}).", User = _currentUser });
                _logs.Add(newLog);
                UpdateLogList(_logs);

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

                    var newLog = await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has deleted product {_chosenProduct}.", User = _currentUser });
                    _logs.Add(newLog);
                    UpdateLogList(_logs);

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
                var newLog = await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has created new product {name}.", User = _currentUser });

                _products.Add(createdProduct);
                UpdateProductList(_products);
                ApplyProductFilters();

                b_createProductPanel.Visibility = Visibility.Collapsed;

                _logs.Add(newLog);
                UpdateLogList(_logs);

                MessageBox.Show("Товар успішно створено!", "Успіх",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка створення товару: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            this.Close();
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dg_ReportPreview.ItemsSource == null)
                {
                    MessageBox.Show("Спочатку сформуйте звіт!", "Увага",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    FileName = $"Звіт_{DateTime.Now:yyyy-MM-dd_HH-mm}",
                    Filter = "Word документ (*.docx)|*.docx|PDF документ (*.pdf)|*.pdf|Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportCurrentReport(saveDialog.FileName);

                    var result = MessageBox.Show("Звіт успішно експортовано!\n\nВідкрити файл?",
                        "Успіх", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                        Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportOutgoingPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!dp_ReportStart.SelectedDate.HasValue || !dp_ReportEnd.SelectedDate.HasValue)
                {
                    MessageBox.Show("Оберіть період!", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DateTime start = dp_ReportStart.SelectedDate.Value.Date;
                DateTime end = dp_ReportEnd.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);

                var requests = await _requestService.GetAllAsync();
                var filtered = requests
                    .Where(r => r.CreatedAt >= start && r.CreatedAt <= end && r.Status == "Completed")
                    .ToList();

                if (!filtered.Any())
                {
                    MessageBox.Show("Немає даних за обраний період!", "Інформація",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    FileName = $"Видаткова_накладна_{DateTime.Now:yyyy-MM-dd_HH-mm}",
                    Filter = "Word документ (*.docx)|*.docx|PDF документ (*.pdf)|*.pdf|Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();

                    switch (extension)
                    {
                        case ".docx":
                            CreateOutgoingDocx(saveDialog.FileName, filtered, start, end);
                            break;
                        case ".pdf":
                            CreateOutgoingPdf(saveDialog.FileName, filtered, start, end);
                            break;
                        case ".xlsx":
                            CreateOutgoingExcel(saveDialog.FileName, filtered, start, end);
                            break;
                        case ".csv":
                            CreateOutgoingCsv(saveDialog.FileName, filtered);
                            break;
                    }

                    var result = MessageBox.Show("Накладну успішно створено!\n\nВідкрити файл?",
                        "Успіх", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                        Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateIncomingDocx(string filePath, List<Incoming> data, DateTime start, DateTime end)
        {
            using (var doc = DocX.Create(filePath))
            {
                var title = doc.InsertParagraph("ПРИБУТКОВА НАКЛАДНА")
                    .FontSize(18)
                    .Bold()
                    .Alignment = WordAlignment.center;

                doc.InsertParagraph($"Дата формування: {DateTime.Now:dd.MM.yyyy HH:mm}")
                    .FontSize(10)
                    .Alignment = WordAlignment.center;

                doc.InsertParagraph($"Період: {start:dd.MM.yyyy} - {end:dd.MM.yyyy}")
                    .FontSize(10)
                    .Alignment = WordAlignment.center;

                doc.InsertParagraph();

                var table = doc.AddTable(data.Count + 1, 6);
                table.Design = TableDesign.LightGridAccent1;

                table.Rows[0].Cells[0].Paragraphs[0].Append("№").Bold();
                table.Rows[0].Cells[1].Paragraphs[0].Append("Дата").Bold();
                table.Rows[0].Cells[2].Paragraphs[0].Append("Товар").Bold();
                table.Rows[0].Cells[3].Paragraphs[0].Append("Кількість").Bold();
                table.Rows[0].Cells[4].Paragraphs[0].Append("Од.").Bold();
                table.Rows[0].Cells[5].Paragraphs[0].Append("Комірник").Bold();

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    table.Rows[i + 1].Cells[0].Paragraphs[0].Append((i + 1).ToString());
                    table.Rows[i + 1].Cells[1].Paragraphs[0].Append(item.ReceivedAt.ToString("dd.MM.yyyy HH:mm"));
                    table.Rows[i + 1].Cells[2].Paragraphs[0].Append(item.Product?.Name ?? "");
                    table.Rows[i + 1].Cells[3].Paragraphs[0].Append(item.Quantity.ToString());
                    table.Rows[i + 1].Cells[4].Paragraphs[0].Append(item.Product?.Unit ?? "");
                    table.Rows[i + 1].Cells[5].Paragraphs[0].Append(item.ReceivedBy?.Name ?? "");
                }

                doc.InsertTable(table);
                doc.InsertParagraph();
                doc.InsertParagraph($"Всього позицій: {data.Count}").Bold();

                doc.Save();
            }
        }

        private void CreateOutgoingDocx(string filePath, List<OutgoingRequest> data, DateTime start, DateTime end)
        {
            using (var doc = DocX.Create(filePath))
            {
                var title = doc.InsertParagraph("ВИДАТКОВА НАКЛАДНА")
                    .FontSize(18)
                    .Bold()
                    .Alignment = WordAlignment.center;

                doc.InsertParagraph($"Дата формування: {DateTime.Now:dd.MM.yyyy HH:mm}")
                    .FontSize(10)
                    .Alignment = WordAlignment.center;

                doc.InsertParagraph($"Період: {start:dd.MM.yyyy} - {end:dd.MM.yyyy}")
                    .FontSize(10)
                    .Alignment = WordAlignment.center;

                doc.InsertParagraph();

                int totalItems = data.Sum(r => r.Items.Count);
                var table = doc.AddTable(totalItems + 1, 7);
                table.Design = TableDesign.LightGridAccent1;

                table.Rows[0].Cells[0].Paragraphs[0].Append("№").Bold();
                table.Rows[0].Cells[1].Paragraphs[0].Append("Заявка").Bold();
                table.Rows[0].Cells[2].Paragraphs[0].Append("Дата").Bold();
                table.Rows[0].Cells[3].Paragraphs[0].Append("Товар").Bold();
                table.Rows[0].Cells[4].Paragraphs[0].Append("К-сть").Bold();
                table.Rows[0].Cells[5].Paragraphs[0].Append("Од.").Bold();
                table.Rows[0].Cells[6].Paragraphs[0].Append("Менеджер").Bold();

                int rowIndex = 1;
                foreach (var request in data)
                {
                    foreach (var item in request.Items)
                    {
                        table.Rows[rowIndex].Cells[0].Paragraphs[0].Append(rowIndex.ToString());
                        table.Rows[rowIndex].Cells[1].Paragraphs[0].Append(request.Id.ToString());
                        table.Rows[rowIndex].Cells[2].Paragraphs[0].Append(request.CreatedAt.ToString("dd.MM.yyyy"));
                        table.Rows[rowIndex].Cells[3].Paragraphs[0].Append(item.Product?.Name ?? "");
                        table.Rows[rowIndex].Cells[4].Paragraphs[0].Append(item.Quantity.ToString());
                        table.Rows[rowIndex].Cells[5].Paragraphs[0].Append(item.Product?.Unit ?? "");
                        table.Rows[rowIndex].Cells[6].Paragraphs[0].Append(request.CreatedBy?.Name ?? "");
                        rowIndex++;
                    }
                }

                doc.InsertTable(table);
                doc.InsertParagraph();
                doc.InsertParagraph($"Всього позицій: {totalItems} (заявок: {data.Count})").Bold();

                doc.Save();
            }
        }

        private void CreateIncomingPdf(string filePath, List<Incoming> data, DateTime start, DateTime end)
        {
            using (var writer = new PdfWriter(filePath))
            using (var pdf = new PdfDocument(writer))
            using (var document = new Document(pdf))
            {
                var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                document.SetFont(font);

                document.Add(new PdfParagraph("ПРИБУТКОВА НАКЛАДНА")
                    .SetFontSize(18)
                    .SetTextAlignment(PdfTextAlign.CENTER));

                document.Add(new PdfParagraph($"Дата формування: {DateTime.Now:dd.MM.yyyy HH:mm}")
                    .SetFontSize(10)
                    .SetTextAlignment(PdfTextAlign.CENTER));

                var table = new PdfTable(6);
                table.AddHeaderCell("№");
                table.AddHeaderCell("Дата");
                table.AddHeaderCell("Товар");
                table.AddHeaderCell("Кількість");
                table.AddHeaderCell("Од.");
                table.AddHeaderCell("Комірник");

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    table.AddCell((i + 1).ToString());
                    table.AddCell(item.ReceivedAt.ToString("dd.MM.yyyy"));
                    table.AddCell(item.Product?.Name ?? "");
                    table.AddCell(item.Quantity.ToString());
                    table.AddCell(item.Product?.Unit ?? "");
                    table.AddCell(item.ReceivedBy?.Name ?? "");
                }

                document.Add(table);
                document.Add(new PdfParagraph($"\nВсього позицій: {data.Count}"));
            }
        }

        private void CreateOutgoingPdf(string filePath, List<OutgoingRequest> data, DateTime start, DateTime end)
        {
            using (var writer = new PdfWriter(filePath))
            using (var pdf = new PdfDocument(writer))
            using (var document = new Document(pdf))
            {
                var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                document.SetFont(font);

                document.Add(new PdfParagraph("ВИДАТКОВА НАКЛАДНА")
                    .SetFontSize(18)
                    .SetTextAlignment(PdfTextAlign.CENTER));

                var table = new PdfTable(7);
                table.AddHeaderCell("№");
                table.AddHeaderCell("Заявка");
                table.AddHeaderCell("Дата");
                table.AddHeaderCell("Товар");
                table.AddHeaderCell("К-сть");
                table.AddHeaderCell("Од.");
                table.AddHeaderCell("Менеджер");

                int index = 1;
                foreach (var request in data)
                {
                    foreach (var item in request.Items)
                    {
                        table.AddCell(index.ToString());
                        table.AddCell(request.Id.ToString());
                        table.AddCell(request.CreatedAt.ToString("dd.MM.yyyy"));
                        table.AddCell(item.Product?.Name ?? "");
                        table.AddCell(item.Quantity.ToString());
                        table.AddCell(item.Product?.Unit ?? "");
                        table.AddCell(request.CreatedBy?.Name ?? "");
                        index++;
                    }
                }

                document.Add(table);
            }
        }

        private void CreateIncomingExcel(string filePath, List<Incoming> data, DateTime start, DateTime end)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Надходження");

                worksheet.Cell(1, 1).Value = "ПРИБУТКОВА НАКЛАДНА";
                worksheet.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;
                worksheet.Range(1, 1, 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell(3, 1).Value = "№";
                worksheet.Cell(3, 2).Value = "Дата";
                worksheet.Cell(3, 3).Value = "Товар";
                worksheet.Cell(3, 4).Value = "Кількість";
                worksheet.Cell(3, 5).Value = "Од.";
                worksheet.Cell(3, 6).Value = "Комірник";
                worksheet.Range(3, 1, 3, 6).Style.Font.Bold = true;

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    worksheet.Cell(i + 4, 1).Value = i + 1;
                    worksheet.Cell(i + 4, 2).Value = item.ReceivedAt.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cell(i + 4, 3).Value = item.Product?.Name ?? "";
                    worksheet.Cell(i + 4, 4).Value = item.Quantity;
                    worksheet.Cell(i + 4, 5).Value = item.Product?.Unit ?? "";
                    worksheet.Cell(i + 4, 6).Value = item.ReceivedBy?.Name ?? "";
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        private void CreateOutgoingExcel(string filePath, List<OutgoingRequest> data, DateTime start, DateTime end)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Відвантаження");

                worksheet.Cell(1, 1).Value = "ВИДАТКОВА НАКЛАДНА";
                worksheet.Range(1, 1, 1, 7).Merge().Style.Font.Bold = true;

                worksheet.Cell(3, 1).Value = "№";
                worksheet.Cell(3, 2).Value = "Заявка";
                worksheet.Cell(3, 3).Value = "Дата";
                worksheet.Cell(3, 4).Value = "Товар";
                worksheet.Cell(3, 5).Value = "К-сть";
                worksheet.Cell(3, 6).Value = "Од.";
                worksheet.Cell(3, 7).Value = "Менеджер";

                int row = 4;
                foreach (var request in data)
                {
                    foreach (var item in request.Items)
                    {
                        worksheet.Cell(row, 1).Value = row - 3;
                        worksheet.Cell(row, 2).Value = request.Id;
                        worksheet.Cell(row, 3).Value = request.CreatedAt.ToString("dd.MM.yyyy");
                        worksheet.Cell(row, 4).Value = item.Product?.Name ?? "";
                        worksheet.Cell(row, 5).Value = item.Quantity;
                        worksheet.Cell(row, 6).Value = item.Product?.Unit ?? "";
                        worksheet.Cell(row, 7).Value = request.CreatedBy?.Name ?? "";
                        row++;
                    }
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        private void CreateIncomingCsv(string filePath, List<Incoming> data)
        {
            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                csv.WriteRecords(data.Select(i => new
                {
                    Дата = i.ReceivedAt.ToString("dd.MM.yyyy HH:mm"),
                    Товар = i.Product?.Name ?? "",
                    Кількість = i.Quantity,
                    Одиниця = i.Product?.Unit ?? "",
                    Комірник = i.ReceivedBy?.Name ?? ""
                }));
            }
        }

        private void CreateOutgoingCsv(string filePath, List<OutgoingRequest> data)
        {
            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                csv.WriteRecords(data.SelectMany(r => r.Items.Select(item => new
                {
                    Заявка = r.Id,
                    Дата = r.CreatedAt.ToString("dd.MM.yyyy"),
                    Товар = item.Product?.Name ?? "",
                    Кількість = item.Quantity,
                    Одиниця = item.Product?.Unit ?? "",
                    Менеджер = r.CreatedBy?.Name ?? ""
                })));
            }
        }

        private async void GeneratePreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!dp_ReportStart.SelectedDate.HasValue || !dp_ReportEnd.SelectedDate.HasValue)
                {
                    MessageBox.Show("Будь ласка, оберіть період звіту!", "Увага",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DateTime startDate = dp_ReportStart.SelectedDate.Value.Date;
                DateTime endDate = dp_ReportEnd.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);

                if (startDate > endDate)
                {
                    MessageBox.Show("Початкова дата не може бути пізніше кінцевої!", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var selectedItem = cb_ReportType.SelectedItem as ComboBoxItem;
                string reportType = selectedItem?.Content?.ToString() ?? "";

                switch (reportType)
                {
                    case "Залишки на складі":
                        await GenerateStockReport();
                        break;
                    case "Надходження (Incoming)":
                        await GenerateIncomingReport(startDate, endDate);
                        break;
                    case "Відвантаження (Outgoing)":
                        await GenerateOutgoingReport(startDate, endDate);
                        break;
                    case "Журнал дій (Audit)":
                        await GenerateAuditReport(startDate, endDate);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка генерації звіту: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task GenerateStockReport()
        {
            var products = await _productService.GetAllAsync();

            var reportData = products.Select(p => new
            {
                Товар = p.Name,
                Од_виміру = p.Unit,
                Залишок = p.Stock,
                Мінімум = p.MinimumStock,
                Статус = p.Status == "OK" ? "OK" : p.Status == "Low" ? "Низький" : "Немає"
            }).ToList();

            dg_ReportPreview.ItemsSource = reportData;
            lbl_ReportTitle.Text = $"Залишки товарів на складі (станом на {DateTime.Now:dd.MM.yyyy HH:mm})";
        }

        private async Task GenerateIncomingReport(DateTime start, DateTime end)
        {
            var incomings = await _incomingService.GetAllAsync();

            var filtered = incomings
                .Where(i => i.ReceivedAt >= start && i.ReceivedAt <= end)
                .OrderByDescending(i => i.ReceivedAt)
                .Select(i => new
                {
                    Дата = i.ReceivedAt.ToString("dd.MM.yyyy HH:mm"),
                    Товар = i.Product?.Name ?? "Невідомо",
                    Кількість = i.Quantity,
                    Одиниця = i.Product?.Unit ?? "",
                    Комірник = i.ReceivedBy?.Name ?? "Невідомо"
                })
                .ToList();

            dg_ReportPreview.ItemsSource = filtered;
            lbl_ReportTitle.Text = $"Надходження товарів ({start:dd.MM.yyyy} - {end:dd.MM.yyyy})";
        }

        private async Task GenerateOutgoingReport(DateTime start, DateTime end)
        {
            var requests = await _requestService.GetAllAsync();

            var filtered = requests
                .Where(r => r.CreatedAt >= start && r.CreatedAt <= end && r.Status == "Completed")
                .SelectMany(r => r.Items.Select(item => new
                {
                    Заявка = r.Id,
                    Дата = r.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                    Товар = item.Product?.Name ?? "Невідомо",
                    Кількість = item.Quantity,
                    Одиниця = item.Product?.Unit ?? "",
                    Статус = "Виконано",
                    Менеджер = r.CreatedBy?.Name ?? "Невідомо"
                }))
                .ToList();

            dg_ReportPreview.ItemsSource = filtered;
            lbl_ReportTitle.Text = $"Відвантаження товарів ({start:dd.MM.yyyy} - {end:dd.MM.yyyy})";
        }

        private async Task GenerateAuditReport(DateTime start, DateTime end)
        {
            var logs = await _logService.GetAllAsync();

            var filtered = logs
                .Where(l => l.CreatedAt >= start && l.CreatedAt <= end)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    Дата_та_час = l.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss"),
                    Користувач = l.User?.Name ?? "Невідомо",
                    Дія = l.Action
                })
                .ToList();

            dg_ReportPreview.ItemsSource = filtered;
            lbl_ReportTitle.Text = $"Журнал дій користувачів ({start:dd.MM.yyyy} - {end:dd.MM.yyyy})";
        }

        private void OnReportTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            dg_ReportPreview.ItemsSource = null;
            lbl_ReportTitle.Text = "Оберіть період та натисніть 'Сформувати перегляд'";
        }

        private async void ExportIncomingPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!dp_ReportStart.SelectedDate.HasValue || !dp_ReportEnd.SelectedDate.HasValue)
                {
                    MessageBox.Show("Оберіть період!", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DateTime start = dp_ReportStart.SelectedDate.Value.Date;
                DateTime end = dp_ReportEnd.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);

                var incomings = await _incomingService.GetAllAsync();
                var filtered = incomings.Where(i => i.ReceivedAt >= start && i.ReceivedAt <= end).ToList();

                if (!filtered.Any())
                {
                    MessageBox.Show("Немає даних за обраний період!", "Інформація",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    FileName = $"Прибуткова_накладна_{DateTime.Now:yyyy-MM-dd_HH-mm}",
                    Filter = "Word документ (*.docx)|*.docx|PDF документ (*.pdf)|*.pdf|Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();

                    switch (extension)
                    {
                        case ".docx":
                            CreateIncomingDocx(saveDialog.FileName, filtered, start, end);
                            break;
                        case ".pdf":
                            CreateIncomingPdf(saveDialog.FileName, filtered, start, end);
                            break;
                        case ".xlsx":
                            CreateIncomingExcel(saveDialog.FileName, filtered, start, end);
                            break;
                        case ".csv":
                            CreateIncomingCsv(saveDialog.FileName, filtered);
                            break;
                    }

                    var result = MessageBox.Show("Накладну успішно створено!\n\nВідкрити файл?",
                        "Успіх", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                        Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCurrentReport(string filePath)
        {
            var items = dg_ReportPreview.ItemsSource;
            if (items == null) return;

            string extension = System.IO.Path.GetExtension(filePath).ToLower();
            var reportTitle = lbl_ReportTitle.Text;

            switch (extension)
            {
                case ".docx":
                    ExportToDocx(filePath, items, reportTitle);
                    break;
                case ".pdf":
                    ExportToPdf(filePath, items, reportTitle);
                    break;
                case ".xlsx":
                    ExportToExcel(filePath, items, reportTitle);
                    break;
                case ".csv":
                    ExportToCsv(filePath, items);
                    break;
            }
        }

        private void ExportToDocx(string filePath, object data, string title)
        {
            using (var doc = DocX.Create(filePath))
            {
                doc.InsertParagraph(title)
                    .FontSize(16)
                    .Bold()
                    .Alignment = WordAlignment.center;

                doc.InsertParagraph($"Дата формування: {DateTime.Now:dd.MM.yyyy HH:mm}")
                    .FontSize(10)
                    .Alignment = WordAlignment.center;

                doc.InsertParagraph();

                var list = ((System.Collections.IEnumerable)data).Cast<object>().ToList();
                if (!list.Any()) return;

                var properties = list[0].GetType().GetProperties();
                var table = doc.AddTable(list.Count + 1, properties.Length);
                table.Design = TableDesign.LightGridAccent1;

                for (int i = 0; i < properties.Length; i++)
                {
                    table.Rows[0].Cells[i].Paragraphs[0].Append(properties[i].Name).Bold();
                }

                for (int row = 0; row < list.Count; row++)
                {
                    for (int col = 0; col < properties.Length; col++)
                    {
                        var value = properties[col].GetValue(list[row])?.ToString() ?? "";
                        table.Rows[row + 1].Cells[col].Paragraphs[0].Append(value);
                    }
                }

                doc.InsertTable(table);
                doc.InsertParagraph();
                doc.InsertParagraph($"Всього записів: {list.Count}").Bold();

                doc.Save();
            }
        }

        private void ExportToPdf(string filePath, object data, string title)
        {
            using (var writer = new PdfWriter(filePath))
            using (var pdf = new PdfDocument(writer))
            using (var document = new Document(pdf))
            {
                var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                document.SetFont(font);

                document.Add(new PdfParagraph(title)
                    .SetFontSize(16)
                    .SetTextAlignment(PdfTextAlign.CENTER));

                document.Add(new PdfParagraph($"Дата формування: {DateTime.Now:dd.MM.yyyy HH:mm}")
                    .SetFontSize(10)
                    .SetTextAlignment(PdfTextAlign.CENTER));

                document.Add(new PdfParagraph("\n"));

                var list = ((System.Collections.IEnumerable)data).Cast<object>().ToList();
                if (!list.Any()) return;

                var properties = list[0].GetType().GetProperties();
                var table = new PdfTable(properties.Length);

                foreach (var prop in properties)
                {
                    table.AddHeaderCell(new iText.Layout.Element.Cell().Add(new PdfParagraph(prop.Name)));
                }

                foreach (var item in list)
                {
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(item)?.ToString() ?? "";
                        table.AddCell(value);
                    }
                }

                document.Add(table);
                document.Add(new PdfParagraph($"\nВсього записів: {list.Count}"));
            }
        }

        private void ExportToExcel(string filePath, object data, string title)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Звіт");

                var list = ((System.Collections.IEnumerable)data).Cast<object>().ToList();
                if (!list.Any()) return;

                worksheet.Cell(1, 1).Value = title;
                var properties = list[0].GetType().GetProperties();
                worksheet.Range(1, 1, 1, properties.Length).Merge().Style.Font.Bold = true;
                worksheet.Range(1, 1, 1, properties.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell(2, 1).Value = $"Дата формування: {DateTime.Now:dd.MM.yyyy HH:mm}";
                worksheet.Range(2, 1, 2, properties.Length).Merge();

                for (int i = 0; i < properties.Length; i++)
                {
                    worksheet.Cell(4, i + 1).Value = properties[i].Name;
                    worksheet.Cell(4, i + 1).Style.Font.Bold = true;
                }

                for (int row = 0; row < list.Count; row++)
                {
                    for (int col = 0; col < properties.Length; col++)
                    {
                        var value = properties[col].GetValue(list[row]);
                        worksheet.Cell(row + 5, col + 1).Value = value?.ToString() ?? "";
                    }
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        private void ExportToCsv(string filePath, object data)
        {
            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                var list = ((System.Collections.IEnumerable)data).Cast<object>().ToList();
                if (!list.Any()) return;

                csv.WriteRecords(list);
            }
        }

        private async void ExportToJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Створити повну резервну копію бази даних?\n\nЦе може зайняти деякий час.",
                    "Підтвердження експорту",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                var saveDialog = new SaveFileDialog
                {
                    FileName = $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json",
                    Filter = "JSON файли (*.json)|*.json|Всі файли (*.*)|*.*",
                    Title = "Зберегти резервну копію"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                var backup = new BackupData
                {
                    BackupDate = DateTime.Now,
                    Users = await _userService.GetAllAsync(),
                    Roles = await _roleService.GetAllAsync(),
                    Products = await _productService.GetAllAsync(),
                    Incomings = await _incomingService.GetAllAsync(),
                    OutgoingRequests = await _requestService.GetAllAsync(),
                    OutgoingItems = await _itemService.GetAllAsync(),
                    ActionLogs = await _logService.GetAllAsync()
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(backup, options);

                await File.WriteAllTextAsync(saveDialog.FileName, json);

                await _logService.CreateAsync(new ActionLog
                {
                    Action = $"{_currentUser.Name} created a backup ({backup.Users.Count} users, {backup.Products.Count} products, {backup.OutgoingRequests.Count} requests)",
                    User = _currentUser,
                });

                _lastBackupTime = DateTime.Now;
                UpdateBackupLabel();

                MessageBox.Show(
                    $"Резервну копію успішно створено!\n\nФайл: {saveDialog.FileName}\n\nВсього збережено:\n" +
                    $"• Користувачів: {backup.Users.Count}\n" +
                    $"• Ролей: {backup.Roles.Count}\n" +
                    $"• Товарів: {backup.Products.Count}\n" +
                    $"• Надходжень: {backup.Incomings.Count}\n" +
                    $"• Заявок: {backup.OutgoingRequests.Count}\n" +
                    $"• Позицій у заявках: {backup.OutgoingItems.Count}\n" +
                    $"• Логів: {backup.ActionLogs.Count}",
                    "Успіх",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Помилка створення резервної копії:\n\n{ex.Message}",
                    "Помилка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void ImportFromJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var warning = MessageBox.Show(
                    "УВАГА! Відновлення з резервної копії:\n\n" +
                    "• Видалить ВСІ поточні дані\n" +
                    "• Замінить їх даними з файлу\n" +
                    "• Цю операцію НЕМОЖЛИВО скасувати\n\n" +
                    "Ви впевнені, що хочете продовжити?",
                    "Критичне попередження",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warning != MessageBoxResult.Yes)
                    return;

                var openDialog = new OpenFileDialog
                {
                    Filter = "JSON файли (*.json)|*.json|Всі файли (*.*)|*.*",
                    Title = "Виберіть файл резервної копії"
                };

                if (openDialog.ShowDialog() != true)
                    return;

                string json = await File.ReadAllTextAsync(openDialog.FileName);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                };

                BackupData? backup = JsonSerializer.Deserialize<BackupData>(json, options);

                if (backup == null)
                {
                    MessageBox.Show("Не вдалося прочитати файл резервної копії!", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var finalConfirm = MessageBox.Show(
                    $"Знайдено резервну копію від {backup.BackupDate:dd.MM.yyyy HH:mm}\n\n" +
                    $"Буде відновлено:\n" +
                    $"• Користувачів: {backup.Users.Count}\n" +
                    $"• Ролей: {backup.Roles.Count}\n" +
                    $"• Товарів: {backup.Products.Count}\n" +
                    $"• Надходжень: {backup.Incomings.Count}\n" +
                    $"• Заявок: {backup.OutgoingRequests.Count}\n" +
                    $"• Позицій у заявках: {backup.OutgoingItems.Count}\n" +
                    $"• Логів: {backup.ActionLogs.Count}\n\n" +
                    "ПІДТВЕРДИТИ ВІДНОВЛЕННЯ?",
                    "Остаточне підтвердження",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (finalConfirm != MessageBoxResult.Yes)
                    return;

                await ClearAllData();
                await RestoreData(backup);

                await _logService.CreateAsync(new ActionLog
                {
                    Action = $"{_currentUser.Name} restored the database from a backup from {backup.BackupDate:dd.MM.yyyy HH:mm}",
                    User = _currentUser,
                    CreatedAt = DateTime.Now
                });

                MessageBox.Show(
                    "Базу даних успішно відновлено!\n\nПрограма буде перезапущена.",
                    "Успіх",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                System.Diagnostics.Process.Start(
                    System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                Application.Current.Shutdown();
            }
            catch (DbUpdateException ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"Помилка бази даних: {message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Помилка відновлення з резервної копії:\n\n{ex.Message}",
                    "Помилка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ClearAllData()
        {
            var logs = await _logService.GetAllAsync();
            foreach (var log in logs)
                await _logService.DeleteAsync(log.Id);

            var items = await _itemService.GetAllAsync();
            foreach (var item in items)
                await _itemService.DeleteAsync(item.Id);

            var requests = await _requestService.GetAllAsync();
            foreach (var request in requests)
                await _requestService.DeleteAsync(request.Id);

            var incomings = await _incomingService.GetAllAsync();
            foreach (var incoming in incomings)
                await _incomingService.DeleteAsync(incoming.Id);

            var products = await _productService.GetAllAsync();
            foreach (var product in products)
                await _productService.DeleteAsync(product.Id);

            var users = await _userService.GetAllAsync();
            foreach (var user in users)
                await _userService.DeleteAsync(user.Id);

            var roles = await _roleService.GetAllAsync();
            foreach (var role in roles)
                await _roleService.DeleteAsync(role.Id);
        }

        private async Task RestoreData(BackupData backup)
        {
            var roleIdMap = new Dictionary<int, int>();
            var userIdMap = new Dictionary<int, int>();
            var productIdMap = new Dictionary<int, int>();
            var requestIdMap = new Dictionary<int, int>();

            foreach (var role in backup.Roles)
            {
                int oldId = role.Id;
                var newRole = new Role
                {
                    Name = role.Name,
                };

                var createdRole = await _roleService.CreateAsync(newRole);
                roleIdMap[oldId] = createdRole.Id;
            }

            foreach (var user in backup.Users)
            {
                int oldId = user.Id;
                int oldRoleId = user.Role?.Id ?? 0;

                var newUser = new User
                {
                    Name = user.Name,
                    PasswordHash = user.PasswordHash,
                    Role = oldRoleId > 0 && roleIdMap.ContainsKey(oldRoleId)
                        ? await _roleService.GetByIdAsync(roleIdMap[oldRoleId])
                        : null
                };

                var createdUser = await _userService.CreateAsync(newUser);
                userIdMap[oldId] = createdUser.Id;
            }

            foreach (var product in backup.Products)
            {
                int oldId = product.Id;
                var newProduct = new Product
                {
                    Name = product.Name,
                    Description = product.Description,
                    Unit = product.Unit,
                    Stock = product.Stock,
                    MinimumStock = product.MinimumStock,
                    CreatedAt = product.CreatedAt
                };

                var createdProduct = await _productService.CreateAsync(newProduct);
                productIdMap[oldId] = createdProduct.Id;
            }

            foreach (var incoming in backup.Incomings)
            {
                int oldProductId = incoming.Product?.Id ?? 0;
                int oldUserId = incoming.ReceivedBy?.Id ?? 0;

                var newIncoming = new Incoming
                {
                    Quantity = incoming.Quantity,
                    ReceivedAt = incoming.ReceivedAt,
                    Product = oldProductId > 0 && productIdMap.ContainsKey(oldProductId)
                        ? await _productService.GetByIdAsync(productIdMap[oldProductId])
                        : null,
                    ReceivedBy = oldUserId > 0 && userIdMap.ContainsKey(oldUserId)
                        ? await _userService.GetByIdAsync(userIdMap[oldUserId])
                        : null
                };

                await _incomingService.CreateAsync(newIncoming);
            }

            foreach (var request in backup.OutgoingRequests)
            {
                int oldId = request.Id;
                int oldUserId = request.CreatedBy?.Id ?? 0;

                var newRequest = new OutgoingRequest
                {
                    Status = request.Status,
                    Comment = request.Comment,
                    CreatedAt = request.CreatedAt,
                    CreatedBy = oldUserId > 0 && userIdMap.ContainsKey(oldUserId)
                        ? await _userService.GetByIdAsync(userIdMap[oldUserId])
                        : null
                };

                var createdRequest = await _requestService.CreateAsync(newRequest);
                requestIdMap[oldId] = createdRequest.Id;
            }

            foreach (var item in backup.OutgoingItems)
            {
                int oldRequestId = item.Request?.Id ?? 0;
                int oldProductId = item.Product?.Id ?? 0;

                var newItem = new OutgoingItem
                {
                    Quantity = item.Quantity,
                    Request = oldRequestId > 0 && requestIdMap.ContainsKey(oldRequestId)
                        ? await _requestService.GetByIdAsync(requestIdMap[oldRequestId])
                        : null,
                    Product = oldProductId > 0 && productIdMap.ContainsKey(oldProductId)
                        ? await _productService.GetByIdAsync(productIdMap[oldProductId])
                        : null
                };

                await _itemService.CreateAsync(newItem);
            }

            foreach (var log in backup.ActionLogs)
            {
                int oldUserId = log.User?.Id ?? 0;

                var newLog = new ActionLog
                {
                    Action = log.Action,
                    CreatedAt = log.CreatedAt,
                    User = oldUserId > 0 && userIdMap.ContainsKey(oldUserId)
                        ? await _userService.GetByIdAsync(userIdMap[oldUserId])
                        : null
                };

                await _logService.CreateAsync(newLog);
            }
        }
    }
}
